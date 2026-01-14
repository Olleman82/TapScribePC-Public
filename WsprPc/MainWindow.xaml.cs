using System;
using System.Windows.Threading;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using WpfMessageBox = System.Windows.MessageBox;
using WsprPc.Models;
using WsprPc.Services;
using WsprPc.Services.Ai;
using WsprPc.Services.Vad;
using WsprPc.Services.Diarization;
using WsprPc.Stores;

namespace WsprPc;

public partial class MainWindow : Window
{
    private GlobalKeyHoldService? _directHotkey;
    private GlobalKeyHoldService? _aiHotkey;
    private readonly DictationController _controller;
    private readonly WhisperNetEngine _engine;
    private readonly HttpClient _httpClient = new();
    private readonly GeminiClient _geminiClient = new();
    private readonly OpenAiClient _openAiClient = new();
    private CancellationTokenSource? _downloadCts;
    private AppConfig _config = new();
    private readonly string _configPath;
    private AppLogger? _logger;
    private readonly PromptStore _promptStore;
    private readonly MemoryStore _memoryStore;
    private List<PromptDefinition> _prompts = new();
    private List<MemoryItem> _memory = new();
    private PromptDefinition? _defaultPrompt;
    private IntPtr _targetWindow;
    private bool _suppressModelSelectionChanged;
    private string? _envPath;
    private List<ModelPreset> _presets = new();
    private ModelPreset? _downloadPreset;
    private TrayIconService? _trayIcon;
    private string? _updateDownloadUrl;
    private readonly System.Text.StringBuilder _currentSessionText = new();     
    private CancellationTokenSource? _autoTuneCts;
    private bool _autoTuneRunning;
    private string _currentStatus = "Väntar";
    private bool _isInitialized = false;
    private bool _isStartingUp = false;
    private bool _welcomeShowing = false;
    private string? _clipboardContext;
    
    // File Transcription (Diarization) fields
    private FileTranscriptionService? _fileTranscriptionService;
    private string? _selectedAudioFilePath;
    private CancellationTokenSource? _fileTranscriptionCts;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public MainWindow()
    {
        InitializeComponent();

        var audio = new AudioCaptureService();
        var polisher = new TextPolisher();
        var paster = new PasteInjector();
        _engine = new WhisperNetEngine();

        string dataDir = GetAppDataDir();
        _configPath = Path.Combine(dataDir, "appsettings.json");
        _config = LoadConfigWithMigration(_configPath);
        NormalizeConfigPaths(dataDir);

        if (_config.AllowEnvKeys)
        {
            _envPath = FindEnvPath();
            var env = EnvLoader.Load(_envPath);
            if (string.IsNullOrWhiteSpace(_config.GeminiApiKey) && env.TryGetValue("GEMINI_API_KEY", out var geminiKey))
                _config.GeminiApiKey = geminiKey;
            if (string.IsNullOrWhiteSpace(_config.OpenAiApiKey) && env.TryGetValue("OPENAI_API_KEY", out var openAiKey))
                _config.OpenAiApiKey = openAiKey;
        }

        _engine.ModelPath = _config.ModelPath;
        _engine.BeamSize = 2;
        _engine.CpuThreads = CalculateCpuThreads();
        _engine.Language = "sv";
        string logDir = _config.LogDir ?? Path.Combine(dataDir, "logs");
        Directory.CreateDirectory(logDir);
        _logger = new AppLogger(Path.Combine(logDir, "app.log"));
        _logger.Info("App start");
        _logger.Info($"ConfigPath={_configPath}");
        _logger.Info($"LogDir={logDir}");
        _logger.Info($"ModelPath={_config.ModelPath}");
        _logger.Info("Engine=WhisperNet");
        _logger.Info($"EnvPath={_envPath ?? "(avstängt)"}");
        _logger.Info($"EnvKeys: gemini={(string.IsNullOrWhiteSpace(_config.GeminiApiKey) ? "saknas" : "ok")}, openai={(string.IsNullOrWhiteSpace(_config.OpenAiApiKey) ? "saknas" : "ok")}");
        _logger.Info($"WhisperThreads={CalculateCpuThreads()}, BeamSize=2");

        _controller = new DictationController(audio, _engine, polisher, paster);
        _controller.MaxSegmentWorkers = CalculateSegmentWorkers();
        _controller.VadSpeechThreshold = (float)_config.SilenceThreshold;
        _controller.VadMinSilenceMs = Math.Max(100, (int)Math.Round(_config.SilenceDurationSeconds * 1000));
        _controller.EnableSilenceChunking = _config.EnableVad;
        _controller.UseModelVad = true;
        _controller.MaxSegmentSeconds = 20.0;
        _controller.VadModelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "silero_vad.onnx");
        _logger.Info($"Model VAD={(File.Exists(_controller.VadModelPath) ? "på" : "saknas")} Enabled={_config.EnableVad} Path={_controller.VadModelPath}");
        if (File.Exists(_controller.VadModelPath))
        {
            try
            {
                using var warmup = new WsprPc.Services.Vad.SileroVadModel(_controller.VadModelPath, audio.SampleRate);
                warmup.Reset();
                _logger.Info("VAD warm-up: ok");
            }
            catch (Exception ex)
            {
                _logger.Info($"VAD warm-up failed: {ex.GetType().Name} {ex.Message}");
            }
        }
        _controller.PartialResultUpdated += OnPartialResultUpdated;
        _controller.FirstResultReady += elapsed =>
        {
            _logger?.Info($"Delresultat efter {elapsed.TotalMilliseconds:0} ms");
        };
        _controller.SegmentError += ex =>
        {
            _logger?.Error("Deltranskribering misslyckades", ex);
        };
        _controller.ChunkerDiagnostics += message =>
        {
            _logger?.Info(message);
        };

        _promptStore = new PromptStore(Path.Combine(dataDir, "prompts.json"));
        _memoryStore = new MemoryStore(Path.Combine(dataDir, "memory.json"));

        InitializeDownloadUi();
        InitializeModelSelector();
        AutoSelectModelIfUnset();
        InitializePromptSystem();
        InitializeSettingsUi();
        InitializeAutoTuneUi();
        InitializeTrayIcon();

        DownloadModelButton.Click += async (_, _) => await DownloadSelectedModelAsync();
        AboutButton.Click += (_, _) => ShowAbout();
        PromptInfoButton.Click += (_, _) => ShowPromptInfo();
        MemoryInfoButton.Click += (_, _) => ShowMemoryInfo();
        FaqButton.Click += (_, _) => { var w = new FaqWindow { Owner = this }; w.ShowDialog(); };

        AddPromptButton.Click += (_, _) => AddPrompt();
        EditPromptButton.Click += (_, _) => EditSelectedPrompt();
        DeletePromptButton.Click += (_, _) => DeleteSelectedPrompt();
        SetDefaultPromptButton.Click += (_, _) => SetDefaultPrompt();
        ClearDefaultPromptButton.Click += (_, _) => ClearDefaultPrompt();
        AddMemoryButton.Click += (_, _) => AddMemoryItem();
        EditMemoryButton.Click += (_, _) => EditMemoryItem();
        DeleteMemoryButton.Click += (_, _) => DeleteMemoryItem();
        SaveSettingsButton.Click += (_, _) => SaveSettings();
        UpdateBannerButton.Click += (_, _) => OpenUpdateUrl();
        AutoTuneButton.Click += async (_, _) => await RunAutoTuneAsync(true);
        DarkModeToggle.Checked += (_, _) => ApplyTheme(true);
        DarkModeToggle.Unchecked += (_, _) => ApplyTheme(false);
        DarkModeToggle.IsChecked = _config.DarkMode;
        if (_config.DarkMode) ApplyTheme(true);
        
        // File Transcription (Filer) tab event handlers
        SelectAudioFileButton.Click += (_, _) => SelectAudioFile();
        StartFileTranscriptionButton.Click += async (_, _) => await StartFileTranscriptionAsync();
        CancelFileTranscriptionButton.Click += (_, _) => CancelFileTranscription();
        CopyFileTranscriptionButton.Click += (_, _) => CopyFileTranscriptionResult();
        SaveFileTranscriptionButton.Click += (_, _) => SaveFileTranscriptionResult();
        DownloadDiarizationModelsButton.Click += async (_, _) => await DownloadDiarizationModelsAsync();
        
        // Initialize file transcription service
        InitializeFileTranscriptionService();

        Loaded += (_, _) => ApplyTitleBarTheme(_config.DarkMode);
        ContentRendered += (_, _) => _ = InitializeStartupFlowAsync();

        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
                _trayIcon?.ShowBalloon("TapScribe PC", "Appen körs nu i systemfältet.");
            }
        };

        Closed += (_, _) =>
        {
            _directHotkey?.Dispose();
            _aiHotkey?.Dispose();
            _controller.Dispose();
            _downloadCts?.Cancel();
            _autoTuneCts?.Cancel();
            _httpClient.Dispose();
            _trayIcon?.Dispose();
            _logger?.Info("App closed");
            System.Windows.Application.Current.Shutdown();
        };

        _isInitialized = true;
        _ = CheckForUpdatesAsync();
    }

    private async Task InitializeStartupFlowAsync()
    {
        if (_isStartingUp) return;
        _isStartingUp = true;

        // Give WPF a small breath to ensure window handles are stable
        await Task.Delay(50);

        try
        {
            await ShowWelcomeIfNeededAsync();
            await StartAutoTuneIfNeededAsync();
        }
        catch (Exception ex)
        {
            _logger?.Error("Startup flow error", ex);
        }
        finally
        {
            _isStartingUp = false;
            _isInitialized = true;
            _ = CheckForUpdatesAsync();
        }
    }

    private void SetInitStatus(string? message, bool visible)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => SetInitStatus(message, visible));
            return;
        }

        InitStatusBanner.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        if (visible && message != null)
        {
            InitStatusText.Text = message;
        }
    }

    private void NormalizeConfigPaths(string dataDir)
{
    string fallbackLogDir = Path.Combine(dataDir, "logs");
    string fallbackModelDir = Path.Combine(dataDir, "models");
    bool updated = false;

    if (string.IsNullOrWhiteSpace(_config.LogDir) || !IsUsablePath(_config.LogDir))
    {
        _config.LogDir = fallbackLogDir;
        updated = true;
    }
    else
    {
        try
        {
            Directory.CreateDirectory(_config.LogDir);
        }
        catch
        {
            _config.LogDir = fallbackLogDir;
            updated = true;
        }
    }

    if (string.IsNullOrWhiteSpace(_config.ModelDir) || !IsUsablePath(_config.ModelDir))
    {
        _config.ModelDir = fallbackModelDir;
        updated = true;
    }

    if (!string.IsNullOrWhiteSpace(_config.ModelDir))
    {
        try
        {
            Directory.CreateDirectory(_config.ModelDir);
        }
        catch
        {
            _config.ModelDir = fallbackModelDir;
            updated = true;
        }
    }

    if (!string.IsNullOrWhiteSpace(_config.ModelPath) && !File.Exists(_config.ModelPath))
    {
        _config.ModelPath = null;
        _config.SelectedModel = null;
        updated = true;
    }

    if (!string.IsNullOrWhiteSpace(_config.WhisperCliPath) && !File.Exists(_config.WhisperCliPath))
    {
        _config.WhisperCliPath = null;
        updated = true;
    }

    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
    string defaultCli = Path.Combine(baseDir, "whisper-cli.exe");
    if (string.IsNullOrWhiteSpace(_config.WhisperCliPath) && File.Exists(defaultCli))
    {
        _config.WhisperCliPath = defaultCli;
        updated = true;
    }

    if (updated)
    {
        try
        {
            _config.Save(_configPath);
        }
        catch
        {
            // Ignore save failures.
        }
    }
}

private static bool IsUsablePath(string path)
    {
        try
        {
            if (!Path.IsPathRooted(path))
                return true;

            string root = Path.GetPathRoot(path) ?? string.Empty;
            if (root.StartsWith(@"\\", StringComparison.Ordinal))
                return true;

            if (root.Length >= 2 && root[1] == ':')
            {
                string drive = root.Substring(0, 2);
                return DriveInfo.GetDrives()
                    .Any(d => string.Equals(d.Name.TrimEnd('\\'), drive, StringComparison.OrdinalIgnoreCase));
            }
        }
        catch
        {
            return false;
        }

        return true;
    }

    private void InitializeSettingsUi()
    {
        var keyNames = Enum.GetNames(typeof(VirtualKey)).ToList();
        DirectHotkeyCombo.ItemsSource = keyNames;
        AiHotkeyCombo.ItemsSource = keyNames;

        DirectHotkeyCombo.SelectedItem = _config.DirectHotkey;
        AiHotkeyCombo.SelectedItem = _config.AiHotkey;

        GeminiApiKeyBox.Password = _config.GeminiApiKey ?? string.Empty;        
        OpenAiApiKeyBox.Password = _config.OpenAiApiKey ?? string.Empty;        
        UseDefaultPromptCheckBox.IsChecked = _config.AiUseDefaultPrompt;
        UseAutoPromptCheckBox.IsChecked = _config.AiUseAutoPrompt;
        AutoPasteCheckBox.IsChecked = _config.AutoPasteEnabled;
        StartWithWindowsCheckBox.IsChecked = _config.StartWithWindows;
        SilenceThresholdTextBox.Text = _config.SilenceThreshold.ToString("0.000", CultureInfo.CurrentCulture);
        SilenceDurationTextBox.Text = _config.SilenceDurationSeconds.ToString("0.0", CultureInfo.CurrentCulture);
        EnableVadCheckBox.IsChecked = _config.EnableVad;
        ManualThreadsTextBox.Text = _config.ManualThreads?.ToString() ?? string.Empty;

        RegisterHotkeys();
        UpdateHotkeyLabel();
        UpdateDefaultPromptLabel();
    }

    private void RegisterHotkeys()
    {
        _directHotkey?.Dispose();
        _aiHotkey?.Dispose();

        if (!Enum.TryParse(_config.DirectHotkey, true, out VirtualKey direct))
            direct = VirtualKey.F8;
        if (!Enum.TryParse(_config.AiHotkey, true, out VirtualKey ai))
            ai = VirtualKey.F9;

        _directHotkey = new GlobalKeyHoldService(direct);
        _directHotkey.KeyDown += () => Dispatcher.InvokeAsync(OnDirectHotkeyDown);
        _directHotkey.KeyUp += () => Dispatcher.InvokeAsync(OnDirectHotkeyUp);
        _directHotkey.Diagnostic += message => _logger?.Info($"DirectHotkey: {message}");
        _directHotkey.Start();

        _aiHotkey = new GlobalKeyHoldService(ai);
        _aiHotkey.KeyDown += () => Dispatcher.InvokeAsync(OnAiHotkeyDown);
        _aiHotkey.KeyUp += () => Dispatcher.InvokeAsync(OnAiHotkeyUp);
        _aiHotkey.Diagnostic += message => _logger?.Info($"AiHotkey: {message}");
        _aiHotkey.Start();
    }

    private void UpdateHotkeyLabel()
    {
        HotkeyText.Text = $"Direkt: {_config.DirectHotkey} • AI: {_config.AiHotkey}";
        AiSectionTitle.Text = $"AI-bearbetning ({_config.AiHotkey})";
    }

    private void InitializePromptSystem()
    {
        _prompts = _promptStore.Load();
        _memory = _memoryStore.Load();

        bool promptChanged = false;
        if (_prompts.Count == 0)
        {
            _prompts.Add(new PromptDefinition
            {
                Title = "Sammanfattning",
                SystemInstruction = "Sammanfatta texten i punktform.",
                Provider = AiProvider.Gemini
            });
            _promptStore.Save(_prompts);
        }
        else
        {
            foreach (var prompt in _prompts)
            {
                if (string.Equals(prompt.Title, "Summary", StringComparison.OrdinalIgnoreCase))
                {
                    prompt.Title = "Sammanfattning";
                    promptChanged = true;
                }
                if (string.Equals(prompt.SystemInstruction, "Summarize the text in bullet points.", StringComparison.OrdinalIgnoreCase))
                {
                    prompt.SystemInstruction = "Sammanfatta texten i punktform.";
                    promptChanged = true;
                }
            }
            if (promptChanged)
                _promptStore.Save(_prompts);
        }

        if (!string.IsNullOrWhiteSpace(_config.DefaultPromptId))
        {
            _defaultPrompt = _prompts.FirstOrDefault(p => p.Id == _config.DefaultPromptId);
        }

        RefreshPromptList();
        RefreshMemoryList();
    }

    private void RefreshPromptList()
    {
        PromptListBox.ItemsSource = null;
        PromptListBox.ItemsSource = _prompts;
        PromptListBox.DisplayMemberPath = nameof(PromptDefinition.Title);
    }

    private void RefreshMemoryList()
    {
        MemoryListBox.ItemsSource = null;
        MemoryListBox.ItemsSource = _memory;
        MemoryListBox.DisplayMemberPath = nameof(MemoryItem.Title);
    }

    private void AddPrompt()
    {
        var dialog = new PromptEditorWindow();
        dialog.Owner = this;
        if (dialog.ShowDialog() == true)
        {
            _prompts.Add(dialog.Result);
            _promptStore.Save(_prompts);
            RefreshPromptList();
        }
    }

    private void EditSelectedPrompt()
    {
        if (PromptListBox.SelectedItem is not PromptDefinition prompt)
            return;

        var dialog = new PromptEditorWindow(prompt);
        dialog.Owner = this;
        if (dialog.ShowDialog() == true)
        {
            _promptStore.Save(_prompts);
            RefreshPromptList();
            UpdateDefaultPromptLabel();
        }
    }

    private void DeleteSelectedPrompt()
    {
        if (PromptListBox.SelectedItem is not PromptDefinition prompt)
            return;

        if (WpfMessageBox.Show("Ta bort prompt?", "Bekräfta", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        _prompts.Remove(prompt);
        if (_defaultPrompt?.Id == prompt.Id)
        {
            _defaultPrompt = null;
            _config.DefaultPromptId = null;
        }
        _promptStore.Save(_prompts);
        _config.Save(_configPath);
        RefreshPromptList();
        UpdateDefaultPromptLabel();
    }

    private void AddMemoryItem()
    {
        var dialog = new MemoryEditorWindow();
        dialog.Owner = this;
        if (dialog.ShowDialog() == true)
        {
            _memory.Add(dialog.Result);
            _memoryStore.Save(_memory);
            RefreshMemoryList();
        }
    }

    private void EditMemoryItem()
    {
        if (MemoryListBox.SelectedItem is not MemoryItem item)
            return;

        var dialog = new MemoryEditorWindow(item);
        dialog.Owner = this;
        if (dialog.ShowDialog() == true)
        {
            _memoryStore.Save(_memory);
            RefreshMemoryList();
        }
    }

    private void DeleteMemoryItem()
    {
        if (MemoryListBox.SelectedItem is not MemoryItem item)
            return;

        if (WpfMessageBox.Show("Ta bort minnespost?", "Bekräfta", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        _memory.Remove(item);
        _memoryStore.Save(_memory);
        RefreshMemoryList();
    }

    private void SetDefaultPrompt()
    {
        if (PromptListBox.SelectedItem is not PromptDefinition prompt)
            return;

        _defaultPrompt = prompt;
        _config.DefaultPromptId = prompt.Id;
        _config.Save(_configPath);
        UpdateDefaultPromptLabel();
    }

    private void ClearDefaultPrompt()
    {
        _defaultPrompt = null;
        _config.DefaultPromptId = null;
        _config.Save(_configPath);
        UpdateDefaultPromptLabel();
    }

    private void UpdateDefaultPromptLabel()
    {
        DefaultPromptText.Text = _defaultPrompt == null
            ? "Standardprompt: (inte vald)"
            : $"Standardprompt: {_defaultPrompt.Title}";
    }

    private void InitializeTrayIcon()
    {
        _trayIcon = new TrayIconService(this, () => _currentStatus);
        InitializeAutoSave();
        if (_config.ShowTrayPinHint)
        {
            _trayIcon.ShowBalloon(
                "Visa TapScribe i aktivitetsfältet",
                "Vill du alltid se TapScribe? Gå till Inställningar → Anpassning → Aktivitetsfält → Andra systemikoner och slå på TapScribe.");
            _config.ShowTrayPinHint = false;
            _config.Save(_configPath);
        }
    }

    private void InitializeAutoSave()
    {
        // CheckBoxes
        AutoPasteCheckBox.Checked += (_, _) => AutoSaveConfig();
        AutoPasteCheckBox.Unchecked += (_, _) => AutoSaveConfig();
        StartWithWindowsCheckBox.Checked += (_, _) => AutoSaveConfig();
        StartWithWindowsCheckBox.Unchecked += (_, _) => AutoSaveConfig();
        EnableVadCheckBox.Checked += (_, _) => AutoSaveConfig();
        EnableVadCheckBox.Unchecked += (_, _) => AutoSaveConfig();
        UseDefaultPromptCheckBox.Checked += (_, _) => AutoSaveConfig();
        UseDefaultPromptCheckBox.Unchecked += (_, _) => AutoSaveConfig();
        UseAutoPromptCheckBox.Checked += (_, _) => AutoSaveConfig();
        UseAutoPromptCheckBox.Unchecked += (_, _) => AutoSaveConfig();

        // ComboBoxes
        ModelPresetCombo.SelectionChanged += (_, _) => AutoSaveConfig();

        // Prompt Selection
        PromptListBox.SelectionChanged += (_, _) => AutoSaveConfig();

        // API Keys (with a small delay/debounce if needed, but simple Save is fine for now)
        GeminiApiKeyBox.PasswordChanged += (_, _) => AutoSaveConfig();
        OpenAiApiKeyBox.PasswordChanged += (_, _) => AutoSaveConfig();
        
        // VAD
        // VAD
        SilenceThresholdTextBox.LostFocus += (_, _) => AutoSaveConfig();
        SilenceDurationTextBox.LostFocus += (_, _) => AutoSaveConfig();
    }

    private void AutoSaveConfig()
    {
        if (!_isInitialized) return;
        UpdateConfigFromUi();
        _config.Save(_configPath);

        // Apply runtime changes
        if (_controller != null)
        {
            _controller.EnableSilenceChunking = _config.EnableVad;
            _controller.VadSpeechThreshold = (float)_config.SilenceThreshold;
            _controller.VadMinSilenceMs = Math.Max(100, (int)Math.Round(_config.SilenceDurationSeconds * 1000));
        }
    }

    private void SetStatus(string status)
    {
        _currentStatus = status;
        _logger?.Info($"Status ändrad till: {status}");
        Dispatcher.Invoke(() =>
        {
            StatusText.Text = status;
            _trayIcon?.UpdateStatus(status);
            UpdateStatusBadgeColor(status);
        });
    }

    private void UpdateStatusBadgeColor(string status)
    {
        var brush = (System.Windows.Media.SolidColorBrush)FindResource("AccentBrush"); // default blue
        if (status.Contains("Lyssnar", StringComparison.OrdinalIgnoreCase))
            brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 197, 94)); // Green
        else if (status.Contains("Bearbetar", StringComparison.OrdinalIgnoreCase))
            brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(249, 115, 22)); // Orange
        else if (status.Contains("Fel", StringComparison.OrdinalIgnoreCase))
            brush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68)); // Red

        StatusBadgeDot.Fill = brush;
        StatusText.Foreground = brush;
    }

    private void ResetSessionText(string placeholder)
    {
        _currentSessionText.Clear();
        Dispatcher.Invoke(() => LastResultText.Text = placeholder);
    }

    private void OnPartialResultUpdated(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        _currentSessionText.Clear();
        _currentSessionText.Append(text);
        Dispatcher.Invoke(() => LastResultText.Text = text);
    }

    private int CalculateCpuThreads()
    {
        if (_config.ManualThreads.HasValue)
            return Math.Max(1, _config.ManualThreads.Value);

        if (_config.AutoTuneCompleted && _config.OptimalThreads.HasValue)
            return Math.Max(1, _config.OptimalThreads.Value);

        int cores = Environment.ProcessorCount;
        int threads = Math.Max(1, cores - 2);
        return Math.Max(1, threads);
    }

    private static int CalculateSegmentWorkers()
    {
        int cores = Environment.ProcessorCount;
        return cores < 8 ? 1 : 2;
    }

    private void UpdateConfigFromUi()
    {
        _config.DirectHotkey = DirectHotkeyCombo.SelectedItem?.ToString() ?? "F8";
        _config.AiHotkey = AiHotkeyCombo.SelectedItem?.ToString() ?? "F9";
        _config.AiUseDefaultPrompt = UseDefaultPromptCheckBox.IsChecked == true;
        _config.AiUseAutoPrompt = UseAutoPromptCheckBox.IsChecked == true;
        _config.AutoPasteEnabled = AutoPasteCheckBox.IsChecked == true;
        _config.StartWithWindows = StartWithWindowsCheckBox.IsChecked == true;
        _config.DarkMode = DarkModeToggle.IsChecked == true;
        _config.SilenceThreshold = ReadDoubleOrFallback(SilenceThresholdTextBox, _config.SilenceThreshold, "VAD-känslighet");
        _config.SilenceDurationSeconds = ReadDoubleOrFallback(SilenceDurationTextBox, _config.SilenceDurationSeconds, "Min tystnad (sek)");
        _config.EnableVad = EnableVadCheckBox.IsChecked == true;
        string gemini = GeminiApiKeyBox.Password.Trim();
        string openai = OpenAiApiKeyBox.Password.Trim();
        if (!string.IsNullOrWhiteSpace(gemini))
            _config.GeminiApiKey = gemini;
        if (!string.IsNullOrWhiteSpace(openai))
            _config.OpenAiApiKey = openai;
        
        _config.ManualThreads = ReadIntOrNull(ManualThreadsTextBox);
        
        if (PromptListBox.SelectedItem is string selectedTitle)
        {
            var p = _prompts.FirstOrDefault(x => x.Title == selectedTitle);
            if (p != null) _config.LastPromptId = p.Id;
        }
    }

    private void SaveSettings()
    {
        UpdateConfigFromUi();
        _config.Save(_configPath);

        ApplyAutoStartSetting(_config.StartWithWindows);

        _controller.VadSpeechThreshold = (float)_config.SilenceThreshold;
        _controller.VadMinSilenceMs = Math.Max(100, (int)Math.Round(_config.SilenceDurationSeconds * 1000));
        _controller.MaxSegmentWorkers = CalculateSegmentWorkers();
        _controller.EnableSilenceChunking = _config.EnableVad;
        RegisterHotkeys();
        UpdateHotkeyLabel();
        ApplySelectedPresetFromSettings();
        UpdateAutoTuneUi();
    }

    private void DirectHotkeyCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        _config.DirectHotkey = DirectHotkeyCombo.SelectedItem?.ToString() ?? "F8";
        UpdateHotkeyLabel();
        RegisterHotkeys();
    }

    private void AiHotkeyCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;
        _config.AiHotkey = AiHotkeyCombo.SelectedItem?.ToString() ?? "F9";
        UpdateHotkeyLabel();
        RegisterHotkeys();
    }

    private void ApplyTheme(bool dark)
    {
        _config.DarkMode = dark;
        _config.Save(_configPath);

        var res = System.Windows.Application.Current.Resources;
        if (dark)
        {
            res["WindowBg"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 23, 42)); // Slate 900
            res["CardBg"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 41, 59));   // Slate 800
            res["ControlBg"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 41, 59)); // Slate 800
            res["ButtonBg"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 99, 235));  // Blue 600
            res["ButtonHover"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(59, 130, 246)); // Blue 500
            res["HeaderBg"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 23, 42));
            res["FooterBg"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 23, 42));
            res["TextPrimary"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(241, 245, 249)); // Slate 100
            res["TextSecondary"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(148, 163, 184)); // Slate 400
            res["BorderColor"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(51, 65, 85));   // Slate 700
            res["AccentLight"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 58, 138)); // Blue 900
            res["AccentForeground"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.White);
        }
        else
        {
            res["WindowBg"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 250, 252));
            res["CardBg"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255));
            res["ControlBg"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255));
            res["ButtonBg"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(241, 245, 249));
            res["ButtonHover"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(226, 232, 240)); // Slate 200
            res["HeaderBg"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255));
            res["FooterBg"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255));
            res["TextPrimary"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 23, 42));
            res["TextSecondary"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(71, 85, 105));
            res["BorderColor"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(226, 232, 240));
            res["AccentLight"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(219, 234, 254)); // Blue 100
            res["AccentForeground"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 99, 235)); // Blue 600
        }
        ApplyTitleBarTheme(dark);
    }

    private void ApplyTitleBarTheme(bool dark)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        int value = dark ? 1 : 0;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
    }

    private void InitializeAutoTuneUi()
    {
        AutoTuneProgressBar.Value = 0;
        AutoTuneProgressBar.IsIndeterminate = false;
        AutoTuneStatusText.Text = "Auto‑tune är redo när en modell är vald.";
        AutoTuneDetailText.Text = "Tar ca 1 minut.";
        UpdateAutoTuneUi();
    }

    private void UpdateAutoTuneUi()
    {
        bool hasModel = !string.IsNullOrWhiteSpace(_config.ModelPath) && File.Exists(_config.ModelPath);

        if (_autoTuneRunning)
        {
            AutoTuneButton.IsEnabled = false;
            AutoTuneStatusText.Text = "Auto‑tune pågår…";
            return;
        }

        if (!hasModel)
        {
            AutoTuneButton.IsEnabled = false;
            AutoTuneStatusText.Text = "Auto‑tune väntar på att en modell väljs.";
            AutoTuneDetailText.Text = "Välj en modell först.";
            return;
        }

        AutoTuneButton.IsEnabled = true;
        if (_config.ManualThreads.HasValue)
        {
            AutoTuneStatusText.Text = $"Manuell inställning: {_config.ManualThreads.Value} trådar.";
            AutoTuneDetailText.Text = "Kör auto‑tune för att optimera.";
        }
        else if (_config.AutoTuneCompleted && _config.OptimalThreads.HasValue)
        {
            AutoTuneStatusText.Text = $"Auto‑tune klar: {_config.OptimalThreads.Value} trådar.";
            AutoTuneDetailText.Text = "Klicka för att köra om.";
        }
        else
        {
            AutoTuneStatusText.Text = "Auto‑tune är redo.";
            AutoTuneDetailText.Text = "Tar ca 1 minut.";
        }
    }

    private async Task StartAutoTuneIfNeededAsync()
    {
        if (_config.AutoTuneCompleted)
            return;

        bool hasModel = !string.IsNullOrWhiteSpace(_config.ModelPath) && File.Exists(_config.ModelPath);
        if (!hasModel)
        {
            UpdateAutoTuneUi();
            return;
        }

        await RunAutoTuneAsync(false);
    }

    private async Task RunAutoTuneAsync(bool manual)
    {
        if (_autoTuneRunning)
            return;

        string? modelPath = _config.ModelPath;
        if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
        {
            if (manual)
                AutoTuneStatusText.Text = "Välj en modell innan auto‑tune.";
            return;
        }

        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string audioPath = Path.Combine(baseDir, "testfile.mp3");
        string vadPath = Path.Combine(baseDir, "silero_vad.onnx");
        string nativePath = Path.Combine(baseDir, "whisper.dll");
        if (!File.Exists(audioPath))
        {
            AutoTuneStatusText.Text = "Testfil saknas för auto‑tune.";
            return;
        }
        if (!File.Exists(vadPath))
        {
            AutoTuneStatusText.Text = "VAD‑modell saknas för auto‑tune.";
            return;
        }

        _autoTuneRunning = true;
        AutoTuneProgressBar.Value = 0;
        AutoTuneProgressBar.IsIndeterminate = true;
        UpdateAutoTuneUi();

        _autoTuneCts?.Cancel();
        _autoTuneCts = new CancellationTokenSource();

        var progress = new Progress<AutoTuneProgress>(p =>
        {
            AutoTuneProgressBar.IsIndeterminate = false;
            AutoTuneProgressBar.Maximum = Math.Max(1, p.StepCount);
            AutoTuneProgressBar.Value = Math.Min(p.StepIndex, p.StepCount);
            AutoTuneStatusText.Text = p.Message;
            if (p.Threads.HasValue)
                AutoTuneDetailText.Text = $"Trådar: {p.Threads.Value}";
        });

        try
        {
            var settings = new AutoTuneSettings
            {
                AudioPath = audioPath,
                ModelPath = modelPath,
                VadModelPath = vadPath,
                NativeLibraryPath = nativePath,
                Language = "sv",
                BeamSize = 2,
                VadOptions = new VadChunkerOptions
                {
                    SpeechThreshold = _controller.VadSpeechThreshold,
                    MinSpeechMs = _controller.VadMinSpeechMs,
                    MinSilenceMs = _controller.VadMinSilenceMs,
                    SpeechPadMs = _controller.VadSpeechPadMs,
                    MaxSegmentSeconds = _controller.MaxSegmentSeconds,
                    SoftMaxGraceSeconds = _controller.SoftMaxGraceSeconds,
                    OverlapSeconds = _controller.OverlapSeconds
                }
            };

            var service = new AutoTuneService();
        SetInitStatus("Optimerar för din dator...", true);
        var result = await service.RunAsync(settings, progress, _autoTuneCts.Token);
        if (result == null)
        {
            AutoTuneStatusText.Text = "Auto‑tune kunde inte köras.";
            SetInitStatus(null, false);
            return;
        }
    

            _config.AutoTuneCompleted = true;
            _config.OptimalThreads = result.OptimalThreads;
            _engine.CpuThreads = result.OptimalThreads;
            _config.Save(_configPath);
            AutoTuneStatusText.Text = $"Auto‑tune klar: {result.OptimalThreads} trådar.";
        AutoTuneDetailText.Text = "Klicka för att köra om.";
        AutoTuneProgressBar.Value = AutoTuneProgressBar.Maximum;
        SetInitStatus(null, false);
        _logger?.Info($"AutoTune: optimal threads={result.OptimalThreads}");
        }
        catch (Exception ex)
    {
        AutoTuneStatusText.Text = "Auto‑tune misslyckades.";
        SetInitStatus(null, false);
        _logger?.Error("AutoTune failed", ex);
    }
    
        finally
        {
            _autoTuneRunning = false;
            AutoTuneProgressBar.IsIndeterminate = false;
            UpdateAutoTuneUi();
        }
    }

    private async Task ShowWelcomeIfNeededAsync()
    {
        if (_config.HasSeenWelcome || _welcomeShowing)
            return;

        try
        {
            _welcomeShowing = true;
            _logger?.Info("ShowWelcomeIfNeededAsync: Startar välkomstflödet.");

            var recommended = _presets.FirstOrDefault(p => p.Subfolder == "kb-whisper-base") ?? _presets.FirstOrDefault();
            string label = recommended?.DisplayName ?? "Standard";

            int attempts = 0;
            const int maxAttempts = 10;
            bool shown = false;

            while (attempts < maxAttempts && !shown)
            {
                attempts++;
                try
                {
                    var helper = new WindowInteropHelper(this);
                    IntPtr handle = helper.Handle;

                    if (!IsVisible && attempts < 5)
                    {
                        _logger?.Info($"Väntar på att huvudfönstret ska bli synligt (försök {attempts})...");
                        await Task.Delay(200);
                        continue;
                    }

                    var dialog = new WelcomeWindow(label);

                    // Sätt ägare endast om vi har ett giltigt tillstånd
                    if (IsVisible && handle != IntPtr.Zero)
                    {
                        dialog.Owner = this;
                    }

                    bool? result = dialog.ShowDialog();
                    shown = true;
                    _config.HasSeenWelcome = true;

                    if (dialog.AutoStartSelected)
                    {
                        _config.StartWithWindows = true;
                        StartWithWindowsCheckBox.IsChecked = true;
                        ApplyAutoStartSetting(true);
                    }

                    _config.Save(_configPath);

                    if (dialog.ShouldDownloadModel && recommended != null)
                    {
                        ApplyPreset(recommended);
                        await DownloadSelectedModelAsync();
                    }
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("Owner"))
                {
                    _logger?.Warn($"Krock vid försök {attempts}, väntar på Windows...");
                    await Task.Delay(200);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.Error("Kritiskt fel i välkomstflödet", ex);
        }
        finally
        {
            _welcomeShowing = false;
        }
    }


    private bool EnsureVadAvailable()
    {
        string vadPath = _controller.VadModelPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "silero_vad.onnx");
        if (File.Exists(vadPath))
            return true;

        _logger?.Error($"VAD saknas: {vadPath}");
        SetStatus("VAD saknas");
        WpfMessageBox.Show(
            "Tystnadsdetekteringen (VAD) saknas. Installationen kan vara ofullständig.\n\nStarta om appen eller installera om TapScribe.",
            "VAD saknas",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        return false;
    }

    private double ReadDoubleOrFallback(System.Windows.Controls.TextBox textBox, double fallback, string label)
    {
        string text = textBox.Text.Trim();
        // Accept both comma and period as decimal separators for better UX
        string normalized = text.Replace('.', ',');
        if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) && value > 0)
            return value;

        WpfMessageBox.Show($"{label} måste vara ett positivt tal. Återställer till {fallback.ToString(CultureInfo.CurrentCulture)}.",
            "Ogiltigt värde",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
        textBox.Text = fallback.ToString(CultureInfo.CurrentCulture);
        return fallback;
    }

    private int? ReadIntOrNull(System.Windows.Controls.TextBox textBox)
    {
        string text = textBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (int.TryParse(text, out var val) && val > 0) return val;
        return null;
    }

    private void ApplySelectedPresetFromSettings()
    {
        if (ModelPresetCombo.SelectedItem is not ModelPreset preset)
            return;

        if (TryApplyActivePreset(preset))
        {
            UpdatePresetDownloadedFlags();
            RefreshPresetCombo();
        }
    }

        public async Task ManualCheckForUpdatesAsync()
    {
        try
        {
            await CheckForUpdatesAsync(true);
        }
        catch (Exception ex)
        {
            _logger?.Error("Manuell uppdateringskontroll misslyckades", ex);
            WpfMessageBox.Show($"Kunde inte söka efter uppdateringar:\n{ex.Message}", "Fel", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task CheckForUpdatesAsync(bool manual = false)
    {
        if (!manual && !_config.UpdateCheckEnabled)
            return;
        if (string.IsNullOrWhiteSpace(_config.UpdateRepoOwner) || string.IsNullOrWhiteSpace(_config.UpdateRepoName))
            return;

        if (!manual && _config.LastUpdateCheckUtc.HasValue)
        {
            var elapsed = DateTimeOffset.UtcNow - _config.LastUpdateCheckUtc.Value;
            if (elapsed < TimeSpan.FromHours(24))
                return;
        }

        if (!manual)
        {
            _config.LastUpdateCheckUtc = DateTimeOffset.UtcNow;
            _config.Save(_configPath);
        }

        try
        {
            string url = $"https://api.github.com/repos/{_config.UpdateRepoOwner}/{_config.UpdateRepoName}/releases/latest";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("TapScribe", GetCurrentVersionString()));
            using var response = await _httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                if (manual) WpfMessageBox.Show("Kunde inte nå GitHub för att söka efter uppdateringar.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("tag_name", out var tag))
                return;

            string tagValue = tag.GetString() ?? "";
            if (!TryParseVersion(tagValue, out var latest))
                return;

            if (!TryParseVersion(GetCurrentVersionString(), out var current))
                return;

            if (latest <= current)
            {
                if (manual) WpfMessageBox.Show("Du har redan den senaste versionen!", "Uppdatering", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string? download = null;
            if (doc.RootElement.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    if (!asset.TryGetProperty("name", out var nameProp))
                        continue;
                    string name = nameProp.GetString() ?? "";
                    if (!asset.TryGetProperty("browser_download_url", out var urlProp))
                        continue;
                    string assetUrl = urlProp.GetString() ?? "";
                    if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        download = assetUrl;
                        break;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(download) && doc.RootElement.TryGetProperty("html_url", out var html))
                download = html.GetString();

            if (string.IsNullOrWhiteSpace(download))
                return;

            _updateDownloadUrl = download;
            Dispatcher.Invoke(() =>
            {
                UpdateBannerText.Text = $"Ny version {latest} finns tillgänglig.";
                UpdateBanner.Visibility = Visibility.Visible;
            });

            if (manual)
            {
                if (WpfMessageBox.Show($"En ny version ({latest}) finns tillgänglig!\nVill du gå till hämtningssidan?", "Uppdatering finns", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(download) { UseShellExecute = true });
                }
            }
        }
        catch (Exception ex)
        {
            if (manual) WpfMessageBox.Show($"Fel vid uppdateringskontroll: {ex.Message}", "Fel", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenUpdateUrl()
    {
        if (string.IsNullOrWhiteSpace(_updateDownloadUrl))
            return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_updateDownloadUrl)
            {
                UseShellExecute = true
            });
        }
        catch
        {
            // Ignore launch failures.
        }
    }

    private static string GetCurrentVersionString()
    {
        return typeof(MainWindow).Assembly.GetName().Version?.ToString() ?? "0.0.0";
    }

    private static bool TryParseVersion(string input, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(input))
            return false;

        string trimmed = input.Trim();
        if (trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed.Substring(1);

        var parts = trimmed.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3)
            trimmed = $"{parts[0]}.{parts[1]}.{parts[2]}";
        else if (parts.Length == 2)
            trimmed = $"{parts[0]}.{parts[1]}.0";
        else if (parts.Length == 1)
            trimmed = $"{parts[0]}.0.0";

        if (!Version.TryParse(trimmed, out var parsed) || parsed == null)
            return false;

        version = parsed;
        return true;
    }

    private static void ApplyAutoStartSetting(bool enabled)
    {
        const string runKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string appName = "TapScribe";
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(runKey, writable: true)
                           ?? Microsoft.Win32.Registry.CurrentUser.CreateSubKey(runKey);
            if (key == null)
                return;

            if (!enabled)
            {
                key.DeleteValue(appName, throwOnMissingValue: false);
                return;
            }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string exePath = Path.Combine(baseDir, "TapScribe.exe");
            string command;
            if (File.Exists(exePath))
            {
                command = $"\"{exePath}\"";
            }
            else
            {
                string dllPath = Path.Combine(baseDir, "WsprPc.dll");
                string dotnet = Environment.ProcessPath ?? "dotnet.exe";
                command = $"\"{dotnet}\" \"{dllPath}\"";
            }

            key.SetValue(appName, command);
        }
        catch
        {
            // Ignore registry errors (e.g., policy restrictions).
        }
    }

    private void InitializeModelSelector()
    {
        UpdatePresetDownloadedFlags();
        ModelPresetCombo.ItemsSource = _presets;

        ModelPreset selected = _presets[1];
        ModelPreset? activePreset = null;
        if (!string.IsNullOrWhiteSpace(_config.ModelPath))
        {
            activePreset = _presets.FirstOrDefault(p =>
                string.Equals(ResolveModelPath(p), _config.ModelPath, StringComparison.OrdinalIgnoreCase));
            if (activePreset != null)
                selected = activePreset;
        }

        _suppressModelSelectionChanged = true;
        ModelPresetCombo.SelectedItem = selected;
        _suppressModelSelectionChanged = false;
        ApplyPreset(selected);

        if (activePreset != null)
        {
            ModelStateText.Text = IsModelPresent(activePreset)
                ? $"Modellstatus: {activePreset.DisplayName}"
                : "Modellstatus: saknas (ladda ner i Avancerat)";
        }
        else if (!string.IsNullOrWhiteSpace(_config.ModelPath))
        {
            ModelStateText.Text = "Modellstatus: anpassad modell";
        }
        else
        {
            ModelStateText.Text = "Modellstatus: saknas (ladda ner i Avancerat)";
        }

        ModelPresetCombo.SelectionChanged += (_, _) =>
        {
            if (_suppressModelSelectionChanged)
                return;

            if (ModelPresetCombo.SelectedItem is ModelPreset preset)
            {
                ApplyPreset(preset);
                TryApplyActivePreset(preset);
            }
        };
    }

    private void AutoSelectModelIfUnset()
    {
        if (!string.IsNullOrWhiteSpace(_config.ModelPath))
            return;

        if (!string.IsNullOrWhiteSpace(_config.SelectedModel))
        {
            var preset = _presets.FirstOrDefault(p =>
                string.Equals(p.FileName, _config.SelectedModel, StringComparison.OrdinalIgnoreCase));
            if (preset != null && TryApplyActivePreset(preset))
            {
                _suppressModelSelectionChanged = true;
                ModelPresetCombo.SelectedItem = preset;
                _suppressModelSelectionChanged = false;
                UpdatePresetDownloadedFlags();
                RefreshPresetCombo();
            }
            return;
        }

        var preferred = _presets.FirstOrDefault(p => p.Subfolder == "kb-whisper-base" && IsModelPresent(p));
        var fallback = preferred ?? _presets.FirstOrDefault(IsModelPresent);
        if (fallback == null)
            return;

        if (TryApplyActivePreset(fallback))
        {
            _suppressModelSelectionChanged = true;
            ModelPresetCombo.SelectedItem = fallback;
            _suppressModelSelectionChanged = false;
            UpdatePresetDownloadedFlags();
            RefreshPresetCombo();
        }
    }

    private bool TryApplyActivePreset(ModelPreset preset)
    {
        var modelPath = ResolveModelPath(preset);
        if (modelPath == null)
        {
            ModelStateText.Text = "Modellstatus: saknas (ladda ner i Avancerat)";
            return false;
        }

        bool modelChanged = !string.Equals(_config.ModelPath, modelPath, StringComparison.OrdinalIgnoreCase);
        _engine.ModelPath = modelPath;
        _config.ModelPath = modelPath;
        _config.SelectedModel = Path.GetFileName(modelPath);
        if (modelChanged)
        {
            _config.AutoTuneCompleted = false;
            _config.OptimalThreads = null;
        }
        _config.Save(_configPath);
        ModelStateText.Text = $"Modellstatus: {preset.DisplayName}";
        UpdateAutoTuneUi();
        return true;
    }

    private string? ResolveModelPath(ModelPreset preset)
    {
        string modelDir = string.IsNullOrWhiteSpace(_config.ModelDir)
            ? Path.Combine(Environment.CurrentDirectory, "models")
            : _config.ModelDir!;

        if (!Directory.Exists(modelDir))
            return null;

        var folder = Path.Combine(modelDir, preset.Subfolder);
        var candidate = Path.Combine(folder, preset.FileName);
        if (File.Exists(candidate))
            return candidate;

        return null;
    }

    private bool IsModelPresent(ModelPreset preset) => ResolveModelPath(preset) != null;

    private void UpdatePresetDownloadedFlags()
    {
        foreach (var preset in _presets)
            preset.IsDownloaded = IsModelPresent(preset);
    }

    private void RefreshPresetCombo()
    {
        if (ModelPresetCombo.ItemsSource != null)
            ModelPresetCombo.Items.Refresh();
    }

    private void InitializeDownloadUi()
    {
        _presets = new List<ModelPreset>
        {
            new(
                "Snabb (KB‑Whisper tiny)",
                "https://huggingface.co/KBLab/kb-whisper-tiny/resolve/main/ggml-model-q5_0.bin?download=true",
                "ggml-model-q5_0.bin",
                "kb-whisper-tiny",
                "Snabbast • Lägst kvalitet")
            ,
            new(
                "Standard (KB‑Whisper base)",
                "https://huggingface.co/KBLab/kb-whisper-base/resolve/main/ggml-model-q5_0.bin?download=true",
                "ggml-model-q5_0.bin",
                "kb-whisper-base",
                "Bra balans • Rekommenderas för de flesta laptops")
            ,
            new(
                "Noggrann (KB‑Whisper small)",
                "https://huggingface.co/KBLab/kb-whisper-small/resolve/main/ggml-model-q5_0.bin?download=true",
                "ggml-model-q5_0.bin",
                "kb-whisper-small",
                "Bättre kvalitet • Långsammare")
            ,
            new(
                "Extra (KB‑Whisper medium)",
                "https://huggingface.co/KBLab/kb-whisper-medium/resolve/main/ggml-model-q5_0.bin?download=true",
                "ggml-model-q5_0.bin",
                "kb-whisper-medium",
                "Hög kvalitet • Mycket långsammare")
            ,
            new(
                "Max (KB‑Whisper large)",
                "https://huggingface.co/KBLab/kb-whisper-large/resolve/main/ggml-model-q5_0.bin?download=true",
                "ggml-model-q5_0.bin",
                "kb-whisper-large",
                "Bäst kvalitet • Tungt för CPU")
        };

        DownloadProgressBar.Value = 0;
        DownloadStatusText.Text = "Väntar";
    }

    private void ApplyPreset(ModelPreset preset)
    {
        _downloadPreset = preset;
        ModelUrlTextBox.Text = preset.Url;
        ModelFileNameTextBox.Text = preset.FileName;
        PresetNoteText.Text = preset.Note;
    }

    private async Task PromptDownloadForPresetAsync(ModelPreset preset)
    {
        var confirm = WpfMessageBox.Show(
            $"Modellen \"{preset.DisplayName}\" saknas.\nVill du ladda ner den nu?",
            "Modell saknas",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes)
            return;

        ApplyPreset(preset);
        await DownloadSelectedModelAsync();
    }

    private async Task DownloadSelectedModelAsync()
    {
        string url = ModelUrlTextBox.Text.Trim();
        string fileName = ModelFileNameTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(fileName))
        {
            DownloadStatusText.Text = "Ange URL och filnamn.";
            return;
        }

        string modelDir = string.IsNullOrWhiteSpace(_config.ModelDir)
            ? Path.Combine(Environment.CurrentDirectory, "models")
            : _config.ModelDir!;

        string targetDir = modelDir;
        if (_downloadPreset != null)
            targetDir = Path.Combine(modelDir, _downloadPreset.Subfolder);

        Directory.CreateDirectory(targetDir);
        string targetPath = Path.Combine(targetDir, fileName);

        if (File.Exists(targetPath))
        {
            var overwrite = WpfMessageBox.Show(
                "En fil med samma namn finns redan. Vill du skriva över?",
                "Modellnedladdning",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (overwrite != MessageBoxResult.Yes)
                return;
        }

          try
          {
              DownloadModelButton.IsEnabled = false;
              DownloadStatusText.Text = "Laddar ner...";
              DownloadProgressBar.Value = 0;
              DownloadProgressBar.IsIndeterminate = true;

            _downloadCts?.Cancel();
        _downloadCts = new CancellationTokenSource();
        SetInitStatus("Laddar ner språkmodell...", true);

        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, _downloadCts.Token);
            response.EnsureSuccessStatusCode();

            long? total = response.Content.Headers.ContentLength;
            await using var stream = await response.Content.ReadAsStreamAsync(_downloadCts.Token);
            await using var file = File.Create(targetPath);

            var buffer = new byte[81920];
            long readTotal = 0;
            int read;
              while ((read = await stream.ReadAsync(buffer, _downloadCts.Token)) > 0)
              {
                  await file.WriteAsync(buffer.AsMemory(0, read), _downloadCts.Token);
                  readTotal += read;

                  if (total.HasValue && total.Value > 0)
                  {
                      if (DownloadProgressBar.IsIndeterminate)
                          DownloadProgressBar.IsIndeterminate = false;
                      double pct = readTotal * 100d / total.Value;
                      DownloadProgressBar.Value = Math.Min(100, pct);
                      DownloadStatusText.Text = $"Laddar ner... {Math.Round(pct)}%";
                  }
              }

              DownloadStatusText.Text = $"Nedladdad: {targetPath}";
              RefreshModelSelector(targetPath);
              ApplyDownloadedPresetIfUnset(targetPath);
              UpdateAutoTuneUi();
          SetInitStatus(null, false);
          _ = StartAutoTuneIfNeededAsync();
      }
      catch (Exception ex)
      {
          DownloadStatusText.Text = "Nedladdning misslyckades: " + ex.Message;
          SetInitStatus(null, false);
      }
    
          finally
          {
              DownloadProgressBar.IsIndeterminate = false;
              DownloadModelButton.IsEnabled = true;
          }
      }

    private void RefreshModelSelector(string preferredPath)
    {
        var preset = _presets.FirstOrDefault(p =>
            preferredPath.Contains(Path.Combine(p.Subfolder, p.FileName), StringComparison.OrdinalIgnoreCase));

        if (preset != null)
        {
            _suppressModelSelectionChanged = true;
            ModelPresetCombo.SelectedItem = preset;
            _suppressModelSelectionChanged = false;
            ApplyPreset(preset);
            UpdatePresetDownloadedFlags();
            RefreshPresetCombo();
        }
        else
        {
            UpdatePresetDownloadedFlags();
            RefreshPresetCombo();
        }
    }

    private void ApplyDownloadedPresetIfUnset(string downloadedPath)
    {
        if (!string.IsNullOrWhiteSpace(_config.ModelPath) || !string.IsNullOrWhiteSpace(_config.SelectedModel))
            return;

        var preset = _presets.FirstOrDefault(p =>
            downloadedPath.Contains(Path.Combine(p.Subfolder, p.FileName), StringComparison.OrdinalIgnoreCase));
        if (preset == null)
            return;

        if (TryApplyActivePreset(preset))
        {
            _suppressModelSelectionChanged = true;
            ModelPresetCombo.SelectedItem = preset;
            _suppressModelSelectionChanged = false;
            UpdatePresetDownloadedFlags();
            RefreshPresetCombo();
        }
    }

    private void ShowModelInfo()
    {
        const string info = "Modellnivåer (högre = bättre kvalitet men långsammare):\n" +
                            "- Snabb (tiny): snabbast, lägst kvalitet\n" +
                            "- Standard (base): bra balans (rekommenderas)\n" +
                            "- Noggrann (small): bättre kvalitet, långsammare\n" +
                            "- Extra (medium): hög kvalitet, mycket långsammare\n" +
                            "- Max (large): bäst kvalitet, tyngst för CPU\n\n" +
                            "Tips: välj Standard om du är osäker.";
        WpfMessageBox.Show(info, "Modellinfo", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ShowAbout()
    {
        var dialog = new AboutWindow { Owner = this };
        dialog.ShowDialog();
    }

    private void ShowAiInfo()
    {
        string aiKey = _config.AiHotkey;
        string info =
            $"Håll in {aiKey} för att transkribera, sedan bearbetar AI:n texten enligt din valda prompt.\n\n" +
            "Standardprompt: Används automatiskt om du aktiverat alternativet.\n" +
            "Autoprompt: Kommer ihåg vilken prompt du senast valde.";
        WpfMessageBox.Show(info, "AI-bearbetning", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ShowPromptInfo()
    {
        const string info =
            "Promptar styr hur AI:n formaterar texten.\n\n" +
            "Exempel: Sammanfattning, WhatsApp, Mail.\n" +
            "Välj en prompt som standard om du vill slippa väljaren.";
        WpfMessageBox.Show(info, "Prompt‑hjälp", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ShowMemoryInfo()
    {
        const string info =
            "Minne läggs till som extra kontext i prompten.\n\n" +
            "Bra för namn, preferenser och återkommande fakta.\n" +
            "Exempel: \"Bokningslänk: https://aiolle.se/bokning\".\n" +
            "Säger du: \"Be dem boka ett möte\" → AI kan svara: \"Hej! Boka gärna här: https://aiolle.se/bokning\".\n\n" +
            "Aktiveras per prompt via \"Använd minne\".";
        WpfMessageBox.Show(info, "Minne‑hjälp", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnDirectHotkeyDown()
    {
        if (_controller.IsRecording)
            return;

        if (_controller.IsTranscribing)
        {
            _logger?.Info("Direkt: key down ignorerad (redan bearbetar)");
            return;
        }

        if (!EnsureVadAvailable())
            return;

        _targetWindow = NativeMethods.GetForegroundWindow();
        _controller.TargetWindow = _targetWindow;
        _logger?.Info("Direkt: key down");
        Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal;
        SetStatus("Lyssnar...");
        ResetSessionText("Lyssnar...");
        _controller.StartRecording();
    }

    private async void OnDirectHotkeyUp()
    {
        if (!_controller.IsRecording)
            return;

        _logger?.Info("Direkt: key up");
        SetStatus("Bearbetar...");

        try
        {
            var result = await _controller.StopAndTranscribeAsync(IsAutoPasteEnabled());
        Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.Normal;
            if (string.IsNullOrWhiteSpace(result))
            {
                _logger?.Info("Direkt: StopAndTranscribeAsync gav inget resultat.");
                SetStatus("Ingen transkribering.");
                Dispatcher.Invoke(() => LastResultText.Text = "Ingen transkribering fångades.");
                await Task.Delay(1500);
            }
            else
            {
                _logger?.Info($"Direkt: Fick resultat ({result.Length} tecken).");
                Dispatcher.Invoke(() => LastResultText.Text = result);
                if (IsAutoPasteEnabled())
                {
                    _logger?.Info($"Direkt: auto-paste {( _controller.LastPasteSucceeded ? "ok" : "misslyckades" )} (mål=0x{_targetWindow.ToInt64():X})");
                    if (!_controller.LastPasteSucceeded)
                    {
                        SetStatus("Kunde inte klistra in — texten finns i urklipp.");
                        await Task.Delay(2000);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.Error("Direkt: fel vid transkribering", ex);
            SetStatus("Fel: " + ex.Message);
            await Task.Delay(3000);
        }

        if (_currentStatus.StartsWith("Bearbetar", StringComparison.OrdinalIgnoreCase) || 
            _currentStatus.StartsWith("Ingen transkribering", StringComparison.OrdinalIgnoreCase) ||
            _currentStatus.StartsWith("Kunde inte klistra in", StringComparison.OrdinalIgnoreCase) ||
            _currentStatus.StartsWith("Fel", StringComparison.OrdinalIgnoreCase))
        {
            SetStatus("Väntar");
        }
    }

    private void OnAiHotkeyDown()
    {
        _logger?.Info($"AI: key down START (IsRecording={_controller.IsRecording})");
        if (_controller.IsRecording)
        {
            _logger?.Info("AI: key down IGNORED (already recording)");
            return;
        }

        if (!EnsureVadAvailable())
        {
            _logger?.Info("AI: key down ABORTED (VAD unavailable)");
            return;
        }

        // Capture clipboard before recording (for UseClipboard prompts)
        _clipboardContext = System.Windows.Clipboard.ContainsText() ? System.Windows.Clipboard.GetText() : null;

        _targetWindow = NativeMethods.GetForegroundWindow();
        _controller.TargetWindow = _targetWindow;
        _logger?.Info("AI: key down -> StartRecording");
        Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal;
        SetStatus("Lyssnar (AI)...");
        ResetSessionText("Lyssnar (AI)...");
        _controller.StartRecording();
        _logger?.Info($"AI: key down END (IsRecording={_controller.IsRecording})");
    }

    private async void OnAiHotkeyUp()
    {
        _logger?.Info($"AI: key up START (IsRecording={_controller.IsRecording})");
        if (!_controller.IsRecording)
        {
            _logger?.Info("AI: key up IGNORED (not recording)");
            return;
        }

        _logger?.Info("AI: key up -> Bearbetar");
        SetStatus("Bearbetar (AI)...");

        string? transcript = null;
        try
        {
            transcript = await _controller.StopAndTranscribeAsync(false);
        }
        catch (Exception ex)
        {
            _logger?.Error("AI: fel vid transkribering", ex);
            SetStatus("Fel: " + ex.Message);
        }

        bool resetStatus = true;
        if (string.IsNullOrWhiteSpace(transcript))
        {
            SetStatus("Ingen transkribering.");
            Dispatcher.Invoke(() => LastResultText.Text = "Ingen transkribering fångades.");
            _logger?.Info("AI: ingen transkribering");
            resetStatus = true; // Still reset later
            await Task.Delay(1500);
        }
        else
        {
            try
            {
                Dispatcher.Invoke(() => LastResultText.Text = transcript);
                var prompt = PickPrompt();
                if (prompt == null)
                {
                    SetStatus("Ingen prompt vald.");
                    Dispatcher.Invoke(() => LastResultText.Text = "Ingen prompt vald.");
                    _logger?.Info("AI: ingen prompt vald");
                    await Task.Delay(1500);
                    SetStatus("Väntar");
                    return;
                }

                _logger?.Info($"AI: prompt='{prompt.Title}', provider={prompt.Provider}, model={(prompt.Provider == AiProvider.Gemini ? prompt.GeminiModel : prompt.OpenAiModel)}");
                string aiText = await ProcessWithAiAsync(transcript, prompt);
                if (!string.IsNullOrWhiteSpace(aiText))
                {
                    _config.LastPromptId = prompt.Id;
                    _config.Save(_configPath);
                    if (IsAutoPasteEnabled())
                    {
                        _controller.PasteResult(aiText);
                        _logger?.Info($"AI: auto-paste {(_controller.LastPasteSucceeded ? "ok" : "misslyckades")} (mål=0x{_targetWindow.ToInt64():X})");
                        if (!_controller.LastPasteSucceeded)
                            SetStatus("Kunde inte klistra in — texten finns i urklipp.");
                        else
                            SetStatus("Väntar");
                    }
                    else
                    {
                        SetStatus("Väntar");
                    }
                    Dispatcher.Invoke(() => LastResultText.Text = aiText);
                    resetStatus = false; // Already handled status
                }
                else
                {
                    SetStatus("AI‑svaret var tomt.");
                    Dispatcher.Invoke(() => LastResultText.Text = "AI‑svaret var tomt.");
                    _logger?.Info("AI: tomt svar");
                    await Task.Delay(1500);
                    resetStatus = true;
                }
            }
            catch (Exception ex)
            {
                _logger?.Error("AI: fel i bearbetning", ex);
                SetStatus("AI‑fel: " + ex.Message);
                Dispatcher.Invoke(() => LastResultText.Text = "AI‑fel: " + ex.Message);
                await Task.Delay(3000);
                resetStatus = true;
            }
        }

        if (resetStatus)
        {
            SetStatus("Väntar");
        }
    }

    private PromptDefinition? PickPrompt()
    {
        if (_prompts.Count == 0)
            return null;

        if (_config.AiUseDefaultPrompt && _defaultPrompt != null)
            return _defaultPrompt;

        if (_config.AiUseAutoPrompt && !string.IsNullOrWhiteSpace(_config.LastPromptId))
        {
            var last = _prompts.FirstOrDefault(p => p.Id == _config.LastPromptId);
            if (last != null)
                return last;
        }

        var picker = new PromptPickerWindow(_prompts);
        var selected = picker.ShowDialog() == true ? picker.SelectedPrompt : null;
        RestoreTargetWindowFocus();
        return selected;
    }

    private async Task<string> ProcessWithAiAsync(string text, PromptDefinition prompt)
    {
        BuildPromptBlocks(prompt, text, out var systemInstruction, out var bodyText);

        if (prompt.Provider == AiProvider.Gemini)
        {
            string? key = GetGeminiKey();
            if (string.IsNullOrWhiteSpace(key))
                throw new InvalidOperationException("Gemini‑nyckel saknas.");

            return await _geminiClient.GenerateAsync(
                key,
                prompt.GeminiModel,
                CombineForGemini(systemInstruction, bodyText),
                prompt.GeminiUseThinking,
                prompt.GeminiUseGrounding);
        }

        string? openAiKey = GetOpenAiKey();
        if (string.IsNullOrWhiteSpace(openAiKey))
            throw new InvalidOperationException("OpenAI‑nyckel saknas.");

        return await _openAiClient.GenerateAsync(
            openAiKey,
            prompt.OpenAiModel,
            systemInstruction,
            bodyText,
            prompt.OpenAiReasoningEffort);
    }

    private string? GetGeminiKey()
    {
        if (!string.IsNullOrWhiteSpace(_config.GeminiApiKey))
            return _config.GeminiApiKey;
        var ui = GeminiApiKeyBox.Password.Trim();
        return string.IsNullOrWhiteSpace(ui) ? null : ui;
    }

    private string? GetOpenAiKey()
    {
        if (!string.IsNullOrWhiteSpace(_config.OpenAiApiKey))
            return _config.OpenAiApiKey;
        var ui = OpenAiApiKeyBox.Password.Trim();
        return string.IsNullOrWhiteSpace(ui) ? null : ui;
    }

    private void BuildPromptBlocks(PromptDefinition prompt, string transcript, out string systemInstruction, out string bodyText)
    {
        systemInstruction = prompt.SystemInstruction.Trim();

        string memoryBlock = string.Empty;
        if (prompt.UseMemory && _memory.Count > 0)
        {
            var lines = _memory.Select(m => $"- {m.Title}: {m.Content}");
            memoryBlock = "Memory:\n" + string.Join("\n", lines) + "\n\n";
        }

        string clipboardBlock = string.Empty;
        if (prompt.UseClipboard && !string.IsNullOrWhiteSpace(_clipboardContext))
        {
            clipboardBlock = $"[CLIPBOARD CONTEXT]\n{_clipboardContext}\n\n";
        }

        string userBlock = string.IsNullOrWhiteSpace(prompt.UserInstruction)
            ? string.Empty
            : $"Användarinstruktion:\n{prompt.UserInstruction.Trim()}\n\n";

        bodyText = $"{memoryBlock}{clipboardBlock}{userBlock}Transcript:\n{transcript}\n\nReturn only the result.".Trim();
    }

    private static string CombineForGemini(string systemInstruction, string bodyText)
    {
        if (string.IsNullOrWhiteSpace(systemInstruction))
            return bodyText;

        return $"Systeminstruktion:\n{systemInstruction}\n\n{bodyText}".Trim();
    }

    private bool IsAutoPasteEnabled()
    {
        return AutoPasteCheckBox.IsChecked == true;
    }

    private sealed class ModelPreset
    {
        public ModelPreset(string displayName, string url, string fileName, string subfolder, string note)
        {
            DisplayName = displayName;
            Url = url;
            FileName = fileName;
            Subfolder = subfolder;
            Note = note;
        }

        public string DisplayName { get; }
        public string Url { get; }
        public string FileName { get; }
        public string Subfolder { get; }
        public string Note { get; }
        public bool IsDownloaded { get; set; }
        public string DisplayLabel => IsDownloaded ? $"✓ {DisplayName}" : DisplayName;
    }

    private static string GetAppDataDir()
    {
        string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string dataDir = Path.Combine(baseDir, "TapScribe");
        Directory.CreateDirectory(dataDir);
        Directory.CreateDirectory(Path.Combine(dataDir, "logs"));
        Directory.CreateDirectory(Path.Combine(dataDir, "models"));
        return dataDir;
    }

    private static AppConfig LoadConfigWithMigration(string configPath)
    {
        if (File.Exists(configPath))
        {
            var loadedConfig = AppConfig.Load(configPath);
            if (loadedConfig.EnsureDefaultsAndMigrate())
            {
                try
                {
                    loadedConfig.Save(configPath);
                }
                catch
                {
                    // Ignore save failures.
                }
            }
            return loadedConfig;
        }

        string legacyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        if (File.Exists(legacyPath))
        {
            try
            {
                File.Copy(legacyPath, configPath, overwrite: true);
                var loadedConfig = AppConfig.Load(configPath);
                if (loadedConfig.EnsureDefaultsAndMigrate())
                {
                    try
                    {
                        loadedConfig.Save(configPath);
                    }
                    catch
                    {
                        // Ignore save failures.
                    }
                }
                return loadedConfig;
            }
            catch
            {
                var loadedConfig = AppConfig.Load(legacyPath);
                if (loadedConfig.EnsureDefaultsAndMigrate())
                {
                    try
                    {
                        loadedConfig.Save(legacyPath);
                    }
                    catch
                    {
                        // Ignore save failures.
                    }
                }
                return loadedConfig;
            }
        }

        var newConfig = new AppConfig
        {
            ModelDir = Path.Combine(Path.GetDirectoryName(configPath) ?? string.Empty, "models"),
            LogDir = Path.Combine(Path.GetDirectoryName(configPath) ?? string.Empty, "logs")
        };
        if (newConfig.EnsureDefaultsAndMigrate())
        {
            try
            {
                newConfig.Save(configPath);
            }
            catch
            {
                // Ignore save failures.
            }
        }
        return newConfig;
    }

    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
    }

    private void RestoreTargetWindowFocus()
    {
        if (_targetWindow == IntPtr.Zero)
            return;

        try
        {
            NativeMethods.SetForegroundWindow(_targetWindow);
        }
        catch
        {
            // Ignore focus restore failures.
        }
    }

    private static string FindEnvPath()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string current = baseDir;

        for (int i = 0; i < 8; i++)
        {
            string candidate = Path.Combine(current, ".env");
            if (File.Exists(candidate))
                return candidate;

            var parent = Directory.GetParent(current);
            if (parent == null)
                break;
            current = parent.FullName;
        }

        return Path.Combine(baseDir, ".env");
    }

    private static string? ResolveFallbackCliPath()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string fallback = Path.Combine(baseDir, "whisper_win32", "whisper-cli.exe");
        return File.Exists(fallback) ? fallback : null;
    }

    #region File Transcription (Diarization)

    private void InitializeFileTranscriptionService()
    {
        try
        {
            string modelsPath = _config.SherpaModelsPath ?? 
                Path.Combine(AppContext.BaseDirectory, "third_party", "models", "sherpa");
            
            _fileTranscriptionService = new FileTranscriptionService(_engine, modelsPath);
            
            // Check if models exist and update UI
            UpdateDiarizationModelBanner();
        }
        catch (Exception ex)
        {
            _logger?.Error("Failed to initialize file transcription service", ex);
        }
    }

    private void UpdateDiarizationModelBanner()
    {
        bool modelsReady = _fileTranscriptionService?.ModelsReady ?? false;
        DiarizationModelBanner.Visibility = modelsReady ? Visibility.Collapsed : Visibility.Visible;
        StartFileTranscriptionButton.IsEnabled = modelsReady && !string.IsNullOrEmpty(_selectedAudioFilePath);
    }

    private void SelectAudioFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Välj ljudfil",
            Filter = "Ljudfiler (*.mp3;*.wav;*.m4a)|*.mp3;*.wav;*.m4a|Alla filer (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            _selectedAudioFilePath = dialog.FileName;
            SelectedAudioFileText.Text = Path.GetFileName(_selectedAudioFilePath);
            SelectedAudioFileText.Foreground = (System.Windows.Media.Brush)FindResource("TextPrimary");
            
            // Enable start button if models are ready
            StartFileTranscriptionButton.IsEnabled = _fileTranscriptionService?.ModelsReady ?? false;
            
            // Hide previous result
            FileTranscriptionResultPanel.Visibility = Visibility.Collapsed;
        }
    }

    private async Task DownloadDiarizationModelsAsync()
    {
        if (_fileTranscriptionService == null)
            return;

        try
        {
            DownloadDiarizationModelsButton.IsEnabled = false;
            DiarizationModelDownloadProgress.Visibility = Visibility.Visible;
            DiarizationModelDownloadProgress.IsIndeterminate = false;

            var progress = new Progress<(int percent, string status)>(p =>
            {
                Dispatcher.Invoke(() =>
                {
                    DiarizationModelDownloadProgress.Value = p.percent;
                    DiarizationModelDownloadStatus.Text = $"{p.status} ({p.percent}%)";
                });
            });

            await _fileTranscriptionService.EnsureModelsAsync(progress);
            
            DiarizationModelDownloadStatus.Text = "Modeller installerade!";
            await Task.Delay(1500);
            UpdateDiarizationModelBanner();
        }
        catch (Exception ex)
        {
            _logger?.Error("Failed to download diarization models", ex);
            DiarizationModelDownloadStatus.Text = $"Fel: {ex.Message}";
            DownloadDiarizationModelsButton.IsEnabled = true;
        }
        finally
        {
            DiarizationModelDownloadProgress.Visibility = Visibility.Collapsed;
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
            FileTranscriptionResultPanel.Visibility = Visibility.Collapsed;
            FileTranscriptionProgressBar.Value = 0;
            FileTranscriptionStatusText.Text = "Startar...";
            FileTranscriptionPercentText.Text = "0%";
            AudioTotalLengthText.Text = audioDuration.ToString(@"mm\:ss");
            TranscriptionElapsedTimeText.Text = "00:00";

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

            // Get optimal threads from config
            int numThreads = CalculateCpuThreads();

            _logger?.Info($"Starting file transcription: {_selectedAudioFilePath} using {numThreads} threads");
            
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
                WpfMessageBox.Show(
                    $"Kunde inte spara filen:\n{ex.Message}",
                    "Fel",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    #endregion
}





