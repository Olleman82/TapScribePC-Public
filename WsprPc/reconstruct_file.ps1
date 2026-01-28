$prefix = Get-Content 'd:\Appar\wspr-pc\WsprPc\MainWindow.xaml.cs' -TotalCount 2164
$suffix = @"
    private void InitializeDiarizationAdvancedUi()
    {
        // Add listeners for Diarization settings
        DiarizationThresholdSlider.ValueChanged += (s, e) => AutoSaveConfig();
        DiarizationCleanupTextBox.LostFocus += (s, e) => AutoSaveConfig();
        DiarizationPitchProtectionCheckBox.Checked += (s, e) => AutoSaveConfig();
        DiarizationPitchProtectionCheckBox.Unchecked += (s, e) => AutoSaveConfig();
    }

    private void InitializeFileTranscriptionService()
    {
        _fileTranscriptionService = new FileTranscriptionService(_engine, GetAppDataDir());
        CheckDiarizationModelsStatus();
    }

    private void CheckDiarizationModelsStatus()
    {
        if (_fileTranscriptionService != null && !_fileTranscriptionService.ModelsReady)
        {
            DiarizationModelBanner.Visibility = Visibility.Visible;
        }
        else
        {
            DiarizationModelBanner.Visibility = Visibility.Collapsed;
        }
    }

    private async Task DownloadDiarizationModelsAsync()
    {
        if (_fileTranscriptionService == null) return;

        DownloadDiarizationModelsButton.IsEnabled = false;
        DiarizationModelDownloadProgress.Visibility = Visibility.Visible;
        DiarizationModelDownloadStatus.Text = "Laddar ner verktyg och modeller...";

        var progress = new Progress<(int percent, string status)>(p =>
        {
            Dispatcher.Invoke(() =>
            {
                DiarizationModelDownloadProgress.Value = p.percent;
                DiarizationModelDownloadStatus.Text = p.status;
            });
        });

        try
        {
            await _fileTranscriptionService.EnsureModelsAsync(progress);
            DiarizationModelBanner.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            _logger?.Error("Diarization dependency download failed", ex);
            WpfMessageBox.Show($"Kunde inte ladda ner verktyg: {ex.Message}", "Fel", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            DownloadDiarizationModelsButton.IsEnabled = true;
            DiarizationModelDownloadProgress.Visibility = Visibility.Collapsed;
            DiarizationModelDownloadStatus.Text = "";
        }
    }

    private async Task StartFileTranscriptionAsync()
    {
        if (_fileTranscriptionService == null || string.IsNullOrEmpty(_selectedAudioFilePath))
            return;

        // Check if models need to be downloaded
        if (!_fileTranscriptionService.ModelsReady)
        {
            DiarizationModelBanner.Visibility = Visibility.Visible;
            return;
        }

        Stopwatch sw = new Stopwatch();
        try
        {
            _fileTranscriptionCts = new CancellationTokenSource();
            
            // Get audio length
            TimeSpan audioDuration = TimeSpan.Zero;
            try
            {
                using var reader = new NAudio.Wave.AudioFileReader(_selectedAudioFilePath);
                audioDuration = reader.TotalTime;
            }
            catch { }
            
            // Update UI state
            StartFileTranscriptionButton.IsEnabled = false;
            SelectAudioFileButton.IsEnabled = false;
            CancelFileTranscriptionButton.Visibility = Visibility.Visible;
            FileTranscriptionProgressPanel.Visibility = Visibility.Visible;
            FileTranscriptionStatusText.Text = "Laddar ner verktyg...";
            FileTranscriptionPercentText.Text = "0%";
            AudioTotalLengthText.Text = audioDuration.ToString(@"mm\:ss");
            TranscriptionElapsedTimeText.Text = "00:00";

            // Ensure dependencies (including FFmpeg)
            if (!_fileTranscriptionService.ModelsReady)
            {
                await DownloadDiarizationModelsAsync();
                if (!_fileTranscriptionService.ModelsReady) throw new InvalidOperationException("Kunde inte ladda ner nödvändiga verktyg.");
            }

            // Get expected speaker count (Auto = 0, 1 = 1, 2 = 2, ...)
            int? expectedSpeakers = null;
            if (SpeakerCountCombo.SelectedIndex > 0)
            {
                expectedSpeakers = SpeakerCountCombo.SelectedIndex;
            }

            sw.Start();
            
            // Create a timer to update elapsed time UI
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (s, e) => {
                TranscriptionElapsedTimeText.Text = sw.Elapsed.ToString(@"mm\:ss");
            };
            timer.Start();

            var progress = new Progress<(int percent, string status)>(p =>
            {
                Dispatcher.Invoke(() =>
                {
                    FileTranscriptionProgressBar.Value = p.percent;
                    FileTranscriptionStatusText.Text = p.status;
                    FileTranscriptionPercentText.Text = $"{p.percent}%";
                });
            });

            // Sync parameters to service
            _fileTranscriptionService.ClusteringThreshold = (float)DiarizationThresholdSlider.Value;
            _fileTranscriptionService.MinTotalDurationSeconds = ReadDoubleOrFallback(DiarizationCleanupTextBox, 5.0, "Diarization städning");
            _fileTranscriptionService.EnablePitchProtection = DiarizationPitchProtectionCheckBox.IsChecked == true;

            int numThreads = Math.Max(1, Environment.ProcessorCount / 2);
            string result = await _fileTranscriptionService.TranscribeAsync(
                _selectedAudioFilePath,
                expectedSpeakers,
                numThreads,
                progress,
                _fileTranscriptionCts.Token);

            sw.Stop();
            timer.Stop();

            // Calculate speed multiplier
            double speedMultiplier = 0;
            if (audioDuration.TotalSeconds > 0 && sw.Elapsed.TotalSeconds > 0)
            {
                speedMultiplier = audioDuration.TotalSeconds / sw.Elapsed.TotalSeconds;
            }

            // Show result
            FileTranscriptionResultText.Text = result;
            FileTranscriptionResultPanel.Visibility = Visibility.Visible;
            
            // Set stats text
            TranscriptionStatsText.Text = $"Bearbetat på {sw.Elapsed:mm\\:ss} ({speedMultiplier:F1}x ljudhastighet)";
            
            _logger?.Info($"File transcription completed: {result.Length} chars. Speed: {speedMultiplier:F1}x");
        }
        catch (OperationCanceledException)
        {
            _logger?.Info("File transcription cancelled");
            FileTranscriptionStatusText.Text = "Avbruten";
        }
        catch (Exception ex)
        {
            _logger?.Error("File transcription failed", ex);
            FileTranscriptionStatusText.Text = $"Fel: {ex.Message}";
            WpfMessageBox.Show(
                $"Transkribering misslyckades:\n{ex.Message}",
                "Fel",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            // Reset UI state
            StartFileTranscriptionButton.IsEnabled = true;
            SelectAudioFileButton.IsEnabled = true;
            CancelFileTranscriptionButton.Visibility = Visibility.Collapsed;
            FileTranscriptionProgressPanel.Visibility = Visibility.Collapsed;
            _fileTranscriptionCts?.Dispose();
            _fileTranscriptionCts = null;
        }
    }

    private void SelectAudioFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Välj ljudfil",
            Filter = "Ljudfiler (*.mp3;*.wav;*.m4a;*.aac;*.mp4)|*.mp3;*.wav;*.m4a;*.aac;*.mp4|Alla filer (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            _selectedAudioFilePath = dialog.FileName;
            SelectedAudioFileText.Text = Path.GetFileName(dialog.FileName);
            StartFileTranscriptionButton.IsEnabled = true;
        }
    }

    private void CancelFileTranscription()
    {
        _fileTranscriptionCts?.Cancel();
    }

    private void CopyFileTranscriptionResult()
    {
        string text = FileTranscriptionResultText.Text;
        if (!string.IsNullOrWhiteSpace(text))
        {
            System.Windows.Clipboard.SetText(text);
            _trayIcon?.ShowBalloon("TapScribe PC", "Text kopierad till urklipp!");
        }
    }

    private void SaveFileTranscriptionResult()
    {
        string text = FileTranscriptionResultText.Text;
        if (string.IsNullOrWhiteSpace(text))
            return;

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Spara transkription",
            Filter = "Textfil (*.txt)|*.txt|Alla filer (*.*)|*.*",
            DefaultExt = ".txt",
            FileName = Path.GetFileNameWithoutExtension(_selectedAudioFilePath ?? "transkription") + "_transkription.txt"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                File.WriteAllText(dialog.FileName, text);
                _trayIcon?.ShowBalloon("TapScribe PC", $"Sparad till {Path.GetFileName(dialog.FileName)}");
            }
            catch (Exception ex)
            {
                _logger?.Error("Failed to save transcription", ex);
                WpfMessageBox.Show($"Kunde inte spara filen: {ex.Message}", "Fel", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private double ReadDoubleOrFallback(System.Windows.Controls.TextBox box, double fallback, string label)
    {
        if (double.TryParse(box.Text.Replace(',', '.'), CultureInfo.InvariantCulture, out var val)) return val;
        return fallback;
    }
    #endregion
}
"@
Set-Content -Path 'd:\Appar\wspr-pc\WsprPc\MainWindow.xaml.cs' -Value $prefix -Encoding utf8
Add-Content -Path 'd:\Appar\wspr-pc\WsprPc\MainWindow.xaml.cs' -Value $suffix -Encoding utf8
