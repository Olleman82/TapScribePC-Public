using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using WsprPc.Models;
using MessageBox = System.Windows.MessageBox;

namespace WsprPc;

/// <summary>
/// Dialog for creating batch experiments with variations.
/// </summary>
public partial class BatchExperimentsWindow : Window
{
    private readonly BatchItem _originalItem;
    public List<BatchItem> GeneratedItems { get; private set; } = new();

    public BatchExperimentsWindow(BatchItem originalItem)
    {
        InitializeComponent();
        _originalItem = originalItem;
        
        // Setup initial UI state
        SubtitleText.Text = $"Skapa varianter för: {_originalItem.FileName}";
        
        // Pre-fill speaker count if set on original
        if (_originalItem.SpeakerCount > 0)
        {
            RadioSpeakersFixed.IsChecked = true;
            SpeakerCountInput.Text = _originalItem.SpeakerCount.ToString();
        }

        // Apply theme hook
        Loaded += (_, _) => ApplyTitleBarTheme(true);

        // Attach listeners for updating summary
        ThresholdInput.TextChanged += UpdateSummary;
        RadioPitchOn.Checked += UpdateSummary;
        RadioPitchOff.Checked += UpdateSummary;
        RadioPitchBoth.Checked += UpdateSummary;
        
        UpdateSummary(null, null);
    }
    
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    private void ApplyTitleBarTheme(bool dark)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        int value = dark ? 1 : 0;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
    }

    private void UpdateSummary(object? sender, EventArgs? e)
    {
        if (SummaryText == null) return;
        
        int count = CalculateTotalVariations();
        SummaryText.Text = $"Genererar {count} varianter";
    }

    private int CalculateTotalVariations()
    {
        var thresholds = ParseThresholds(ThresholdInput.Text);
        int multiplier = RadioPitchBoth.IsChecked == true ? 2 : 1;
        return thresholds.Count * multiplier;
    }

    private List<double> ParseThresholds(string input)
    {
        var result = new List<double>();
        if (string.IsNullOrWhiteSpace(input)) return result;

        var parts = input.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            // Handle both dot and comma decimal separators
            string sanitized = part.Replace(',', '.');
            // Try parse using invariant culture (dot) first, then current
            if (double.TryParse(sanitized, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
            {
                result.Add(val);
            }
        }
        return result;
    }

    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        var thresholds = ParseThresholds(ThresholdInput.Text);
        if (thresholds.Count == 0)
        {
            MessageBox.Show("Ange minst ett giltigt tröskelvärde (t.ex. 1.0).", "Felaktig inmatning", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        int speakerCount = 0;
        if (RadioSpeakersFixed.IsChecked == true)
        {
            if (!int.TryParse(SpeakerCountInput.Text, out speakerCount) || speakerCount < 1)
            {
                MessageBox.Show("Ange ett giltigt antal talare (heeltal > 0).", "Felaktig inmatning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        GeneratedItems.Clear();

        foreach (var threshold in thresholds)
        {
            // Determine pitch setting(s)
            var pitchSettings = new List<bool>();
            
            if (RadioPitchBoth.IsChecked == true)
            {
                pitchSettings.Add(true);
                pitchSettings.Add(false);
            }
            else
            {
                pitchSettings.Add(RadioPitchOn.IsChecked == true);
            }

            foreach (var pitch in pitchSettings)
            {
                var newItem = new BatchItem
                {
                    FilePath = _originalItem.FilePath,
                    DiarizationThreshold = threshold,
                    EnablePitchProtection = pitch,
                    SpeakerCount = speakerCount
                };
                GeneratedItems.Add(newItem);
            }
        }

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
