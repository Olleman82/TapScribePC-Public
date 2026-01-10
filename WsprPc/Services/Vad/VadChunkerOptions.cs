namespace WsprPc.Services.Vad;

public sealed class VadChunkerOptions
{
    public float SpeechThreshold { get; set; } = 0.5f;
    public int MinSpeechMs { get; set; } = 250;
    public int MinSilenceMs { get; set; } = 2000;
    public int SpeechPadMs { get; set; } = 400;
    public double MaxSegmentSeconds { get; set; } = 10.0;
    public double SoftMaxGraceSeconds { get; set; } = 0.4;
    public double OverlapSeconds { get; set; } = 0.25;
}
