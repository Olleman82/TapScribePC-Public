using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using WpfMessageBox = System.Windows.MessageBox;
using WsprPc.Models;
using WsprPc.Services.Ai.Local;

namespace WsprPc;

public partial class PromptEditorWindow : Window
{
    private const int DefaultLocalAiMaxTokens = 384;
    private const double DefaultLocalAiTemperature = 0.2;
    private const int DefaultLocalAiContextSize = 0;
    private const int DefaultLocalAiTimeoutSeconds = 120;
    private const int DefaultLocalAiGpuLayers = -1;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private bool _darkMode;
    private readonly string _localAiModelDir;
    private readonly List<LocalAiModelPreset> _localAiPresets;
    private readonly LocalModelDownloader _localModelDownloader = new();
    private CancellationTokenSource? _localModelDownloadCts;
    private LocalAiModelPreset? _selectedLocalAiPreset;
    private bool _syncingThinking;

    public PromptDefinition Result { get; private set; }

    private static readonly string[] OpenAiModels =
    [
        "gpt-5.1",
        "gpt-5",
        "gpt-5-mini",
        "gpt-5-nano",
        "gpt-4.1",
        "gpt-4.1-mini"
    ];

    private static readonly string[] ReasoningLevels =
    [
        "minimal",
        "low",
        "medium",
        "high",
        "none"
    ];

    public PromptEditorWindow(string localAiModelDir, IReadOnlyList<LocalAiModelPreset> localAiPresets, PromptDefinition? existing = null)
    {
        InitializeComponent();
        _localAiModelDir = localAiModelDir;
        _localAiPresets = localAiPresets
            .Select(p => new LocalAiModelPreset(p.Id, p.DisplayName, p.Url, p.FileName, p.Subfolder, p.Note, p.Sha256))
            .ToList();

        ProviderCombo.ItemsSource = Enum.GetNames(typeof(AiProvider)).ToList();
        ProviderCombo.SelectedIndex = 0;

        OpenAiModelCombo.ItemsSource = OpenAiModels;
        OpenAiReasoningCombo.ItemsSource = ReasoningLevels;

        if (existing != null)
        {
            Result = existing;
            TitleBox.Text = existing.Title;
            SystemBox.Text = existing.SystemInstruction;
            UserBox.Text = existing.UserInstruction;
            UseMemoryCheck.IsChecked = existing.UseMemory;
            UseClipboardCheck.IsChecked = existing.UseClipboard;
            GeminiModelBox.Text = existing.GeminiModel;
            GeminiThinkingCheck.IsChecked = existing.GeminiUseThinking;
            LocalThinkingCheck.IsChecked = existing.GeminiUseThinking;
            GeminiGroundingCheck.IsChecked = existing.GeminiUseGrounding;
            IsMailPromptCheck.IsChecked = existing.IsMailPrompt;
            if (existing.IsMailPrompt)
            {
                GeminiThinkingCheck.IsChecked = true;
                GeminiThinkingCheck.IsEnabled = false;
                GeminiGroundingCheck.IsChecked = true;
                GeminiGroundingCheck.IsEnabled = false;
            }
            OpenAiModelBox.Text = existing.OpenAiModel;
            GeminiGroundingCheck.IsChecked = existing.GeminiUseGrounding;
            OpenAiModelBox.Text = existing.OpenAiModel;
            OpenAiReasoningCombo.SelectedItem = existing.OpenAiReasoningEffort;
            ProviderCombo.SelectedItem = existing.Provider.ToString();
            SendToWebhookCheck.IsChecked = existing.SendToWebhook;
            WebhookUrlBox.Text = existing.WebhookUrl;
            WebhookTokenBox.Text = existing.WebhookToken;
            SendRawTextCheck.IsChecked = existing.SendRawText;
            InitializeLocalAiModelSelection(existing.LocalAiModelId, existing.LocalAiModelPath);
            LocalAiMaxTokensBox.Text = (existing.LocalAiMaxTokens ?? DefaultLocalAiMaxTokens).ToString();
            LocalAiTemperatureBox.Text = (existing.LocalAiTemperature ?? DefaultLocalAiTemperature).ToString(CultureInfo.InvariantCulture);
            LocalAiContextSizeBox.Text = (existing.LocalAiContextSize ?? DefaultLocalAiContextSize).ToString();
            LocalAiTimeoutBox.Text = (existing.LocalAiTimeoutSeconds ?? DefaultLocalAiTimeoutSeconds).ToString();
            LocalAiGpuLayersBox.Text = (existing.LocalAiGpuLayers ?? DefaultLocalAiGpuLayers).ToString();
        }
        else
        {
            Result = new PromptDefinition();
            GeminiModelBox.Text = Result.GeminiModel;
            OpenAiModelBox.Text = Result.OpenAiModel;
            OpenAiReasoningCombo.SelectedItem = Result.OpenAiReasoningEffort;
            LocalThinkingCheck.IsChecked = Result.GeminiUseThinking;
            InitializeLocalAiModelSelection(null, null);
            LocalAiMaxTokensBox.Text = DefaultLocalAiMaxTokens.ToString();
            LocalAiTemperatureBox.Text = DefaultLocalAiTemperature.ToString(CultureInfo.InvariantCulture);
            LocalAiContextSizeBox.Text = DefaultLocalAiContextSize.ToString();
            LocalAiTimeoutBox.Text = DefaultLocalAiTimeoutSeconds.ToString();
            LocalAiGpuLayersBox.Text = DefaultLocalAiGpuLayers.ToString();
        }

        if (string.IsNullOrWhiteSpace(OpenAiModelBox.Text))
            OpenAiModelBox.Text = "gpt-5-mini";

        OpenAiModelCombo.SelectionChanged += (_, _) =>
        {
            if (OpenAiModelCombo.SelectedItem is string model)
                OpenAiModelBox.Text = model;
            UpdateOpenAiWarning();
        };

        OpenAiModelBox.TextChanged += (_, _) => UpdateOpenAiWarning();
        OpenAiReasoningCombo.SelectionChanged += (_, _) => UpdateOpenAiWarning();

        ProviderCombo.SelectionChanged += (_, _) => UpdateProviderVisibility();
        UpdateOpenAiWarning();
        UpdateProviderVisibility();
        LocalAiModelPresetCombo.SelectionChanged += async (_, _) => await OnLocalAiPresetChangedAsync();
        CancelLocalAiDownloadButton.Click += (_, _) => _localModelDownloadCts?.Cancel();
        RetryLocalAiDownloadButton.Click += async (_, _) => await DownloadSelectedLocalAiModelAsync();
        GeminiThinkingCheck.Checked += (_, _) => SyncThinkingFromGemini(true);
        GeminiThinkingCheck.Unchecked += (_, _) => SyncThinkingFromGemini(false);
        LocalThinkingCheck.Checked += (_, _) => SyncThinkingFromLocal(true);
        LocalThinkingCheck.Unchecked += (_, _) => SyncThinkingFromLocal(false);

        SendToWebhookCheck.Checked += (_, _) => UpdateWebhookVisibility();
        SendToWebhookCheck.Unchecked += (_, _) => UpdateWebhookVisibility();
        SendRawTextCheck.Checked += (_, _) => UpdateProviderVisibility();
        SendRawTextCheck.Unchecked += (_, _) => UpdateProviderVisibility();
        UpdateWebhookVisibility();
        
        // Mail prompt logic
        IsMailPromptCheck.Checked += (_, _) =>
        {
            // Auto-enable and lock Thinking + Grounding
            GeminiThinkingCheck.IsChecked = true;
            GeminiThinkingCheck.IsEnabled = false;
            GeminiGroundingCheck.IsChecked = true;
            GeminiGroundingCheck.IsEnabled = false;
        };
        
        IsMailPromptCheck.Unchecked += (_, _) =>
        {
            // Unlock controls (keep them checked or not is up to user, but let's unlock)
            GeminiThinkingCheck.IsEnabled = true;
            GeminiGroundingCheck.IsEnabled = true;
        };

        OkButton.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(TitleBox.Text))
            {
                WpfMessageBox.Show("Ange en titel.");
                return;
            }

            Result.Title = TitleBox.Text.Trim();
            Result.SystemInstruction = SystemBox.Text.Trim();
            Result.UserInstruction = UserBox.Text.Trim();
            Result.UseMemory = UseMemoryCheck.IsChecked == true;
            Result.UseClipboard = UseClipboardCheck.IsChecked == true;
            Result.GeminiModel = string.IsNullOrWhiteSpace(GeminiModelBox.Text)
                ? "models/gemini-flash-latest"
                : GeminiModelBox.Text.Trim();
            Result.GeminiUseThinking = (Enum.TryParse(ProviderCombo.SelectedItem?.ToString(), out AiProvider p) && p == AiProvider.LocalQwen)
                ? LocalThinkingCheck.IsChecked == true
                : GeminiThinkingCheck.IsChecked == true;
            Result.GeminiUseGrounding = GeminiGroundingCheck.IsChecked == true;
            Result.IsMailPrompt = IsMailPromptCheck.IsChecked == true;
            Result.OpenAiModel = string.IsNullOrWhiteSpace(OpenAiModelBox.Text)
                ? "gpt-5-mini"
                : OpenAiModelBox.Text.Trim();
            Result.OpenAiReasoningEffort = OpenAiReasoningCombo.SelectedItem?.ToString() ?? "minimal";

            Result.OpenAiReasoningEffort = OpenAiReasoningCombo.SelectedItem?.ToString() ?? "minimal";
            Result.LocalAiModelId = _selectedLocalAiPreset?.Id;
            Result.LocalAiModelPath = ResolveLocalAiModelPath(_selectedLocalAiPreset);
            Result.LocalAiMaxTokens = ParseInt(LocalAiMaxTokensBox.Text, DefaultLocalAiMaxTokens, min: 16);
            Result.LocalAiTemperature = ParseDouble(LocalAiTemperatureBox.Text, DefaultLocalAiTemperature, min: 0, max: 2);
            Result.LocalAiContextSize = ParseInt(LocalAiContextSizeBox.Text, DefaultLocalAiContextSize, min: 0);
            Result.LocalAiTimeoutSeconds = ParseInt(LocalAiTimeoutBox.Text, DefaultLocalAiTimeoutSeconds, min: 10);
            Result.LocalAiGpuLayers = ParseInt(LocalAiGpuLayersBox.Text, DefaultLocalAiGpuLayers, min: -1);

            // Webhook
            Result.SendToWebhook = SendToWebhookCheck.IsChecked == true;
            Result.WebhookUrl = WebhookUrlBox.Text.Trim();
            Result.WebhookToken = WebhookTokenBox.Text.Trim();
            Result.SendRawText = SendRawTextCheck.IsChecked == true;

            if (Enum.TryParse(ProviderCombo.SelectedItem?.ToString(), out AiProvider provider))
                Result.Provider = provider;

            DialogResult = true;
        };

        CancelButton.Click += (_, _) =>
        {
            DialogResult = false;
        };

        // Sync theme with owner
        Loaded += (_, _) =>
        {
            // Owner is set after constructor, check it in Loaded
            if (this.Owner is MainWindow mw)
            {
                _darkMode = mw.DarkModeToggle.IsChecked == true;
            }
            ApplyTitleBarTheme(_darkMode);
        };
    }

    private void ApplyTitleBarTheme(bool dark)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        int value = dark ? 1 : 0;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
    }

    private void UpdateOpenAiWarning()
    {
        string model = OpenAiModelBox.Text.Trim();
        string effort = OpenAiReasoningCombo.SelectedItem?.ToString() ?? "";

        bool known = OpenAiModels.Contains(model, StringComparer.OrdinalIgnoreCase);
        bool noneEffort = string.Equals(effort, "none", StringComparison.OrdinalIgnoreCase);
        bool gpt5Family = model.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase);

        if (!known)
        {
            OpenAiWarningText.Text = "Okänt modellnamn. Kontrollera att modellen finns i ditt konto.";
            return;
        }

        if (noneEffort && gpt5Family)
        {
            OpenAiWarningText.Text = "Den här modellen kan neka reasoning='none'. Prova 'minimal' om det misslyckas.";
            return;
        }

        OpenAiWarningText.Text = "";
    }

    private void UpdateWebhookVisibility()
    {
        bool useWebhook = SendToWebhookCheck.IsChecked == true;
        WebhookSettingsPanel.Visibility = useWebhook ? Visibility.Visible : Visibility.Collapsed;
        UpdateProviderVisibility();
    }

    private void InitializeLocalAiModelSelection(string? selectedModelId, string? selectedModelPath)
    {
        foreach (var preset in _localAiPresets)
        {
            preset.IsDownloaded = ResolveLocalAiModelPath(preset) != null;
        }

        LocalAiModelPresetCombo.ItemsSource = _localAiPresets;

        LocalAiModelPreset? selected = null;
        if (!string.IsNullOrWhiteSpace(selectedModelId))
        {
            selected = _localAiPresets.FirstOrDefault(p =>
                string.Equals(p.Id, selectedModelId, StringComparison.OrdinalIgnoreCase));
        }

        if (selected == null && !string.IsNullOrWhiteSpace(selectedModelPath))
        {
            selected = _localAiPresets.FirstOrDefault(p =>
                selectedModelPath.Contains(Path.Combine(p.Subfolder, p.FileName), StringComparison.OrdinalIgnoreCase));
        }

        selected ??= _localAiPresets.FirstOrDefault(p => p.IsDownloaded) ?? _localAiPresets.FirstOrDefault();
        if (selected == null)
            return;

        _selectedLocalAiPreset = selected;
        LocalAiModelPresetCombo.SelectedItem = selected;
        LocalAiPresetNoteText.Text = selected.Note;
        UpdateLocalAiDownloadUi(selected.IsDownloaded, "Väntar", false, false);
        if (!selected.IsDownloaded)
            _ = DownloadSelectedLocalAiModelAsync();
    }

    private async Task OnLocalAiPresetChangedAsync()
    {
        if (LocalAiModelPresetCombo.SelectedItem is not LocalAiModelPreset preset)
            return;

        _selectedLocalAiPreset = preset;
        LocalAiPresetNoteText.Text = preset.Note;
        bool downloaded = ResolveLocalAiModelPath(preset) != null;
        UpdateLocalAiDownloadUi(downloaded, downloaded ? "Väntar" : "Förbereder nedladdning...", false, false);
        if (!downloaded)
            await DownloadSelectedLocalAiModelAsync();
    }

    private async Task DownloadSelectedLocalAiModelAsync()
    {
        if (_selectedLocalAiPreset == null)
            return;

        string targetDir = Path.Combine(_localAiModelDir, _selectedLocalAiPreset.Subfolder);
        Directory.CreateDirectory(targetDir);
        string targetPath = Path.Combine(targetDir, _selectedLocalAiPreset.FileName);

        try
        {
            _localModelDownloadCts?.Cancel();
            _localModelDownloadCts = new CancellationTokenSource();
            LocalAiDownloadProgressBar.IsIndeterminate = true;
            LocalAiDownloadStatusText.Text = "Laddar ner lokal AI-modell...";
            UpdateLocalAiDownloadUi(isDownloaded: false, LocalAiDownloadStatusText.Text, isDownloading: true, showRetry: false);

            var progress = new Progress<int>(pct =>
            {
                if (LocalAiDownloadProgressBar.IsIndeterminate)
                    LocalAiDownloadProgressBar.IsIndeterminate = false;
                LocalAiDownloadProgressBar.Value = pct;
                LocalAiDownloadStatusText.Text = $"Laddar ner lokal AI-modell... {pct}%";
            });

            await _localModelDownloader.DownloadAsync(_selectedLocalAiPreset.Url, targetPath, progress, _localModelDownloadCts.Token);

            if (!string.IsNullOrWhiteSpace(_selectedLocalAiPreset.Sha256)
                && !LocalModelDownloader.VerifySha256(targetPath, _selectedLocalAiPreset.Sha256))
            {
                throw new InvalidOperationException("Checksum-verifiering misslyckades.");
            }

            _selectedLocalAiPreset.IsDownloaded = true;
            LocalAiModelPresetCombo.Items.Refresh();
            UpdateLocalAiDownloadUi(isDownloaded: true, $"Nedladdad: {targetPath}", isDownloading: false, showRetry: false);
        }
        catch (OperationCanceledException)
        {
            UpdateLocalAiDownloadUi(isDownloaded: false, "Nedladdning avbruten.", isDownloading: false, showRetry: true);
        }
        catch (Exception ex)
        {
            UpdateLocalAiDownloadUi(isDownloaded: false, $"Nedladdning misslyckades: {ex.Message}", isDownloading: false, showRetry: true);
        }
        finally
        {
            LocalAiDownloadProgressBar.IsIndeterminate = false;
        }
    }

    private void UpdateLocalAiDownloadUi(bool isDownloaded, string status, bool isDownloading, bool showRetry)
    {
        LocalAiDownloadStatusText.Text = status;
        LocalAiDownloadPanel.Visibility = isDownloaded ? Visibility.Collapsed : Visibility.Visible;
        CancelLocalAiDownloadButton.Visibility = isDownloading ? Visibility.Visible : Visibility.Collapsed;
        CancelLocalAiDownloadButton.IsEnabled = isDownloading;
        RetryLocalAiDownloadButton.Visibility = showRetry ? Visibility.Visible : Visibility.Collapsed;
        RetryLocalAiDownloadButton.IsEnabled = showRetry;
    }

    private string? ResolveLocalAiModelPath(LocalAiModelPreset? preset)
    {
        if (preset == null)
            return null;

        string candidate = Path.Combine(_localAiModelDir, preset.Subfolder, preset.FileName);
        return File.Exists(candidate) ? candidate : null;
    }

    private void UpdateProviderVisibility()
    {
        if (!Enum.TryParse(ProviderCombo.SelectedItem?.ToString(), out AiProvider provider))
            provider = AiProvider.Gemini;

        bool sendRaw = SendToWebhookCheck.IsChecked == true && SendRawTextCheck.IsChecked == true;
        
        if (sendRaw)
        {
            GeminiSettingsPanel.Visibility = Visibility.Collapsed;
            OpenAiSettingsPanel.Visibility = Visibility.Collapsed;
            OpenAiReasoningPanel.Visibility = Visibility.Collapsed;
            OpenAiWarningText.Visibility = Visibility.Collapsed;
            InstructionsPanel.Visibility = Visibility.Collapsed;
            return;
        }

        InstructionsPanel.Visibility = Visibility.Visible;
        bool isGemini = provider == AiProvider.Gemini;
        bool isOpenAi = provider == AiProvider.OpenAI;
        bool isLocal = provider == AiProvider.LocalQwen;

        GeminiSettingsPanel.Visibility = isGemini ? Visibility.Visible : Visibility.Collapsed;
        OpenAiSettingsPanel.Visibility = isOpenAi ? Visibility.Visible : Visibility.Collapsed;
        OpenAiReasoningPanel.Visibility = isOpenAi ? Visibility.Visible : Visibility.Collapsed;
        OpenAiWarningText.Visibility = isOpenAi ? Visibility.Visible : Visibility.Collapsed;
        LocalProviderExpander.Visibility = isLocal ? Visibility.Visible : Visibility.Collapsed;
    }

    private static int ParseInt(string? value, int fallback, int min)
    {
        if (!int.TryParse(value, out int parsed))
            return fallback;
        return Math.Max(min, parsed);
    }

    private static double ParseDouble(string? value, double fallback, double min, double max)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
            && !double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed))
            return fallback;

        if (parsed < min) return min;
        if (parsed > max) return max;
        return parsed;
    }

    private void SyncThinkingFromGemini(bool value)
    {
        if (_syncingThinking) return;
        _syncingThinking = true;
        LocalThinkingCheck.IsChecked = value;
        _syncingThinking = false;
    }

    private void SyncThinkingFromLocal(bool value)
    {
        if (_syncingThinking) return;
        _syncingThinking = true;
        GeminiThinkingCheck.IsChecked = value;
        _syncingThinking = false;
    }
}
