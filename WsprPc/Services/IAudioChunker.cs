using System;

namespace WsprPc.Services;

public interface IAudioChunker
{
    event Action<short[]>? SegmentReady;
    void AddSamples(short[] samples);
    void Flush();
    void Reset();
}
