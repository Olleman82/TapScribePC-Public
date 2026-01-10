using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Microsoft.ML.OnnxRuntime;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using WsprPc.Services;
using WsprPc.Services.Vad;

static string? GetArg(string[] args, string name)
{
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            return args[i + 1];
    }
    return null;
}

static bool HasFlag(string[] args, string name)
{
    return args.Any(arg => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase));
}

static string FindRepoRoot()
{
    string current = Directory.GetCurrentDirectory();
    for (int i = 0; i < 6; i++)
    {
        if (File.Exists(Path.Combine(current, "WsprPc.sln")) || Directory.Exists(Path.Combine(current, "WsprPc")))
            return current;
        var parent = Directory.GetParent(current);
        if (parent == null)
            break;
        current = parent.FullName;
    }
    return Directory.GetCurrentDirectory();
}

static string? FindDefaultAudio(string root)
{
    string debugDir = Path.Combine(root, "debug");
    if (!Directory.Exists(debugDir))
        return null;
    var files = Directory.GetFiles(debugDir, "*.mp3");
    return files.FirstOrDefault();
}

static string? TryReadModelPathFromConfig()
{
    try
    {
        string configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TapScribe", "appsettings.json");
        if (!File.Exists(configPath))
            return null;
        using var stream = File.OpenRead(configPath);
        using var doc = JsonDocument.Parse(stream);
        if (doc.RootElement.TryGetProperty("ModelPath", out var modelProp))
            return modelProp.GetString();
    }
    catch
    {
        return null;
    }

    return null;
}

static float ParseFloat(string? value, float fallback)
{
    if (string.IsNullOrWhiteSpace(value))
        return fallback;
    return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : fallback;
}

static double ParseDouble(string? value, double fallback)
{
    if (string.IsNullOrWhiteSpace(value))
        return fallback;
    return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : fallback;
}

static int ParseInt(string? value, int fallback)
{
    if (string.IsNullOrWhiteSpace(value))
        return fallback;
    return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : fallback;
}

string repoRoot = FindRepoRoot();
string? audioPath = GetArg(args, "--audio") ?? FindDefaultAudio(repoRoot);
if (string.IsNullOrWhiteSpace(audioPath) || !File.Exists(audioPath))
{
    Console.WriteLine("Audio file not found. Use --audio <path>");
    return;
}

string modelPath = GetArg(args, "--vad-model") ?? Path.Combine(repoRoot, "third_party", "silero_vad.onnx");
if (!File.Exists(modelPath))
{
    Console.WriteLine($"VAD model not found: {modelPath}");
    return;
}

string? whisperModelPath = GetArg(args, "--model") ?? TryReadModelPathFromConfig();
if (string.IsNullOrWhiteSpace(whisperModelPath) || !File.Exists(whisperModelPath))
{
    Console.WriteLine("Whisper model not found. Use --model <path>");
    return;
}

Console.WriteLine($"Audio: {audioPath}");
Console.WriteLine($"VAD Model: {modelPath}");
Console.WriteLine($"Whisper Model: {whisperModelPath}");

using (var session = new InferenceSession(modelPath))
{
    Console.WriteLine("Inputs:");
    foreach (var kvp in session.InputMetadata)
        Console.WriteLine($"- {kvp.Key} :: {kvp.Value.ElementType} [{string.Join(",", kvp.Value.Dimensions)}]");
    Console.WriteLine("Outputs:");
    foreach (var kvp in session.OutputMetadata)
        Console.WriteLine($"- {kvp.Key} :: {kvp.Value.ElementType} [{string.Join(",", kvp.Value.Dimensions)}]");
}

float speechThreshold = ParseFloat(GetArg(args, "--threshold"), 0.1f);
int minSpeechMs = ParseInt(GetArg(args, "--min-speech"), 150);
int minSilenceMs = ParseInt(GetArg(args, "--min-silence"), 500);
int speechPadMs = ParseInt(GetArg(args, "--speech-pad"), 400);
double maxSegmentSeconds = ParseDouble(GetArg(args, "--max-seg"), 6.0);
double softGraceSeconds = ParseDouble(GetArg(args, "--soft-grace"), 0.2);
double overlapSeconds = ParseDouble(GetArg(args, "--overlap"), 0.2);
int threads = ParseInt(GetArg(args, "--threads"), Math.Max(1, Environment.ProcessorCount - 2));
int beamSize = ParseInt(GetArg(args, "--beam"), 2);
string language = GetArg(args, "--lang") ?? "sv";
bool transcribe = !HasFlag(args, "--no-transcribe");
bool whole = HasFlag(args, "--whole");

Console.WriteLine($"Settings: threshold={speechThreshold}, minSpeechMs={minSpeechMs}, minSilenceMs={minSilenceMs}, padMs={speechPadMs}, maxSeg={maxSegmentSeconds}, softGrace={softGraceSeconds}, overlap={overlapSeconds}");
Console.WriteLine($"Engine: threads={threads}, beam={beamSize}, lang={language}");

using var reader = new AudioFileReader(audioPath);
ISampleProvider provider = reader;
if (provider.WaveFormat.Channels > 1)
{
    provider = new StereoToMonoSampleProvider(provider)
    {
        LeftVolume = 0.5f,
        RightVolume = 0.5f
    };
}
if (provider.WaveFormat.SampleRate != 16000)
    provider = new WdlResamplingSampleProvider(provider, 16000);

int sampleRate = provider.WaveFormat.SampleRate;
var engine = new WhisperNetEngine
{
    ModelPath = whisperModelPath,
    BeamSize = beamSize,
    CpuThreads = threads,
    Language = language,
    NativeLibraryPath = Path.Combine(repoRoot, "third_party", "whisper.cpp_bin_v1.5.1_x64", "whisper.dll")
};

if (whole)
{
    var all = new List<short>();
    int blockSamples = 800;
    float[] floatBuffer = new float[blockSamples];
    while (true)
    {
        int read = provider.Read(floatBuffer, 0, blockSamples);
        if (read <= 0)
            break;
        for (int i = 0; i < read; i++)
        {
            float sample = Math.Clamp(floatBuffer[i], -1f, 1f);
            all.Add((short)Math.Round(sample * short.MaxValue));
        }
    }

    if (transcribe)
    {
        var sw = Stopwatch.StartNew();
        string text = await engine.TranscribeAsync(all.ToArray(), sampleRate);
        sw.Stop();
        Console.WriteLine($"WHOLE TRANSCRIBE samples={all.Count} time={sw.ElapsedMilliseconds}ms textLen={text.Length}");
    }

    Console.WriteLine("Done.");
    return;
}

var options = new VadChunkerOptions
{
    SpeechThreshold = speechThreshold,
    MinSpeechMs = minSpeechMs,
    MinSilenceMs = minSilenceMs,
    SpeechPadMs = speechPadMs,
    MaxSegmentSeconds = maxSegmentSeconds,
    SoftMaxGraceSeconds = softGraceSeconds,
    OverlapSeconds = overlapSeconds
};

using var chunker = new VadChunker(modelPath, sampleRate, options);
var segments = new List<short[]>();
long processedSamples = 0;
chunker.SegmentEmitted += info =>
{
    double endSec = processedSamples / (double)sampleRate;
    double startSec = endSec - info.SegmentMs / 1000d;
    Console.WriteLine($"SEGMENT {info.Reason} start={startSec:0.00}s end={endSec:0.00}s len={info.SegmentMs:0}ms speech={info.SpeechMs:0}ms silence={info.SilenceMs:0}ms");
};
chunker.SegmentReady += segment => segments.Add(segment);

int blockSamples2 = 800; // 50ms at 16k
float[] floatBuffer2 = new float[blockSamples2];

while (true)
{
    int read = provider.Read(floatBuffer2, 0, blockSamples2);
    if (read <= 0)
        break;

    short[] pcm = new short[read];
    for (int i = 0; i < read; i++)
    {
        float sample = Math.Clamp(floatBuffer2[i], -1f, 1f);
        pcm[i] = (short)Math.Round(sample * short.MaxValue);
    }

    chunker.AddSamples(pcm);
    processedSamples += read;
}

chunker.Flush();
Console.WriteLine($"Segments captured: {segments.Count}");

if (transcribe)
{
    var transcript = new List<string>();
    for (int i = 0; i < segments.Count; i++)
    {
        var sw = Stopwatch.StartNew();
        string text = await engine.TranscribeAsync(segments[i], sampleRate);
        sw.Stop();
        Console.WriteLine($"TRANSCRIBE idx={i} samples={segments[i].Length} time={sw.ElapsedMilliseconds}ms textLen={text.Length}");
        if (!string.IsNullOrWhiteSpace(text))
            transcript.Add(text);
    }

    Console.WriteLine($"Final transcript length: {string.Join(" ", transcript).Length}");
}

Console.WriteLine("Done.");
