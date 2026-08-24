using System.IO;
using System.Windows;
using System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows.Controls.Primitives;
using System;

namespace VideoSelector;

public partial class MainWindow : Window
{
    private List<string> _videoFiles = new List<string>();
    private int _currentIndex = -1;
    private bool _isPlaying = false;
    private DispatcherTimer _timer;
    private bool _isDraggingSlider = false;

    public MainWindow()
    {
        InitializeComponent();
        MediaPlayer.MediaEnded += (s, e) => 
        {
            MediaPlayer.Position = System.TimeSpan.Zero;
            MediaPlayer.Play();
        };
        MediaPlayer.MediaOpened += MediaPlayer_MediaOpened;

        _timer = new DispatcherTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(200);
        _timer.Tick += Timer_Tick;
    }

    private void Timer_Tick(object sender, EventArgs e)
    {
        if (!_isDraggingSlider && MediaPlayer.Source != null && MediaPlayer.NaturalDuration.HasTimeSpan)
        {
            TimelineSlider.Value = MediaPlayer.Position.TotalSeconds;
        }
    }

    private void MediaPlayer_MediaOpened(object sender, RoutedEventArgs e)
    {
        ApplySpeed();
        if (MediaPlayer.NaturalDuration.HasTimeSpan)
        {
            var duration = MediaPlayer.NaturalDuration.TimeSpan;
            int width = MediaPlayer.NaturalVideoWidth;
            int height = MediaPlayer.NaturalVideoHeight;
            
            string resString = $"{width}x{height}";
            if (width >= 3840 || height >= 2160) resString += " (4K)";
            else if (width >= 1920 || height >= 1080) resString += " (1080p)";
            
            TxtVideoInfo.Text = $"{duration:hh\\:mm\\:ss} | {resString}";
            TimelineSlider.Maximum = duration.TotalSeconds;
            _timer.Start();
        }
        else
        {
            TxtVideoInfo.Text = "Unknown duration/resolution";
            TimelineSlider.Maximum = 0;
            _timer.Stop();
        }
    }

    private void ApplySpeed()
    {
        if (CmbSpeed != null && MediaPlayer != null)
        {
            if (CmbSpeed.SelectedIndex == 1) MediaPlayer.SpeedRatio = 2.0;
            else if (CmbSpeed.SelectedIndex == 2) MediaPlayer.SpeedRatio = 4.0;
            else if (CmbSpeed.SelectedIndex == 3) MediaPlayer.SpeedRatio = 6.0;
            else MediaPlayer.SpeedRatio = 1.0;
        }
    }

    private void CmbSpeed_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        ApplySpeed();
    }

    private void TimelineSlider_DragStarted(object sender, DragStartedEventArgs e)
    {
        _isDraggingSlider = true;
    }

    private void TimelineSlider_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        _isDraggingSlider = false;
        MediaPlayer.Position = TimeSpan.FromSeconds(TimelineSlider.Value);
    }

    private void TimelineSlider_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        MediaPlayer.Position = TimeSpan.FromSeconds(TimelineSlider.Value);
    }

    private void BtnSelectFolder_Click(object sender, RoutedEventArgs e)
    {
        using (var dialog = new FolderBrowserDialog())
        {
            dialog.Description = "Select a folder containing MP4 videos";
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                LoadFolder(dialog.SelectedPath);
            }
        }
    }

    private void LoadFolder(string path)
    {
        try
        {
            var directory = new DirectoryInfo(path);
            var files = directory.GetFiles("*.mp4", SearchOption.TopDirectoryOnly);
            
            // Sort by creation date
            _videoFiles = files.OrderBy(f => f.CreationTime).Select(f => f.FullName).ToList();
            
            if (_videoFiles.Count > 0)
            {
                _currentIndex = 0;
                TxtStatus.Text = $"Folder: {path} ({_videoFiles.Count} videos)";
                PlayCurrentVideo();
            }
            else
            {
                TxtStatus.Text = $"Folder: {path} (0 videos)";
                TxtFileName.Text = "No videos found.";
                TxtVideoInfo.Text = "";
                _timer.Stop();
                TimelineSlider.Value = 0;
                MediaPlayer.Stop();
                MediaPlayer.Source = null;
                _videoFiles.Clear();
                _currentIndex = -1;
            }
        }
        catch (System.Exception ex)
        {
            MessageBox.Show($"Error loading folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void PlayCurrentVideo()
    {
        if (_currentIndex >= 0 && _currentIndex < _videoFiles.Count)
        {
            string file = _videoFiles[_currentIndex];
            TxtFileName.Text = $"[{_currentIndex + 1}/{_videoFiles.Count}] {Path.GetFileName(file)}";
            TxtVideoInfo.Text = "Loading info...";
            MediaPlayer.Source = new System.Uri(file);
            MediaPlayer.Play();
            _isPlaying = true;
            BtnPlayPause.Content = "Pause";
        }
    }

    private void BtnPlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (MediaPlayer.Source == null) return;

        if (_isPlaying)
        {
            MediaPlayer.Pause();
            BtnPlayPause.Content = "Play";
            _isPlaying = false;
        }
        else
        {
            MediaPlayer.Play();
            BtnPlayPause.Content = "Pause";
            _isPlaying = true;
        }
    }

    private void BtnNext_Click(object sender, RoutedEventArgs e)
    {
        if (_videoFiles.Count == 0) return;

        _currentIndex++;
        if (_currentIndex >= _videoFiles.Count)
        {
            _currentIndex = 0; // Loop back
        }
        PlayCurrentVideo();
    }

    private void BtnPrevious_Click(object sender, RoutedEventArgs e)
    {
        if (_videoFiles.Count == 0) return;

        _currentIndex--;
        if (_currentIndex < 0)
        {
            _currentIndex = _videoFiles.Count - 1; // Loop back to end
        }
        PlayCurrentVideo();
    }

    private async void BtnReject_Click(object sender, RoutedEventArgs e)
    {
        if (_currentIndex >= 0 && _currentIndex < _videoFiles.Count)
        {
            string fileToMove = _videoFiles[_currentIndex];
            
            // Stop media player to release file lock
            _timer.Stop();
            TimelineSlider.Value = 0;
            MediaPlayer.Stop();
            MediaPlayer.Source = null;

            BtnReject.IsEnabled = false;
            string oldFileName = TxtFileName.Text;
            TxtFileName.Text = "Moving...";
            TxtVideoInfo.Text = "";

            try
            {
                await Task.Run(() => 
                {
                    string targetFolder = Path.Combine(Path.GetDirectoryName(fileToMove)!, "Rejected");
                    if (!Directory.Exists(targetFolder))
                    {
                        Directory.CreateDirectory(targetFolder);
                    }
                    string targetFile = Path.Combine(targetFolder, Path.GetFileName(fileToMove));
                    
                    if (File.Exists(targetFile))
                    {
                        File.Delete(targetFile);
                    }
                    
                    File.Move(fileToMove, targetFile);
                });

                _videoFiles.RemoveAt(_currentIndex);

                if (_videoFiles.Count == 0)
                {
                    _currentIndex = -1;
                    TxtFileName.Text = "No more videos.";
                    TxtVideoInfo.Text = "";
                    TxtStatus.Text = "All videos moved or none left.";
                }
                else
                {
                    // Adjust index if we moved the last item
                    if (_currentIndex >= _videoFiles.Count)
                    {
                        _currentIndex = 0;
                    }
                    TxtStatus.Text = $"Remaining videos: {_videoFiles.Count}";
                    PlayCurrentVideo();
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Failed to move file:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                TxtFileName.Text = oldFileName; // Restore text if move failed
                PlayCurrentVideo();
            }
            finally
            {
                BtnReject.IsEnabled = true;
            }
        }
    }
}