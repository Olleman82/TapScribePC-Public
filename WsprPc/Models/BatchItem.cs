using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WsprPc.Models;

/// <summary>
/// Represents an item in the batch transcription queue.
/// </summary>
public class BatchItem : INotifyPropertyChanged
{
    private string _filePath = "";
    private int _speakerCount; // 0 = Auto
    private BatchStatus _status = BatchStatus.Pending;
    private int _progressPercent;
    private string _statusText = "Väntar";
    private string _result = "";
    private TimeSpan _processingTime;
    private string? _errorMessage;
    private double _diarizationThreshold = 1.15;
    private bool _enablePitchProtection = true;

    public double DiarizationThreshold
    {
        get => _diarizationThreshold;
        set { _diarizationThreshold = value; OnPropertyChanged(); }
    }

    public bool EnablePitchProtection
    {
        get => _enablePitchProtection;
        set { _enablePitchProtection = value; OnPropertyChanged(); }
    }

    public string FilePath
    {
        get => _filePath;
        set { _filePath = value; OnPropertyChanged(); OnPropertyChanged(nameof(FileName)); }
    }

    public string FileName => System.IO.Path.GetFileName(_filePath);

    /// <summary>
    /// Number of expected speakers. 0 = Auto-detect.
    /// </summary>
    public int SpeakerCount
    {
        get => _speakerCount;
        set { _speakerCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(SpeakerCountDisplay)); }
    }

    public string SpeakerCountDisplay => _speakerCount == 0 ? "Auto" : _speakerCount.ToString();

    public BatchStatus Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsRemovable)); OnPropertyChanged(nameof(CanViewResult)); }
    }

    public int ProgressPercent
    {
        get => _progressPercent;
        set { _progressPercent = value; OnPropertyChanged(); }
    }

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    public string Result
    {
        get => _result;
        set { _result = value; OnPropertyChanged(); }
    }

    public TimeSpan ProcessingTime
    {
        get => _processingTime;
        set { _processingTime = value; OnPropertyChanged(); }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set { _errorMessage = value; OnPropertyChanged(); }
    }

    public bool IsRemovable => Status == BatchStatus.Pending;
    public bool CanViewResult => Status == BatchStatus.Completed && !string.IsNullOrEmpty(Result);

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

public enum BatchStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}
