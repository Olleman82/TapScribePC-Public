using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WsprPc.Models;
using WsprPc.Services.Diarization;
using WpfMessageBox = System.Windows.MessageBox;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace WsprPc;

/// <summary>
/// Batch queue window for processing multiple audio files.
/// </summary>
public partial class BatchQueueWindow : Window
{
    private readonly FileTranscriptionService _transcriptionService;
    private readonly ObservableCollection<BatchItem> _items = new();
    private CancellationTokenSource? _batchCts;
    private bool _isRunning;
    private readonly float _clusteringThreshold;
    private readonly double _minTotalDuration;
    private readonly bool _enablePitchProtection;
    private readonly bool _detectMeetingType;
    private readonly double _physicalMeetingAdjustment;

    public BatchQueueWindow(FileTranscriptionService transcriptionService, float clusteringThreshold, double minTotalDuration, bool enablePitchProtection, bool detectMeetingType, double physicalMeetingAdjustment)
    {
        InitializeComponent();
        _transcriptionService = transcriptionService;
        _clusteringThreshold = clusteringThreshold;
        _minTotalDuration = minTotalDuration;
        _enablePitchProtection = enablePitchProtection;
        _detectMeetingType = detectMeetingType;
        _physicalMeetingAdjustment = physicalMeetingAdjustment;
        
        FileGrid.ItemsSource = _items;
        DetectMeetingTypeCheckBox.IsChecked = detectMeetingType;
        UpdateFileCount();

        Loaded += (_, _) => ApplyTitleBarTheme(true);
    }

    #region Win32 Dark Mode Title Bar
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    private void ApplyTitleBarTheme(bool dark)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        int value = dark ? 1 : 0;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
    }
    #endregion

    private void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Välj ljudfiler",
            Filter = "Ljudfiler (*.mp3;*.wav;*.m4a;*.aac;*.mp4)|*.mp3;*.wav;*.m4a;*.aac;*.mp4|Alla filer (*.*)|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (var file in dialog.FileNames)
            {
                // Avoid duplicates
                if (!_items.Any(i => i.FilePath.Equals(file, StringComparison.OrdinalIgnoreCase)))
                {
                    _items.Add(new BatchItem 
                    { 
                        FilePath = file, 
                        DiarizationThreshold = _clusteringThreshold,
                        EnablePitchProtection = _enablePitchProtection // Inherit global setting initially
                    });
                }
            }
            UpdateFileCount();
        }
    }

    private void RemoveItem_Click(object sender, RoutedEventArgs e)
    {
        // Handle both simple button click and context menu click
        BatchItem? item = null;
        
        if (sender is System.Windows.Controls.MenuItem menuItem && menuItem.DataContext is BatchItem mItem)
            item = mItem;
        else if (sender is System.Windows.Controls.Button btn && btn.DataContext is BatchItem bItem)
            item = bItem;

        if (item != null)
        {
            _items.Remove(item);
            UpdateFileCount();
        }
    }

    private void ItemMenu_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.ContextMenu != null)
        {
            btn.ContextMenu.DataContext = btn.DataContext; // Ensure context is passed
            btn.ContextMenu.IsOpen = true;
        }
    }

    private void DuplicateItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem menuItem && menuItem.DataContext is BatchItem item)
        {
            var index = _items.IndexOf(item);
            if (index == -1) return;

            var newItem = new BatchItem
            {
                FilePath = item.FilePath,
                DiarizationThreshold = item.DiarizationThreshold,
                EnablePitchProtection = item.EnablePitchProtection,
                SpeakerCount = item.SpeakerCount
            };

            _items.Insert(index + 1, newItem);
            UpdateFileCount();
        }
    }

    private void CreateExperiment_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem menuItem && menuItem.DataContext is BatchItem item)
        {
            var window = new BatchExperimentsWindow(item);
            if (window.ShowDialog() == true && window.GeneratedItems.Count > 0)
            {
                var index = _items.IndexOf(item);
                // Insert items in reverse order so they appear in correct sequence after index
                for (int i = window.GeneratedItems.Count - 1; i >= 0; i--)
                {
                    _items.Insert(index + 1, window.GeneratedItems[i]);
                }
                UpdateFileCount();
            }
        }
    }

    private void ViewResult_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.DataContext is BatchItem item && !string.IsNullOrEmpty(item.Result))
        {
            // Get audio duration for the modal
            TimeSpan audioDuration = TimeSpan.Zero;
            try
            {
                using var reader = new NAudio.Wave.AudioFileReader(item.FilePath);
                audioDuration = reader.TotalTime;
            }
            catch { }

            var resultWindow = new TranscriptionResultWindow(item.Result, item.FilePath, item.ProcessingTime, audioDuration)
            {
                Owner = this
            };
            resultWindow.ShowDialog();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (_isRunning)
        {
            var result = WpfMessageBox.Show(
                "Vill du avbryta den pågående körningen?",
                "Avbryt batch",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                _batchCts?.Cancel();
            }
        }
        else
        {
            DialogResult = false;
            Close();
        }
    }

    private async void StartBatch_Click(object sender, RoutedEventArgs e)
    {
        if (_items.Count == 0)
        {
            WpfMessageBox.Show("Lägg till filer först.", "Ingen fil", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!_transcriptionService.ModelsReady)
        {
            WpfMessageBox.Show("Modeller är inte nedladdade. Gå till huvudfliken och ladda ner dem först.", "Modeller saknas", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _isRunning = true;
        _batchCts = new CancellationTokenSource();
        StartBatchButton.IsEnabled = false;
        AddFilesButton.IsEnabled = false;
        CancelButton.Content = "Avbryt körning";

        try
        {
            await ProcessBatchAsync(_batchCts.Token);
        }
        catch (OperationCanceledException)
        {
            // Mark remaining as cancelled
            foreach (var item in _items.Where(i => i.Status == BatchStatus.Pending))
            {
                item.Status = BatchStatus.Cancelled;
                item.StatusText = "Avbruten";
            }
        }
        finally
        {
            _isRunning = false;
            StartBatchButton.IsEnabled = true;
            AddFilesButton.IsEnabled = true;
            CancelButton.Content = "Stäng";
            _batchCts?.Dispose();
            _batchCts = null;

            // Show summary
            int completed = _items.Count(i => i.Status == BatchStatus.Completed);
            int failed = _items.Count(i => i.Status == BatchStatus.Failed);
            int cancelled = _items.Count(i => i.Status == BatchStatus.Cancelled);
            
            string summary = $"Klart: {completed}";
            if (failed > 0) summary += $", Fel: {failed}";
            if (cancelled > 0) summary += $", Avbrutna: {cancelled}";
            
            FileCountText.Text = summary;

            // Close app if requested
            if (CloseOnCompleteCheckBox.IsChecked == true && failed == 0 && cancelled == 0)
            {
                System.Windows.Application.Current.Shutdown();
            }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        // Apply global settings to service
        // _transcriptionService.ClusteringThreshold is now set per item below
        _transcriptionService.MinTotalDurationSeconds = _minTotalDuration;
        _transcriptionService.EnablePitchProtection = _enablePitchProtection;
        
        // Meeting type detection - uses the checkbox state (can differ from initial setting)
        _transcriptionService.DetectMeetingType = DetectMeetingTypeCheckBox.IsChecked == true;
        _transcriptionService.PhysicalMeetingThresholdAdjustment = _physicalMeetingAdjustment;

        int numThreads = Math.Max(1, Environment.ProcessorCount / 2);

        foreach (var item in _items.Where(i => i.Status == BatchStatus.Pending))
        {
            ct.ThrowIfCancellationRequested();

            item.Status = BatchStatus.Running;
            item.StatusText = "Körs...";
            item.ProgressPercent = 0;

            var sw = Stopwatch.StartNew();

            try
            {
                var progress = new Progress<(int percent, string status)>(p =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        item.ProgressPercent = p.percent;
                        item.StatusText = $"Körs {p.percent}%";
                    });
                });

                // Set per-file settings
                _transcriptionService.ClusteringThreshold = (float)item.DiarizationThreshold;
                _transcriptionService.EnablePitchProtection = item.EnablePitchProtection;
                int? expectedSpeakers = item.SpeakerCount > 0 ? item.SpeakerCount : null;

                string result = await _transcriptionService.TranscribeAsync(
                    item.FilePath,
                    expectedSpeakers,
                    numThreads,
                    progress,
                    ct);

                sw.Stop();
                item.ProcessingTime = sw.Elapsed;
                item.Result = result;
                item.Status = BatchStatus.Completed;
                item.StatusText = "✓ Klar";

                // Auto-save if enabled
                if (AutoSaveCheckBox.IsChecked == true)
                {
                    // Build variant suffix: _t{threshold}_pitch_{on/off}[_{speakerCount}sp]
                    string variantSuffix = $"_t{item.DiarizationThreshold:F2}_pitch_{(item.EnablePitchProtection ? "on" : "off")}";
                    if (item.SpeakerCount > 0)
                    {
                        variantSuffix += $"_{item.SpeakerCount}sp";
                    }
                    
                    string outputPath = Path.Combine(
                        Path.GetDirectoryName(item.FilePath) ?? "",
                        Path.GetFileNameWithoutExtension(item.FilePath) + variantSuffix + ".txt");
                    
                    try
                    {
                        await File.WriteAllTextAsync(outputPath, result, ct);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Auto-save failed: {ex.Message}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                item.Status = BatchStatus.Cancelled;
                item.StatusText = "Avbruten";
                throw;
            }
            catch (Exception ex)
            {
                sw.Stop();
                item.ProcessingTime = sw.Elapsed;
                item.Status = BatchStatus.Failed;
                item.StatusText = "✗ Fel";
                item.ErrorMessage = ex.Message;
            }
        }
    }

    private void UpdateFileCount()
    {
        FileCountText.Text = $"{_items.Count} {(_items.Count == 1 ? "fil" : "filer")} i kö";
    }
}
