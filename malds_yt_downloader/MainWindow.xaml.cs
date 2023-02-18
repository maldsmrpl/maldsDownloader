using AngleSharp.Common;
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
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        ObservableCollection<DownloadTask> videoTask
                = new ObservableCollection<DownloadTask>();

        bool isDownloadingInProgress = false;
        bool isDownloadingPaused = false;

        string dataFile = "data.bin";
        string configFile = "config.bin";

        private void StartDownload(string urlToDownload, string filePath, string fileName)
        {
            if (!isDownloadingInProgress)
            {
                try
                {
                    isDownloadingInProgress = true;
                    WebClient client = new WebClient();
                    client.DownloadProgressChanged += new DownloadProgressChangedEventHandler(client_DownloadProgressChanged);
                    client.DownloadFileCompleted += new AsyncCompletedEventHandler(client_DownloadFileCompleted);
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
            UpdateDataGrid();
        }

        void client_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            videoTask[IndexOfFirstItem()].Progress = "Завантажено";
            videoTask[IndexOfFirstItem()].Status = DownloadType.Completed;
            DeleteDownloadQueue(IndexOfFirstItem());
            isDownloadingInProgress = false;
            StartNextDownload();
            UpdateDataGrid();
        }

        public void UpdateDataGrid()
        {
            VideoDataGrid.Dispatcher.BeginInvoke(new Action(() => VideoDataGrid.Items.Refresh()), System.Windows.Threading.DispatcherPriority.Background);
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

                if (!Directory.Exists(DownloadPathTextBox.Text))
                {
                    Directory.CreateDirectory(DownloadPathTextBox.Text);
                }

                for (int i = 0; i < videoTask.Count; i++)
                {
                    if (videoTask[i].DownloadQueue == 1)
                    {
                        string pathDevidedByAuthors = DownloadPathTextBox.Text + FileNameWithAccaptableCharacters(videoTask[i].Author) + '\\';
                        videoTask[i].FilePath = pathDevidedByAuthors;

                        if (!Directory.Exists(pathDevidedByAuthors))
                        {
                            Directory.CreateDirectory(pathDevidedByAuthors);
                        }
                        StartDownload(videoTask[i].VideoUrl, pathDevidedByAuthors, videoTask[i].FileName);
                    }
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

        public int? MaxDownloadQueueNumber()
        {
            int maxDownloadQueue = 0;
            for (int i = 0; i < videoTask.Count; i++)
            {
                if (maxDownloadQueue < videoTask[i].DownloadQueue)
                {
                    if (videoTask[i].DownloadQueue != null)
                    {
                        maxDownloadQueue = (int)videoTask[i].DownloadQueue;
                    }
                }
            }
            if (maxDownloadQueue != 0)
            {
                return maxDownloadQueue;
            }
            else
            {
                return null;
            }
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
            }
        }

    public MainWindow()
        {
            InitializeComponent();
            VideoDataGrid.ItemsSource = videoTask;
        }

        public async void VideoAddButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var youtube = new YoutubeClient();
                var video = await youtube.Videos.GetAsync(VideoUrlTextBox.Text);
                var streamManifest = await youtube.Videos.Streams.GetManifestAsync(VideoUrlTextBox.Text);

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

                int unFinishedConter = 0;
                for (int i = 0; i < videoTask.Count; i++)
                {
                    if (videoTask[i].DownloadQueue != null)
                    {
                        unFinishedConter++;
                    }
                }
                addTask.DownloadQueue = unFinishedConter + 1;

                addTask.FileName = FileNameWithAccaptableCharacters($"{addTask.UploadDateString} - {addTask.Author} - {addTask.Title} - ({addTask.Quality}).{addTask.Container}");

                videoTask.Add(addTask);

                addTask.Status = DownloadType.InProgress;

                if (!isDownloadingInProgress && !isDownloadingPaused)
                {
                    StartNextDownload();
                }
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
            VideoUrlTextBox.Text = "";
        }

        private void VideoUrlTextBox_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            VideoUrlTextBox.Text = "";
        }

        private void VideoDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            MessageBox.Show(videoTask[VideoDataGrid.SelectedIndex].Description);
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

            if (File.Exists(configFile))
            {
                BinaryFormatter formatter = new BinaryFormatter();
                /*
                Stream data = File.OpenWrite(dataFile);
                formatter.Serialize(data, videoTask);
                data.Close();
                */

            }
            else
            {
                File.Create(configFile);
            }

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

            if (File.Exists(configFile))
            {
                BinaryFormatter formatter = new BinaryFormatter();
                /*
                Stream data = File.OpenWrite(dataFile);
                formatter.Serialize(data, videoTask);
                data.Close();
                */

            }
            else
            {
                File.Create(configFile);
            }

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
                VideoAddButton_Click(sender, e);
            }
        }
    }
}
