// See https://aka.ms/new-console-template for more information
using WsprPc.Services;

Console.WriteLine("Audio probe: starting capture...");
using var audio = new AudioCaptureService();
try
{
    audio.Start();
    Console.WriteLine("Audio capture apparently started successfully. Press ENTER to stop.");
    Console.ReadLine();
    var samples = audio.StopAndGetPcm16();
    Console.WriteLine($"Captured {samples.Length} samples ({samples.Length / (double)audio.SampleRate:F2}s).");
}
catch (Exception ex)
{
    Console.WriteLine("Audio capture failed: " + ex);
    return 1;
}

return 0;
