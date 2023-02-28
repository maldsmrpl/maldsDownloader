using AngleSharp;
using AngleSharp.Common;
using AngleSharp.Io;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Net;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos.Streams;
using static System.Net.Mime.MediaTypeNames;

namespace malds_yt_downloader
{
    public partial class MainWindow : Window
    {
        WebClient client = new WebClient();

        ObservableCollection<DownloadTask> videoTask 
            = new ObservableCollection<DownloadTask>();
        ObservableCollection<DownloadTask> playlistTask 
            = new ObservableCollection<DownloadTask>();
        ObservableCollection<DownloadTask> channelTask
            = new ObservableCollection<DownloadTask>();

        bool isDownloadingInProgress = false;
        bool isDownloadingPaused = false;

        string dataFile = "data.bin";
        string configFile = "config.ini";

        private void StartDownload(string urlToDownload, string filePath, string fileName)
        {
            if (!isDownloadingInProgress)
            {
                try
                {
                    isDownloadingInProgress = true;
                    
                    client.DownloadProgressChanged 
                        += new DownloadProgressChangedEventHandler(client_DownloadProgressChanged);
                    client.DownloadFileCompleted 
                        += new AsyncCompletedEventHandler(client_DownloadFileCompleted);
                    client.DownloadFileAsync(new Uri(urlToDownload), filePath + fileName);
                }
                catch (Exception err)
                { 
                    MessageBox.Show($"Download error - {err.Message} \n \n Please try later"); 
                }
            }
        }

        void client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            double bytesIn = double.Parse(e.BytesReceived.ToString());
            double totalBytes = double.Parse(e.TotalBytesToReceive.ToString());
            double percentage = bytesIn / totalBytes * 100;
            videoTask[IndexOfFirstItem()].Progress = $"{String.Format("{0:0.0}", percentage)}%";
            RemainToDownloadTextBlock.Text = $"Залишилось: {GetRoundedSize((double)videoTask.Sum(s => s.SizeByteTotal) - bytesIn)}";
            UpdateDataGrid();            
        }

        void client_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            videoTask[IndexOfFirstItem()].Progress = "Завантажено";
            videoTask[IndexOfFirstItem()].Status = Status.Completed;
            DeleteDownloadQueue(IndexOfFirstItem());
            isDownloadingInProgress = false;
            RemainToDownloadTextBlock.Text = $"Залишилось: {GetRoundedSize((int)videoTask.Sum(s => s.SizeByteTotal))}";
            StartNextDownload();
            UpdateDataGrid();
        }

        public string GetRoundedSize(double bytes)
        {
            double roundedSize = 0;
            roundedSize = bytes / 1024;
            
            if (roundedSize < 1024)
            {
                return $"{String.Format("{0:0.0}", roundedSize)} кб";
            }
            else
            {
                roundedSize /= 1024;
            }
            if (roundedSize < 1024)
            {
                return $"{String.Format("{0:0.##}", roundedSize)} мб";
            }
            else
            {
                roundedSize /= 1024;
                return $"{String.Format("{0:0.##}", roundedSize)} Гб";
            }
        }

        public void UpdateDataGrid()
        {
            VideoDataGrid.Dispatcher.BeginInvoke(new Action(() 
                => VideoDataGrid.Items.Refresh()), System.Windows.Threading.DispatcherPriority.Background);
        }

        public void CreateFolderIfNotExist(string folderName)
        {
            if (!Directory.Exists(folderName))
            {
                Directory.CreateDirectory(folderName);
            }
        }

        public void StartNextDownload()
        {
            if (videoTask.Count >= 0)
            {
                isDownloadingPaused = false;

                if (DownloadPathTextBox.Text[DownloadPathTextBox.Text.Length - 1] != '\\')
                {
                    DownloadPathTextBox.Text = DownloadPathTextBox.Text + '\\';
                }

                for (int i = 0; i < videoTask.Count; i++)
                {
                    if (videoTask[i].DownloadQueue == 1)
                    {
                        if (isFoldersByAuthorCheckBox.IsChecked.Value && 
                            videoTask[i].PlaylistName == null)
                        {
                            videoTask[i].FilePath = 
                                DownloadPathTextBox.Text + 
                                FileNameWithAccaptableCharacters(videoTask[i].Author) + '\\';
                        }
                        else if (isFoldersByAuthorCheckBox.IsChecked.Value && 
                            videoTask[i].PlaylistName != null)
                        {
                            videoTask[i].FilePath = 
                                DownloadPathTextBox.Text + 
                                FileNameWithAccaptableCharacters(videoTask[i].Author) + '\\' + 
                                FileNameWithAccaptableCharacters(videoTask[i].PlaylistName) + '\\';
                        }
                        else if (!isFoldersByAuthorCheckBox.IsChecked.Value && 
                            videoTask[i].PlaylistName != null)
                        {
                            videoTask[i].FilePath = 
                                DownloadPathTextBox.Text + 
                                FileNameWithAccaptableCharacters(videoTask[i].PlaylistName) + '\\';
                        }
                        else
                        {
                            videoTask[i].FilePath = DownloadPathTextBox.Text;
                        }

                        CreateFolderIfNotExist(videoTask[i].FilePath);
                        StartDownload(videoTask[i].VideoUrl, videoTask[i].FilePath, videoTask[i].FileName);
                        videoTask[i].Status = Status.InProgress;
                    }
                }
            }
        }

        public static void OpenWithDefaultProgram(string path)
        {
            using Process fileopener = new Process();

            fileopener.StartInfo.FileName = "explorer";
            fileopener.StartInfo.Arguments = "\"" + path + "\"";
            fileopener.Start();
        }

        public void SaveSettingsToIni(string configFile = "config.ini")
        {
            if (File.Exists(configFile))
            {
                File.Delete(configFile);
            }
            IniFile mySettings = new IniFile(configFile);
            mySettings.Write("download path", DownloadPathTextBox.Text, "Settings");
            mySettings.Write("quality", VideoQualityComboBox.SelectedIndex.ToString(), "Settings");
            mySettings.Write("isFoldersByAuthor", isFoldersByAuthorCheckBox.IsChecked.ToString(), "Settings");
            mySettings.Write("isDeleteAfterCompletion", isDeleteAfterCompletionCheckBox.IsChecked.ToString(), "Settings");
        }

        public void LoadSettingsFromIni(string configFile = "config.ini")
        {
            if (File.Exists(configFile))
            {
                IniFile mySettings = new IniFile(configFile);
                DownloadPathTextBox.Text = mySettings.Read("download path", "Settings");

                if (mySettings.Read("quality", "Settings") == "1")
                {
                    VideoQualityComboBox.SelectedIndex = 1;
                }
                else
                {
                    VideoQualityComboBox.SelectedIndex = 0;
                }

                if (mySettings.Read("isFoldersByAuthor", "Settings") == "False")
                {
                    isFoldersByAuthorCheckBox.IsChecked = false;
                }
                else
                {
                    isFoldersByAuthorCheckBox.IsChecked = true;
                }

                if (mySettings.Read("isDeleteAfterCompletion", "Settings") == "False")
                {
                    isDeleteAfterCompletionCheckBox.IsChecked = false;
                }
                else
                {
                    isDeleteAfterCompletionCheckBox.IsChecked = true;
                }
            }
        }

        public int IndexOfFirstItem()
        {
            for (int i = 0; i < videoTask.Count; i++)
            {
                if (videoTask[i].DownloadQueue == 1)
                {
                    return i;
                }
            }
            return -1;
        }

        public string FileNameWithAccaptableCharacters(string path)
        {
            List<char> charsToRemove = new List<char>() { '\\', '/', ':', '*', '?', '\"', '<', '>', '|' };
            foreach (char c in charsToRemove)
            {
                path = path.Replace(c.ToString(), String.Empty);
            }
            return path;
        }

        public void DeleteDownloadQueue(int DownloadQueueNumber)
        {
            if (videoTask[DownloadQueueNumber].DownloadQueue != null)
            {
                for (int i = 0; i < videoTask.Count; i++)
                {
                    if (videoTask[i].DownloadQueue > videoTask[DownloadQueueNumber].DownloadQueue)
                    {
                        videoTask[i].DownloadQueue -= 1;
                    }
                }
                videoTask[DownloadQueueNumber].DownloadQueue = null;

                if (isDeleteAfterCompletionCheckBox.IsChecked.Value)
                {
                    videoTask.RemoveAt(DownloadQueueNumber);
                }
            }
        }

        public int AddToDownloadQueue (ObservableCollection<DownloadTask> collection)
        {
            int unFinishedConter = 0;
            for (int i = 0; i < collection.Count; i++)
            {
                if (collection[i].DownloadQueue != null)
                {
                    unFinishedConter++;
                }
            }
            return unFinishedConter + 1;
        }

        public async void AddVideoToCollection(string url, ObservableCollection<DownloadTask> collection, bool shallIStartDownloading = false, string nameOfPlaylist = null)
        {
            try
            {
                var youtube = new YoutubeClient();
                var video = await youtube.Videos.GetAsync(url);
                var streamManifest = await youtube.Videos.Streams.GetManifestAsync(url);

                DownloadTask addTask = new DownloadTask();
                addTask.Title = video.Title;
                addTask.Description = video.Description;
                addTask.Author = video.Author.ChannelTitle;
                addTask.YouTubeUrl = video.Url;
                addTask.Thumb = video.Thumbnails.GetWithHighestResolution().Url;
                addTask.Duration = video.Duration.ToString();
                addTask.YouTubeUrl = video.Url.ToString();
                addTask.UploadDate = video.UploadDate.Date;
                addTask.UploadDateString = addTask.UploadDate.ToString("yyyy-MM-dd");
                addTask.IsSelected = true;

                if (VideoQualityComboBox.SelectedIndex == 0)
                {
                    addTask.Quality = "360p";
                }
                else
                {
                    addTask.Quality = "720p";
                }

                for (int i = 0; i < streamManifest.GetMuxedStreams().Count(); i++)
                {
                    if (streamManifest.GetMuxedStreams().GetItemByIndex(i).VideoQuality.Label == addTask.Quality)
                    {
                        addTask.VideoUrl = streamManifest.GetMuxedStreams().GetItemByIndex(i).Url;
                        addTask.SizeByteTotal = streamManifest.GetMuxedStreams().GetItemByIndex(i).Size.Bytes;
                        addTask.SizeToDisplay = $"{String.Format("{0:0.00}", (((double)addTask.SizeByteTotal / 1024) / 1024))} мб";
                        addTask.Container = streamManifest.GetMuxedStreams().GetItemByIndex(i).Container.ToString();
                    }
                }

                addTask.PlaylistName = nameOfPlaylist;
                addTask.FileName = FileNameWithAccaptableCharacters(
                    $"{addTask.UploadDateString} - {addTask.Title} - ({addTask.Quality}).{addTask.Container}");

                if (shallIStartDownloading)
                {
                    addTask.DownloadQueue = AddToDownloadQueue(videoTask);
                }

                addTask.Status = Status.InQueue;

                collection.Add(addTask);

                if (!isDownloadingInProgress && !isDownloadingPaused)
                {
                    StartNextDownload();
                }
            }
            catch
            {
                MessageBox.Show("Adding error");
            }
            finally
            {
                UpdateDataGrid();
            }
            
        }

    public MainWindow()
        {
            InitializeComponent();
            VideoDataGrid.ItemsSource = videoTask;
            LoadSettingsFromIni(configFile);
        }

        public void VideoAddButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddVideoToCollection(VideoUrlTextBox.Text, videoTask, true);
                UpdateDataGrid();
            }
            catch (Exception err)
            {
                MessageBox.Show($"Adding error - {err.Message} \n \n Please try later");
            }
            finally
            {
                VideoUrlTextBox.Text = "";
                VideoUrlTextBox.Focus();
            }
        }

        private void VideoDataGrid_Loaded(object sender, RoutedEventArgs e)
        {
            VideoDataGrid.ItemsSource = videoTask;
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (VideoDataGrid.SelectedIndex != -1)
            {
                DeleteDownloadQueue(VideoDataGrid.SelectedIndex);
                videoTask.RemoveAt(VideoDataGrid.SelectedIndex);
            }
            UpdateDataGrid();
        }

        private void DeleteAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isDownloadingInProgress && !isDownloadingPaused)
            {
                videoTask.Clear();
            }
            else
            {
                for (int i = 0; i < videoTask.Count; i++)
                {
                    if (videoTask[i].DownloadQueue != 1)
                    {
                        videoTask.RemoveAt(i);
                        i = 0;
                    }
                }
            }
            UpdateDataGrid();
        }

        private void VideoUrlTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            VideoUrlTextBox.Text = string.Empty;
        }

        private void VideoUrlTextBox_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            VideoUrlTextBox.Text = string.Empty;
        }

        private void VideoDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (videoTask[VideoDataGrid.SelectedIndex].FilePath != null &&
                videoTask[VideoDataGrid.SelectedIndex].FileName != null &&
                videoTask[VideoDataGrid.SelectedIndex].Status == Status.Completed)
            {
                if (File.Exists(videoTask[VideoDataGrid.SelectedIndex].FilePath + 
                    videoTask[VideoDataGrid.SelectedIndex].FileName))
                {
                    try
                    {
                        OpenWithDefaultProgram(videoTask[VideoDataGrid.SelectedIndex].FilePath +
                    videoTask[VideoDataGrid.SelectedIndex].FileName);
                    }
                    catch
                    {
                        MessageBox.Show("File not found");
                    }
                }
            }

            MessageBox.Show(videoTask[VideoDataGrid.SelectedIndex].Description);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

            if (File.Exists(dataFile))
            {
                BinaryFormatter formatter = new BinaryFormatter();
                Stream data = File.OpenRead(dataFile);
                if (data.Length != 0)
                {
                    videoTask = formatter.Deserialize(data) as ObservableCollection<DownloadTask>;
                }
                data.Close();
            }
            else
            {
                File.Create(dataFile);
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (File.Exists(dataFile))
            {
                BinaryFormatter formatter = new BinaryFormatter();
                Stream data = File.OpenWrite(dataFile);
                formatter.Serialize(data, videoTask);
                data.Close();
            }
            else
            {
                File.Create(dataFile);
            }

            SaveSettingsToIni(configFile);
        }

        private void StartDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            StartNextDownload();
        }

        private void PauseDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            isDownloadingPaused = true;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (VideoTab.IsFocused)
                {
                    VideoAddButton_Click(sender, e);
                }
                else if (PlaylistTab.IsFocused)
                {
                    PlaylistAddButton_Click(sender, e);
                }
                else if (ChannelTab.IsFocused)
                {
                    // ChannelAddButton_Click(sender, e);
                }
            }
        }

        private async void PlaylistAddButton_Click(object sender, RoutedEventArgs err)
        {
            try
            {
                var youtube = new YoutubeClient();
                var playlist = await youtube.Playlists.GetAsync(PlaylistUrlTextBox.Text);

                await foreach (var batch in youtube.Playlists.GetVideoBatchesAsync(PlaylistUrlTextBox.Text))
                {
                    foreach (var video in batch.Items)
                    {
                        AddVideoToCollection(video.Url, playlistTask, nameOfPlaylist: playlist.Title);
                        PlaylistDataGrid.ItemsSource = playlistTask;
                    }
                }
            }
            catch (Exception error)
            { 
                MessageBox.Show($"Error of adding Playlist \n {error.Message}"); 
            }
            finally { PlaylistDataGrid.ItemsSource = playlistTask; }
        }

        private void AddSelectedVideosButton_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < playlistTask.Count; i++)
            {
                if (playlistTask[i].IsSelected)
                {
                    playlistTask[i].DownloadQueue = AddToDownloadQueue(videoTask);
                    videoTask.Add(playlistTask[i]);
                    if (!isDownloadingInProgress || !isDownloadingPaused)
                    {
                        StartNextDownload();
                    }
                }
            }
            VideoTab.Focus();
            playlistTask.Clear();
            PlaylistUrlTextBox.Text = string.Empty;
        }

        private void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            SaveSettingsToIni(configFile);
        }

        private void ClearPlaylistButton_Click(object sender, RoutedEventArgs e)
        {
            playlistTask.Clear();
            PlaylistUrlTextBox.Text = string.Empty;
            PlaylistUrlTextBox.Focus();
        }

        private void PlaylistUrlTextBox_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            PlaylistUrlTextBox.Text = string.Empty;
        }

        private void PlaylistUrlTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            PlaylistUrlTextBox.Text = string.Empty;
        }
    }
}
