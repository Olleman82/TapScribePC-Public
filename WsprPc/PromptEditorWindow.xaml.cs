using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using WpfMessageBox = System.Windows.MessageBox;
using WsprPc.Models;

namespace WsprPc;

public partial class PromptEditorWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private bool _darkMode;

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

    public PromptEditorWindow(PromptDefinition? existing = null)
    {
        InitializeComponent();

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
        }
        else
        {
            Result = new PromptDefinition();
            GeminiModelBox.Text = Result.GeminiModel;
            OpenAiModelBox.Text = Result.OpenAiModel;
            OpenAiReasoningCombo.SelectedItem = Result.OpenAiReasoningEffort;
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
            Result.GeminiUseThinking = GeminiThinkingCheck.IsChecked == true;
            Result.GeminiUseGrounding = GeminiGroundingCheck.IsChecked == true;
            Result.IsMailPrompt = IsMailPromptCheck.IsChecked == true;
            Result.OpenAiModel = string.IsNullOrWhiteSpace(OpenAiModelBox.Text)
                ? "gpt-5-mini"
                : OpenAiModelBox.Text.Trim();
            Result.OpenAiReasoningEffort = OpenAiReasoningCombo.SelectedItem?.ToString() ?? "minimal";

            Result.OpenAiReasoningEffort = OpenAiReasoningCombo.SelectedItem?.ToString() ?? "minimal";

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
        GeminiSettingsPanel.Visibility = isGemini ? Visibility.Visible : Visibility.Collapsed;
        OpenAiSettingsPanel.Visibility = isGemini ? Visibility.Collapsed : Visibility.Visible;
        OpenAiReasoningPanel.Visibility = isGemini ? Visibility.Collapsed : Visibility.Visible;
        OpenAiWarningText.Visibility = isGemini ? Visibility.Collapsed : Visibility.Visible;
    }
}
