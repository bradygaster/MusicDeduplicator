using NAudio.Wave;

public class Player : IDisposable
{
    private WaveOutEvent? _output;
    private AudioFileReader? _reader;

    public void Play(string path)
    {
        Stop();
        _reader = new AudioFileReader(path);
        _output = new WaveOutEvent();
        _output.Init(_reader);
        _output.Play();
    }

    public void Stop()
    {
        _output?.Stop();
        _reader?.Dispose();
        _output?.Dispose();
        _reader = null;
        _output = null;
    }

    public void Dispose() => Stop();
}
