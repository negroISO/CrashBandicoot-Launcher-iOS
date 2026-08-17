using AVFoundation;
using Foundation;
using RecompOne.Runtime;

namespace CrashBandicoot.IOSRuntime;

internal sealed unsafe class IOSAudioOutput : IDisposable
{
    const int SampleRate = 44100;
    const int MixChunkFrames = 512;

    readonly object _gate = new();
    readonly short[] _stereo = new short[MixChunkFrames * 2];
    readonly float[] _left = new float[MixChunkFrames];
    readonly float[] _right = new float[MixChunkFrames];
    readonly AVAudioSourceNodeRenderHandler _render;

    AVAudioEngine? _engine;
    AVAudioSourceNode? _source;
    Spu? _spu;
    float _volume = 1f;
    bool _running;
    bool _paused;
    bool _failed;

    public static IOSAudioOutput? Current { get; private set; }

    public IOSAudioOutput()
    {
        _render = Render;
        Current = this;
    }

    public void Attach(Spu? spu)
    {
        if (_failed) return;
        lock (_gate) _spu = spu;
        Start();
    }

    public void SetMasterVolume(float volume) => _volume = volume;

    public void PauseOutput()
    {
        lock (_gate)
        {
            _paused = true;
            if (_engine?.Running == true) _engine.Pause();
        }
    }

    public void ResumeOutput()
    {
        lock (_gate)
        {
            _paused = false;
            if (_engine is { } engine && !engine.Running)
            {
                NSError? error;
                engine.StartAndReturnError(out error);
            }
        }
    }

    void Start()
    {
        lock (_gate)
        {
            if (_running || _failed) return;
            try
            {
                var session = AVAudioSession.SharedInstance();
                session.SetCategory(AVAudioSessionCategory.Playback);
                session.SetActive(true);

                var format = new AVAudioFormat(
                    AVAudioCommonFormat.PCMFloat32, SampleRate, 2, false);
        _source = new AVAudioSourceNode(format, _render);
                _engine = new AVAudioEngine();
                _engine.AttachNode(_source);
                _engine.Connect(_source, _engine.MainMixerNode, format);
                _engine.Prepare();
                NSError? error;
                if (!_engine.StartAndReturnError(out error))
                    throw new InvalidOperationException(
                        $"AVAudioEngine failed to start: {error?.LocalizedDescription}");

                _running = true;
                Console.WriteLine("[CrashIOSAudio] AVAudioEngine started");
            }
            catch (Exception error)
            {
                _failed = true;
                _running = false;
                Console.WriteLine($"[CrashIOSAudio] init failed: {error}");
                DisposeEngine();
            }
        }
    }

    int Render(ref bool silence, ref AudioToolbox.AudioTimeStamp timestamp,
        uint frameCount, ref AudioToolbox.AudioBuffers output)
    {
        int frames = checked((int)frameCount);
        if (frames <= 0 || output.Count < 2)
        {
            silence = true;
            return 0;
        }

        var leftBuffer = output[0];
        var rightBuffer = output[1];
        var left = (float*)leftBuffer.Data;
        var right = (float*)rightBuffer.Data;
        int rendered = 0;
        while (rendered < frames)
        {
            int chunk = Math.Min(MixChunkFrames, frames - rendered);
            lock (_gate)
            {
                var spu = _spu;
                if (_paused || spu is null)
                {
                    Array.Clear(_left, 0, chunk);
                    Array.Clear(_right, 0, chunk);
                }
                else
                {
                    spu.Mix(_stereo, chunk);
                    var gain = _volume;
                    for (var index = 0; index < chunk; ++index)
                    {
                        _left[index] = _stereo[index * 2] * gain / 32768f;
                        _right[index] = _stereo[index * 2 + 1] * gain / 32768f;
                    }
                }
            }

            for (var index = 0; index < chunk; ++index)
            {
                left[rendered + index] = _left[index];
                right[rendered + index] = _right[index];
            }
            rendered += chunk;
        }
        silence = false;
        return 0;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _running = false;
            _spu = null;
            DisposeEngine();
        }
        if (ReferenceEquals(Current, this))
            Current = null;
    }

    void DisposeEngine()
    {
        if (_engine is { } engine) engine.Stop();
        _source = null;
        _engine = null;
    }
}
