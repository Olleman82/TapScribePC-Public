using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace WsprPc.Services.Vad;

public sealed class SileroVadModel : IDisposable
{
    private static readonly ConcurrentDictionary<string, SharedSession> SessionCache = new();
    private readonly InferenceSession _session;
    private readonly string _inputName;
    private readonly string? _sampleRateName;
    private readonly string? _stateHName;
    private readonly string? _stateCName;
    private DenseTensor<float>? _stateH;
    private DenseTensor<float>? _stateC;
    private readonly int _frameSize;
    private readonly int _contextSize;
    private readonly int _inputSize;
    private readonly float[] _context;
    private readonly int _sampleRate;

    public int FrameSize => _frameSize;

    public SileroVadModel(string modelPath, int sampleRate)
    {
        _sampleRate = sampleRate;
        var shared = GetOrCreateSharedSession(modelPath, sampleRate);
        _session = shared.Session;
        _inputName = shared.InputName;
        _sampleRateName = shared.SampleRateName;
        _stateHName = shared.StateHName;
        _stateCName = shared.StateCName;
        _frameSize = shared.FrameSize;
        _contextSize = shared.ContextSize;
        _inputSize = shared.InputSize;
        _context = new float[_contextSize];

        if (shared.StateHShape != null)
            _stateH = new DenseTensor<float>(shared.StateHShape);
        if (shared.StateCShape != null)
            _stateC = new DenseTensor<float>(shared.StateCShape);
    }

    public float Predict(float[] frame)
    {
        if (frame.Length != _frameSize)
            throw new ArgumentException($"Silero VAD expects frame size {_frameSize}.", nameof(frame));

        var inputs = new List<NamedOnnxValue>();
        var inputData = new float[_inputSize];
        if (_contextSize > 0)
            Array.Copy(_context, 0, inputData, 0, _contextSize);
        Array.Copy(frame, 0, inputData, _contextSize, _frameSize);
        var inputTensor = new DenseTensor<float>(inputData, new[] { 1, _inputSize });
        inputs.Add(NamedOnnxValue.CreateFromTensor(_inputName, inputTensor));

        if (_sampleRateName != null)
            inputs.Add(CreateSampleRateValue(_sampleRateName));

        if (_stateHName != null && _stateH != null)
            inputs.Add(NamedOnnxValue.CreateFromTensor(_stateHName, _stateH));
        if (_stateCName != null && _stateC != null)
            inputs.Add(NamedOnnxValue.CreateFromTensor(_stateCName, _stateC));

        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _session.Run(inputs);

        float probability = ExtractProbability(results);
        UpdateState(results);
        UpdateContext(inputData);
        return probability;
    }

    public void Reset()
    {
        if (_stateH != null)
            _stateH = new DenseTensor<float>(_stateH.Dimensions.ToArray());
        if (_stateC != null)
            _stateC = new DenseTensor<float>(_stateC.Dimensions.ToArray());
        Array.Clear(_context, 0, _context.Length);
    }

    public void Dispose()
    {
        // Session is shared for process lifetime.
    }

    private static SharedSession GetOrCreateSharedSession(string modelPath, int sampleRate)
    {
        string key = $"{modelPath}|{sampleRate}";
        return SessionCache.GetOrAdd(key, _ => CreateSharedSession(modelPath, sampleRate));
    }

    private static SharedSession CreateSharedSession(string modelPath, int sampleRate)
    {
        using var options = new SessionOptions
        {
            InterOpNumThreads = 1,
            IntraOpNumThreads = 1,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL
        };
        options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
        var session = new InferenceSession(modelPath, options);
        var inputMeta = session.InputMetadata;
        var floatInput = inputMeta.FirstOrDefault(kvp =>
            kvp.Value.ElementType == typeof(float) && kvp.Value.Dimensions.Length >= 2);

        if (string.IsNullOrWhiteSpace(floatInput.Key))
            throw new InvalidOperationException("Silero VAD model has no float input.");

        string inputName = inputMeta.ContainsKey("input") ? "input" : floatInput.Key;
        int frameSize = sampleRate switch
        {
            16000 => 512,
            8000 => 256,
            _ => throw new InvalidOperationException($"Unsupported sample rate {sampleRate}. Silero VAD expects 8000 or 16000 Hz.")
        };
        int contextSize = sampleRate == 16000 ? 64 : 32;
        int inputSize = ResolveInputSize(floatInput.Value.Dimensions);
        if (inputSize <= 0)
            inputSize = frameSize;
        if (inputSize == frameSize)
            contextSize = 0;

        string? sampleRateName = null;
        if (inputMeta.ContainsKey("sr"))
            sampleRateName = "sr";
        else if (inputMeta.ContainsKey("sample_rate"))
            sampleRateName = "sample_rate";

        string? stateHName = null;
        string? stateCName = null;
        int[]? stateHShape = null;
        int[]? stateCShape = null;
        if (inputMeta.TryGetValue("h", out var hMeta))
        {
            stateHName = "h";
            stateHShape = hMeta.Dimensions.Select(d => d > 0 ? d : 1).ToArray();
        }
        if (inputMeta.TryGetValue("c", out var cMeta))
        {
            stateCName = "c";
            stateCShape = cMeta.Dimensions.Select(d => d > 0 ? d : 1).ToArray();
        }

        return new SharedSession(
            session,
            inputName,
            sampleRateName,
            stateHName,
            stateCName,
            stateHShape,
            stateCShape,
            frameSize,
            contextSize,
            inputSize);
    }

    private sealed record SharedSession(
        InferenceSession Session,
        string InputName,
        string? SampleRateName,
        string? StateHName,
        string? StateCName,
        int[]? StateHShape,
        int[]? StateCShape,
        int FrameSize,
        int ContextSize,
        int InputSize);

    private static int ResolveInputSize(IReadOnlyList<int> dimensions)
    {
        if (dimensions.Count == 0)
            return 0;
        int last = dimensions[^1];
        return last > 0 ? last : 0;
    }

    private NamedOnnxValue CreateSampleRateValue(string inputName)
    {
        Type type = _session.InputMetadata[inputName].ElementType;
        if (type == typeof(float))
        {
            var tensor = new DenseTensor<float>(new[] { (float)_sampleRate }, new[] { 1 });
            return NamedOnnxValue.CreateFromTensor(inputName, tensor);
        }

        if (type == typeof(int))
        {
            var tensor = new DenseTensor<int>(new[] { _sampleRate }, new[] { 1 });
            return NamedOnnxValue.CreateFromTensor(inputName, tensor);
        }

        var longTensor = new DenseTensor<long>(new long[] { _sampleRate }, new[] { 1 });
        return NamedOnnxValue.CreateFromTensor(inputName, longTensor);
    }

    private static float ExtractProbability(IEnumerable<DisposableNamedOnnxValue> results)
    {
        foreach (var result in results)
        {
            if (result.Value is DenseTensor<float> tensor)
                return tensor.GetValue(0);
        }

        return 0f;
    }

    private void UpdateState(IEnumerable<DisposableNamedOnnxValue> results)
    {
        foreach (var result in results)
        {
            if (_stateHName != null && (string.Equals(result.Name, "hn", StringComparison.OrdinalIgnoreCase)
                                        || string.Equals(result.Name, _stateHName, StringComparison.OrdinalIgnoreCase)))
                _stateH = CloneTensor(result.AsTensor<float>());
            if (_stateCName != null && (string.Equals(result.Name, "cn", StringComparison.OrdinalIgnoreCase)
                                        || string.Equals(result.Name, _stateCName, StringComparison.OrdinalIgnoreCase)))
                _stateC = CloneTensor(result.AsTensor<float>());
        }
    }

    private void UpdateContext(float[] inputData)
    {
        if (_contextSize == 0)
            return;

        Array.Copy(inputData, inputData.Length - _contextSize, _context, 0, _contextSize);
    }

    private static DenseTensor<float> CloneTensor(Tensor<float> tensor)
    {
        float[] data = tensor.ToArray();
        return new DenseTensor<float>(data, tensor.Dimensions.ToArray());
    }
}
