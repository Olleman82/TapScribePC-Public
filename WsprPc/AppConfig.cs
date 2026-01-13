using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace WsprPc;

public sealed class AppConfig
{
    private const string DefaultUpdateRepoOwner = "Olleman82";
    private const string DefaultUpdateRepoName = "TapScribePC-Public";
    public const int CurrentSettingsVersion = 6;

    public int SettingsVersion { get; set; } = CurrentSettingsVersion;
    public string? WhisperCliPath { get; set; }
    public string? ModelPath { get; set; }
    public string? ModelDir { get; set; }
    public string? SelectedModel { get; set; }
    public string? LogDir { get; set; }
    public string DirectHotkey { get; set; } = "F8";
    public string AiHotkey { get; set; } = "F9";
    public bool AiUseDefaultPrompt { get; set; }
    public bool AiUseAutoPrompt { get; set; }
    public bool AutoPasteEnabled { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool UpdateCheckEnabled { get; set; } = true;
    public string? UpdateRepoOwner { get; set; } = DefaultUpdateRepoOwner;
    public string? UpdateRepoName { get; set; } = DefaultUpdateRepoName;
    public DateTimeOffset? LastUpdateCheckUtc { get; set; }
    public string? DefaultPromptId { get; set; }
    public string? LastPromptId { get; set; }
    public string? GeminiApiKey { get; set; }
    public string? OpenAiApiKey { get; set; }
    public bool AllowEnvKeys { get; set; }
    public bool ShowTrayPinHint { get; set; } = true;
    public double SilenceThreshold { get; set; } = 0.05;
    public double SilenceDurationSeconds { get; set; } = 1.0;
    public bool HasSeenWelcome { get; set; }
    public bool AutoTuneCompleted { get; set; }
    public int? OptimalThreads { get; set; }
    public int? ManualThreads { get; set; }
    public bool DarkMode { get; set; }
    public bool EnableVad { get; set; } = true;
    
    // Diarization settings
    public string? SherpaModelsPath { get; set; }
    public bool SherpaModelsDownloaded { get; set; }

    public static AppConfig Load(string path)
    {
        if (!File.Exists(path))
            return new AppConfig();

        string json = File.ReadAllText(path);
        var options = CreateJsonOptions();

        try
        {
            return JsonSerializer.Deserialize<AppConfig>(json, options) ?? new AppConfig();
        }
        catch (JsonException)
        {
            string repaired = RepairConfigJson(json);
            try
            {
                return JsonSerializer.Deserialize<AppConfig>(repaired, options) ?? new AppConfig();
            }
            catch
            {
                return new AppConfig();
            }
        }
    }

    public bool EnsureDefaultsAndMigrate()
    {
        bool changed = false;

        if (string.IsNullOrWhiteSpace(UpdateRepoOwner))
        {
            UpdateRepoOwner = DefaultUpdateRepoOwner;
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(UpdateRepoName))
        {
            UpdateRepoName = DefaultUpdateRepoName;
            changed = true;
        }

        if (SettingsVersion <= 0)
        {
            SettingsVersion = CurrentSettingsVersion;
            changed = true;
        }

        if (SettingsVersion < 2)
        {
            ShowTrayPinHint = true;
            SettingsVersion = 2;
            changed = true;
        }

        if (SettingsVersion < 3)
        {
            HasSeenWelcome = false;
            AutoTuneCompleted = false;
            OptimalThreads = null;
            SettingsVersion = 3;
            changed = true;
        }

        if (SettingsVersion < 4)
        {
            if (SilenceThreshold < 0.02)
            {
                SilenceThreshold = 0.05;
                changed = true;
            }
            SettingsVersion = 4;
            changed = true;
        }

        if (SettingsVersion < 5)
        {
            EnableVad = true;
            SettingsVersion = 5;
            changed = true;
        }

        if (SettingsVersion < CurrentSettingsVersion)
        {
            SettingsVersion = CurrentSettingsVersion;
            changed = true;
        }

        if (SilenceThreshold <= 0)
        {
            SilenceThreshold = 0.05;
            changed = true;
        }

        if (SilenceDurationSeconds <= 0)
        {
            SilenceDurationSeconds = 1.0;
            changed = true;
        }

        return changed;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };
        options.Converters.Add(new NullableDateTimeOffsetConverter());
        return options;
    }

    private static string RepairConfigJson(string json)
    {
        string result = json;
        var match = Regex.Match(result, "\"LastUpdateCheckUtc\"\\s*:\\s*\"([^\"]*)\"");
        if (match.Success)
        {
            string value = match.Groups[1].Value;
            if (!DateTimeOffset.TryParse(value, out _))
            {
                result = Regex.Replace(result, "\"LastUpdateCheckUtc\"\\s*:\\s*\"[^\"]*\"", "\"LastUpdateCheckUtc\": null");
            }
        }

        return result;
    }

    private sealed class NullableDateTimeOffsetConverter : JsonConverter<DateTimeOffset?>
    {
        public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            if (reader.TokenType == JsonTokenType.String)
            {
                string? value = reader.GetString();
                if (string.IsNullOrWhiteSpace(value))
                    return null;

                if (DateTimeOffset.TryParse(value, out var parsed))
                    return parsed;

                return null;
            }

            return reader.GetDateTimeOffset();
        }

        public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
                writer.WriteStringValue(value.Value);
            else
                writer.WriteNullValue();
        }
    }

    public void Save(string path)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string json = JsonSerializer.Serialize(this, options);
        File.WriteAllText(path, json);
    }
}
