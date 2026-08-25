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
using System.Windows.Media;

namespace VideoSelector;

public partial class MainWindow : Window
{
    private List<string> _videoFiles = new List<string>();
    private int _currentIndex = -1;
    private bool _isPlaying = false;
    private DispatcherTimer _timer;
    private bool _isDraggingSlider = false;
    private bool _isUpdatingSlider = false;
    private double _currentSpeed = 1.0;
    private Stack<(string OriginalPath, string TargetPath, int Index)> _undoStack = new Stack<(string, string, int)>();

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

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (!_isDraggingSlider && MediaPlayer.Source != null && MediaPlayer.NaturalDuration.HasTimeSpan)
        {
            _isUpdatingSlider = true;
            TimelineSlider.Value = MediaPlayer.Position.TotalSeconds;
            _isUpdatingSlider = false;
        }
    }

    private void MediaPlayer_MediaOpened(object sender, RoutedEventArgs e)
    {
        MediaPlayer.SpeedRatio = _currentSpeed;
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

    private void SetSpeed(double speed, System.Windows.Controls.Button btn)
    {
        _currentSpeed = speed;
        if (MediaPlayer != null)
        {
            MediaPlayer.SpeedRatio = speed;
        }
        
        var normalBg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(51, 51, 51));
        BtnSpeed1x.Background = normalBg;
        BtnSpeed2x.Background = normalBg;
        BtnSpeed3x.Background = normalBg;
        BtnSpeed4x.Background = normalBg;
        BtnSpeed6x.Background = normalBg;

        btn.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 215));
    }

    private void BtnSpeed_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string speedStr && double.TryParse(speedStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double speed))
        {
            SetSpeed(speed, btn);
        }
    }

    private void TimelineSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isUpdatingSlider && !_isDraggingSlider && MediaPlayer.Source != null)
        {
            MediaPlayer.Position = TimeSpan.FromSeconds(TimelineSlider.Value);
        }
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
                _undoStack.Clear();
                BtnUndo.IsEnabled = false;
                PlayCurrentVideo();
            }
            else
            {
                TxtStatus.Text = $"Folder: {path} (0 videos)";
                TxtFileName.Text = "No videos found.";
                TxtVideoInfo.Text = "";
                TxtCounter.Text = "";
                _timer.Stop();
                TimelineSlider.Value = 0;
                MediaPlayer.Stop();
                MediaPlayer.Source = null;
                _videoFiles.Clear();
                _undoStack.Clear();
                BtnUndo.IsEnabled = false;
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
            TxtFileName.Text = Path.GetFileName(file);
            TxtCounter.Text = $"[{_currentIndex + 1}/{_videoFiles.Count}]";
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
            int rejectedIndex = _currentIndex;
            
            // Stop media player to release file lock
            _timer.Stop();
            TimelineSlider.Value = 0;
            MediaPlayer.Stop();
            MediaPlayer.Source = null;

            BtnReject.IsEnabled = false;
            BtnUndo.IsEnabled = false;
            string oldFileName = TxtFileName.Text;
            TxtFileName.Text = "Moving...";
            TxtVideoInfo.Text = "";
            string oldCounter = TxtCounter.Text;
            TxtCounter.Text = "";

            try
            {
                string targetFolder = Path.Combine(Path.GetDirectoryName(fileToMove)!, "Rejected");
                string targetFile = Path.Combine(targetFolder, Path.GetFileName(fileToMove));

                await Task.Run(() => 
                {
                    if (!Directory.Exists(targetFolder))
                    {
                        Directory.CreateDirectory(targetFolder);
                    }
                    
                    if (File.Exists(targetFile))
                    {
                        File.Delete(targetFile);
                    }
                    
                    File.Move(fileToMove, targetFile);
                });

                _undoStack.Push((fileToMove, targetFile, rejectedIndex));
                _videoFiles.RemoveAt(_currentIndex);

                if (_videoFiles.Count == 0)
                {
                    _currentIndex = -1;
                    TxtFileName.Text = "No more videos.";
                    TxtVideoInfo.Text = "";
                    TxtCounter.Text = "";
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
                TxtCounter.Text = oldCounter;
                PlayCurrentVideo();
            }
            finally
            {
                BtnReject.IsEnabled = true;
                BtnUndo.IsEnabled = _undoStack.Count > 0;
            }
        }
    }

    private async void BtnUndo_Click(object sender, RoutedEventArgs e)
    {
        if (_undoStack.Count > 0)
        {
            var undoAction = _undoStack.Pop();
            
            _timer.Stop();
            TimelineSlider.Value = 0;
            MediaPlayer.Stop();
            MediaPlayer.Source = null;

            BtnReject.IsEnabled = false;
            BtnUndo.IsEnabled = false;
            string oldFileName = TxtFileName.Text;
            TxtFileName.Text = "Undoing...";
            TxtVideoInfo.Text = "";
            string oldCounter = TxtCounter.Text;
            TxtCounter.Text = "";

            try
            {
                await Task.Run(() => 
                {
                    if (File.Exists(undoAction.TargetPath))
                    {
                        if (File.Exists(undoAction.OriginalPath))
                            File.Delete(undoAction.OriginalPath);
                        File.Move(undoAction.TargetPath, undoAction.OriginalPath);
                    }
                });

                int insertIndex = undoAction.Index;
                if (insertIndex > _videoFiles.Count) 
                {
                    insertIndex = _videoFiles.Count;
                }
                
                _videoFiles.Insert(insertIndex, undoAction.OriginalPath);
                
                _currentIndex = insertIndex;
                TxtStatus.Text = $"Remaining videos: {_videoFiles.Count}";
                PlayCurrentVideo();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Failed to undo file move:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                TxtFileName.Text = oldFileName;
                TxtCounter.Text = oldCounter;
                PlayCurrentVideo();
            }
            finally
            {
                BtnReject.IsEnabled = true;
                BtnUndo.IsEnabled = _undoStack.Count > 0;
            }
        }
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Right)
        {
            BtnNext_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.Left)
        {
            BtnPrevious_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.Up)
        {
            if (MediaPlayer.Source != null && MediaPlayer.NaturalDuration.HasTimeSpan)
            {
                var newPos = MediaPlayer.Position.TotalSeconds + 5;
                if (newPos > TimelineSlider.Maximum) newPos = TimelineSlider.Maximum;
                MediaPlayer.Position = TimeSpan.FromSeconds(newPos);
            }
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.Down)
        {
            if (MediaPlayer.Source != null)
            {
                var newPos = MediaPlayer.Position.TotalSeconds - 5;
                if (newPos < 0) newPos = 0;
                MediaPlayer.Position = TimeSpan.FromSeconds(newPos);
            }
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.Delete)
        {
            if (BtnReject.IsEnabled)
            {
                BtnReject_Click(this, new RoutedEventArgs());
            }
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.Back)
        {
            if (BtnUndo.IsEnabled)
            {
                BtnUndo_Click(this, new RoutedEventArgs());
            }
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.Space)
        {
            BtnPlayPause_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.Enter)
        {
            BtnSelectFolder_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.D1 || e.Key == System.Windows.Input.Key.NumPad1)
        {
            SetSpeed(1.0, BtnSpeed1x);
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.D2 || e.Key == System.Windows.Input.Key.NumPad2)
        {
            SetSpeed(2.0, BtnSpeed2x);
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.D3 || e.Key == System.Windows.Input.Key.NumPad3)
        {
            SetSpeed(3.0, BtnSpeed3x);
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.D4 || e.Key == System.Windows.Input.Key.NumPad4)
        {
            SetSpeed(4.0, BtnSpeed4x);
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.D6 || e.Key == System.Windows.Input.Key.NumPad6)
        {
            SetSpeed(6.0, BtnSpeed6x);
            e.Handled = true;
        }
    }
}