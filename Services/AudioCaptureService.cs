using NAudio.Wave;
using MathNet.Numerics.IntegralTransforms;
using System.Numerics;
using Microsoft.Extensions.Configuration;

namespace earhole.Services;

/// <summary>
/// Handles audio capture from system loopback and FFT spectrum analysis
/// </summary>
public class AudioCaptureService : IDisposable
{
    public int SpectrumResolution => spectrumResolution;
    private readonly int spectrumResolution;
    private readonly WasapiLoopbackCapture capture;
    private readonly Thread captureThread;
    private readonly object spectrumLock = new object();
    private readonly float[] leftSpectrum;
    private readonly float[] rightSpectrum;
    private volatile bool running = true;

    public event EventHandler<SpectrumDataEventArgs>? SpectrumDataAvailable;
    public event EventHandler? AudioDetected;

    private bool audioDetectedFlag = false;

    public AudioCaptureService(Microsoft.Extensions.Configuration.IConfiguration config)
    {
        spectrumResolution = config.GetValue<int>("Audio:SpectrumResolution", 1024);
        leftSpectrum = new float[spectrumResolution];
        rightSpectrum = new float[spectrumResolution];
        capture = new WasapiLoopbackCapture();
        capture.DataAvailable += OnDataAvailable;
        captureThread = new Thread(AudioCapture)
        {
            IsBackground = true
        };
        captureThread.Start();
    }

    private void AudioCapture()
    {
        capture.StartRecording();
        while (running)
        {
            Thread.Sleep(100);
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (!running) return;

        // Convert to float array
        int sampleCount = e.BytesRecorded / 4; // 32-bit float
        float[] samples = new float[sampleCount];
        System.Buffer.BlockCopy(e.Buffer, 0, samples, 0, e.BytesRecorded);

        if (samples.Any(f => f != 0))
        {
            if (!audioDetectedFlag)
            {
                audioDetectedFlag = true;
                AudioDetected?.Invoke(this, EventArgs.Empty);
            }
        }
        else
        {
            return; // No valid audio data
        }

        // Separate left and right channels
        float[] leftChannel = new float[sampleCount / 2];
        float[] rightChannel = new float[sampleCount / 2];
        for (int i = 0; i < leftChannel.Length; i++)
        {
            leftChannel[i] = samples[i * 2];      // Left channel
            rightChannel[i] = samples[i * 2 + 1]; // Right channel
        }

        // Ensure FFT size is sufficient for SPECTRUM_RESOLUTION
        int minFftSize = spectrumResolution * 2;
        int fftSize = NextPowerOfTwo(Math.Max(leftChannel.Length, minFftSize));
        if (fftSize < 2) return;
        Array.Resize(ref leftChannel, fftSize);
        Array.Resize(ref rightChannel, fftSize);

        // FFT for left channel
        var leftComplex = leftChannel.Select(x => new Complex(x, 0)).ToArray();
        Fourier.Forward(leftComplex, FourierOptions.NoScaling);

        // FFT for right channel
        var rightComplex = rightChannel.Select(x => new Complex(x, 0)).ToArray();
        Fourier.Forward(rightComplex, FourierOptions.NoScaling);

        lock (spectrumLock)
        {
            // Clear spectrum arrays first to avoid leftover data
            Array.Clear(leftSpectrum, 0, leftSpectrum.Length);
            Array.Clear(rightSpectrum, 0, rightSpectrum.Length);
            
            int maxBins = Math.Min(leftSpectrum.Length, leftComplex.Length / 2);
            for (int i = 0; i < maxBins; i++)
            {
                leftSpectrum[i] = (float)leftComplex[i].Magnitude;
            }
            
            maxBins = Math.Min(rightSpectrum.Length, rightComplex.Length / 2);
            for (int i = 0; i < maxBins; i++)
            {
                rightSpectrum[i] = (float)rightComplex[i].Magnitude;
            }
        }

        // Raise event with spectrum data
        SpectrumDataAvailable?.Invoke(this, new SpectrumDataEventArgs(leftSpectrum, rightSpectrum));
    }

    public void GetSpectrumData(float[] leftBuffer, float[] rightBuffer)
    {
        if (leftBuffer.Length != spectrumResolution || rightBuffer.Length != spectrumResolution)
        {
            throw new ArgumentException($"Buffers must be size {spectrumResolution}");
        }

        lock (spectrumLock)
        {
            Array.Copy(leftSpectrum, leftBuffer, spectrumResolution);
            Array.Copy(rightSpectrum, rightBuffer, spectrumResolution);
        }
    }

    public void Stop()
    {
        running = false;
        capture.DataAvailable -= OnDataAvailable;
        capture.StopRecording();
    }

    public void Dispose()
    {
        Stop();
        capture.Dispose();
        GC.SuppressFinalize(this);
    }

    private static int NextPowerOfTwo(int n)
    {
        int p = 1;
        while (p < n) p <<= 1;
        return p;
    }
}

public class SpectrumDataEventArgs : EventArgs
{
    public float[] LeftSpectrum { get; }
    public float[] RightSpectrum { get; }

    public SpectrumDataEventArgs(float[] leftSpectrum, float[] rightSpectrum)
    {
        // Create copies to avoid threading issues
        LeftSpectrum = new float[leftSpectrum.Length];
        RightSpectrum = new float[rightSpectrum.Length];
        Array.Copy(leftSpectrum, LeftSpectrum, leftSpectrum.Length);
        Array.Copy(rightSpectrum, RightSpectrum, rightSpectrum.Length);
    }
}
