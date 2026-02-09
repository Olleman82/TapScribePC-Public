using System;
using System.IO;
using System.Windows;
using WpfMessageBox = System.Windows.MessageBox;

namespace WsprPc;

/// <summary>
/// Modal window to display transcription results.
/// </summary>
public partial class TranscriptionResultWindow : Window
{
    private readonly string _transcription;
    private readonly string _fileName;

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public TranscriptionResultWindow(string transcription, string fileName, TimeSpan processingTime, TimeSpan audioLength)
    {
        InitializeComponent();
        _transcription = transcription;
        _fileName = fileName;
        
        // Set title with filename
        TitleText.Text = string.IsNullOrEmpty(fileName) ? "Transkribering" : $"Transkribering – {Path.GetFileName(fileName)}";
        
        // Calculate and display stats
        double speedRatio = audioLength.TotalSeconds > 0 
            ? audioLength.TotalSeconds / processingTime.TotalSeconds 
            : 0;
        
        string timeStr = FormatTimeSpan(processingTime);
        string statsStr = speedRatio > 0 
            ? $"Tid: {timeStr} • Hastighet: {speedRatio:F1}x"
            : $"Tid: {timeStr}";
        StatsText.Text = statsStr;
        
        // Set content
        TranscriptionTextBox.Text = transcription;
        Loaded += (s, e) => ApplyDarkMode();
    }

    private void ApplyDarkMode()
    {
        var interopHelper = new System.Windows.Interop.WindowInteropHelper(this);
        int useDarkMode = 1;
        DwmSetWindowAttribute(interopHelper.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));
    }

    private static string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
            return ts.ToString(@"h\:mm\:ss");
        return ts.ToString(@"m\:ss");
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Windows.Clipboard.SetText(_transcription);
            // Brief visual feedback
            CopyButton.Content = "✓ Kopierad!";
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (_, _) => { CopyButton.Content = "Kopiera text"; timer.Stop(); };
            timer.Start();
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show($"Kunde inte kopiera: {ex.Message}", "Fel", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Textfil (*.txt)|*.txt|Alla filer (*.*)|*.*",
            DefaultExt = ".txt",
            FileName = string.IsNullOrEmpty(_fileName) 
                ? "transkribering.txt" 
                : Path.GetFileNameWithoutExtension(_fileName) + "_transkribering.txt"
        };
        
        if (dlg.ShowDialog() == true)
        {
            try
            {
                File.WriteAllText(dlg.FileName, _transcription);
                WpfMessageBox.Show($"Sparad till:\n{dlg.FileName}", "Sparat", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show($"Kunde inte spara: {ex.Message}", "Fel", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
