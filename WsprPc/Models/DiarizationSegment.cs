using System;

namespace WsprPc.Models;

/// <summary>
/// Represents a segment of audio with speaker identification.
/// </summary>
public record DiarizationSegment(
    int SpeakerId,
    TimeSpan Start,
    TimeSpan End,
    short[]? AudioData = null,
    string? TranscribedText = null
)
{
    /// <summary>
    /// Duration of the segment.
    /// </summary>
    public TimeSpan Duration => End - Start;
    
    /// <summary>
    /// Creates a copy with transcribed text.
    /// </summary>
    public DiarizationSegment WithText(string text) => 
        this with { TranscribedText = text };
}
