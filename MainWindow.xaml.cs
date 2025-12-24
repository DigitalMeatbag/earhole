using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using NAudio.Wave;
using MathNet.Numerics.IntegralTransforms;
using System.Threading;
using System.Numerics;
using SkiaSharp;
using System.Timers;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using earhole.Modes;

namespace earhole;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    static int NextPowerOfTwo(int n)
    {
        int p = 1;
        while (p < n) p <<= 1;
        return p;
    }

    private float[] spectrum = new float[256]; // FFT bins
    private object spectrumLock = new object();
    private EarholeLoopbackCapture capture;
    private Thread captureThread;
    private System.Timers.Timer timer;
    private volatile bool running = true;
    private bool isFullscreen = false;
    private bool audioDetected = false;
    private Storyboard fadeStoryboard;
    private bool isClosing = false;
    private IVisualizerMode currentMode;

    public MainWindow()
    {
        InitializeComponent();

        // Initialize the default visualizer mode
        currentMode = new ParticleMode();

        // Setup fade storyboard
        fadeStoryboard = new Storyboard();
        var animation = new DoubleAnimation
        {
            From = 1.0,
            To = 0.0,
            Duration = TimeSpan.FromSeconds(1)
        };
        Storyboard.SetTarget(animation, StatusText);
        Storyboard.SetTargetProperty(animation, new PropertyPath(TextBlock.OpacityProperty));
        fadeStoryboard.Children.Add(animation);

        SkiaView.PaintSurface += OnPaint;

        capture = new EarholeLoopbackCapture();
        capture.DataAvailable += OnDataAvailable;
        captureThread = new Thread(AudioCapture);
        captureThread.IsBackground = true;
        captureThread.Start();

        // Timer to force repaint
        timer = new System.Timers.Timer(100);
        timer.Elapsed += (s, e) => Dispatcher.BeginInvoke(() => SkiaView.InvalidateVisual());
        timer.Start();

        this.Closing += MainWindow_Closing;
    }

    private void ToggleFullscreen()
    {
        if (isFullscreen)
        {
            WindowStyle = WindowStyle.SingleBorderWindow;
            WindowState = WindowState.Normal;
            ResizeMode = ResizeMode.CanResize;
        }
        else
        {
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
            ResizeMode = ResizeMode.NoResize;
        }
        isFullscreen = !isFullscreen;

        // Show fullscreen status message
        Dispatcher.Invoke(() =>
        {
            StatusText.Text = isFullscreen ? "fullscreen: on" : "fullscreen: off";
            StatusText.Opacity = 1;
            fadeStoryboard.Begin();
        });
    }

    private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!isClosing)
        {
            isClosing = true;
            e.Cancel = true;

            // Show "peace out" message
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = "peace out";
                StatusText.Opacity = 1;
                // Start timer for 5 seconds
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                timer.Tick += (s, e) =>
                {
                    fadeStoryboard.Begin();
                    timer.Stop();
                };
                timer.Start();
            });

            // Start a delay timer for 1 second before shutting down
            var delayTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            delayTimer.Tick += (s, args) =>
            {
                delayTimer.Stop();
                running = false;
                timer.Stop();
                timer.Dispose();
                if (capture != null)
                {
                    capture.DataAvailable -= OnDataAvailable;
                    capture.StopRecording();
                    capture.Dispose();
                }
                Application.Current.Shutdown();
            };
            delayTimer.Start();
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F || e.Key == Key.F11)
        {
            ToggleFullscreen();
            e.Handled = true;
        }
        else if (e.Key == Key.Q || e.Key == Key.Escape)
        {
            this.Close();
            e.Handled = true;
        }
    }

    private void AudioCapture()
    {
        capture.StartRecording();
        while (running)
        {
            Thread.Sleep(100);
        }
        // No need to stop again, already stopped in Closing
    }

    private void OnDataAvailable(object sender, WaveInEventArgs e)
    {
        if (!running) return;

        // Convert to float array
        int sampleCount = e.BytesRecorded / 4; // 32-bit float
        float[] samples = new float[sampleCount];
        System.Buffer.BlockCopy(e.Buffer, 0, samples, 0, e.BytesRecorded);

        if (samples.Any(f => f != 0))
        {
            if (!audioDetected)
            {
                audioDetected = true;
                Dispatcher.Invoke(() =>
                {
                    StatusText.Text = "earhole";
                    StatusText.Opacity = 1;
                    // Start timer for 5 seconds
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
                    timer.Tick += (s, e) =>
                    {
                        fadeStoryboard.Begin();
                        timer.Stop();
                    };
                    timer.Start();
                });
            }
            // Process only if there are non-zero samples
            Console.WriteLine("Received audio data...");
        }
        else
        {
            Console.WriteLine("Silence detected...");
            return; // No valid audio data
        }

        // Take one channel or mix
        float[] mono = new float[sampleCount / 2];
        for (int i = 0; i < mono.Length; i++)
        {
            mono[i] = (samples[i * 2] + samples[i * 2 + 1]) / 2;
        }

        // Ensure length is power of 2 for FFT
        int fftSize = NextPowerOfTwo(Math.Min(mono.Length, 2048)); // Cap at 2048
        if (fftSize > mono.Length) fftSize = mono.Length;
        if (fftSize < 2) return;
        Array.Resize(ref mono, fftSize);

        // FFT
        var complex = mono.Select(x => new Complex(x, 0)).ToArray();
        Fourier.Forward(complex, FourierOptions.NoScaling);

        float maxMag = (float)complex.Max(c => c.Magnitude);
        Console.WriteLine($"FFT done, size: {complex.Length}, max mag: {maxMag}");

        lock (this.spectrumLock)
        {
            for (int i = 0; i < this.spectrum.Length && i < complex.Length / 2; i++)
            {
                this.spectrum[i] = (float)complex[i].Magnitude;
            }
        }

        // Invalidate the view to trigger repaint
        Dispatcher.BeginInvoke(() => SkiaView.InvalidateVisual());
    }

    private void OnPaint(object sender, SkiaSharp.Views.Desktop.SKPaintSurfaceEventArgs e)
    {
        Console.WriteLine("Painting...");

        var canvas = e.Surface.Canvas;
        int width = e.Info.Width;
        int height = e.Info.Height;

        // Use the current mode to render the visualization
        lock (this.spectrumLock)
        {
            currentMode.Render(canvas, width, height, this.spectrum);
        }
    }
}