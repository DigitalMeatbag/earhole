using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace earhole.Services;

/// <summary>
/// Manages UI notifications, status messages, and fade animations for text elements
/// </summary>
public class UINotificationService
{
    private readonly TextBlock statusText;
    private readonly TextBlock trackInfoText;
    private readonly TextBlock modeInfoText;
    private readonly TextBlock fpsText;
    private readonly Storyboard fadeStoryboard;
    private DispatcherTimer? trackInfoFadeTimer;
    private DispatcherTimer? modeInfoFadeTimer;

    private bool isTrackInfoPersistent = false;

    public bool IsTrackInfoPersistent 
    { 
        get => isTrackInfoPersistent;
        set => isTrackInfoPersistent = value;
    }

    public UINotificationService(
        TextBlock statusText,
        TextBlock trackInfoText,
        TextBlock modeInfoText,
        TextBlock fpsText)
    {
        this.statusText = statusText;
        this.trackInfoText = trackInfoText;
        this.modeInfoText = modeInfoText;
        this.fpsText = fpsText;

        // Setup fade storyboard
        fadeStoryboard = new Storyboard();
        var animation = new DoubleAnimation
        {
            From = 1.0,
            To = 0.0,
            Duration = TimeSpan.FromSeconds(1)
        };
        Storyboard.SetTarget(animation, statusText);
        Storyboard.SetTargetProperty(animation, new PropertyPath(TextBlock.OpacityProperty));
        fadeStoryboard.Children.Add(animation);
    }

    public void ShowStatusMessage(string message)
    {
        statusText.Text = message;
        statusText.Opacity = 1;
        fadeStoryboard.Begin();
    }

    public void ShowLargeMessage(string mainText, string? subText = null, int delaySeconds = 5)
    {
        statusText.Inlines.Clear();
        statusText.Inlines.Add(new System.Windows.Documents.Run(mainText)
        {
            FontSize = 48
        });
        
        if (!string.IsNullOrEmpty(subText))
        {
            statusText.Inlines.Add(new System.Windows.Documents.LineBreak());
            statusText.Inlines.Add(new System.Windows.Documents.Run(subText)
            {
                FontSize = 12
            });
        }
        
        statusText.Opacity = 1;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(delaySeconds) };
        timer.Tick += (s, e) =>
        {
            fadeStoryboard.Begin();
            timer.Stop();
        };
        timer.Start();
    }

    public void ShowTrackInfo(string trackInfo)
    {
        trackInfoText.Text = $"🎵 {trackInfo}";
        
        if (!isTrackInfoPersistent)
        {
            // Fade in
            var fadeIn = new DoubleAnimation
            {
                From = 0.0,
                To = 0.8,
                Duration = TimeSpan.FromSeconds(0.5)
            };
            trackInfoText.BeginAnimation(TextBlock.OpacityProperty, fadeIn);
            
            // Setup fade out after 3 seconds
            StartTrackInfoFadeTimer();
        }
        else
        {
            // Keep persistent display visible
            trackInfoText.Opacity = 0.8;
        }
    }

    public void ShowModeInfo(string emoji, string modeName)
    {
        modeInfoText.Text = $"{emoji} {modeName}";
        
        // Fade in
        var fadeIn = new DoubleAnimation
        {
            From = 0.0,
            To = 0.8,
            Duration = TimeSpan.FromSeconds(0.5)
        };
        modeInfoText.BeginAnimation(TextBlock.OpacityProperty, fadeIn);
        
        // Setup fade out after 3 seconds
        modeInfoFadeTimer?.Stop();
        modeInfoFadeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        modeInfoFadeTimer.Tick += (s, e) =>
        {
            var fadeOut = new DoubleAnimation
            {
                From = 0.8,
                To = 0.0,
                Duration = TimeSpan.FromSeconds(0.5)
            };
            modeInfoText.BeginAnimation(TextBlock.OpacityProperty, fadeOut);
            modeInfoFadeTimer?.Stop();
        };
        modeInfoFadeTimer.Start();
    }

    public void UpdateFpsDisplay(double fps)
    {
        fpsText.Text = $"{fps:F1} fps";
    }

    public void SetFpsVisibility(bool visible)
    {
        fpsText.Opacity = visible ? 0.8 : 0;
        if (!visible)
        {
            fpsText.Text = "";
        }
    }

    public void ToggleTrackInfoPersistence(string currentTrackInfo)
    {
        isTrackInfoPersistent = !isTrackInfoPersistent;
        
        if (isTrackInfoPersistent)
        {
            // Stop any fade animation
            trackInfoFadeTimer?.Stop();
            trackInfoText.BeginAnimation(TextBlock.OpacityProperty, null);
            
            // Show current track info if available
            if (!string.IsNullOrEmpty(currentTrackInfo) && currentTrackInfo != "🎵 Unknown Track")
            {
                trackInfoText.Opacity = 0.8;
            }
            
            ShowStatusMessage("track info: persistent");
        }
        else
        {
            // Switching to temporary mode
            if (!string.IsNullOrEmpty(trackInfoText.Text) && trackInfoText.Text != "🎵 Unknown Track")
            {
                StartTrackInfoFadeTimer();
            }
            else
            {
                var fadeOut = new DoubleAnimation
                {
                    From = trackInfoText.Opacity,
                    To = 0.0,
                    Duration = TimeSpan.FromSeconds(0.5)
                };
                trackInfoText.BeginAnimation(TextBlock.OpacityProperty, fadeOut);
            }
            
            ShowStatusMessage("track info: temporary");
        }
    }

    public void RestoreTrackInfoAfterMenu()
    {
        if (!string.IsNullOrEmpty(trackInfoText.Text))
        {
            if (isTrackInfoPersistent)
            {
                trackInfoText.Opacity = 0.8;
            }
            else
            {
                var fadeIn = new DoubleAnimation
                {
                    From = 0.0,
                    To = 0.8,
                    Duration = TimeSpan.FromSeconds(0.5)
                };
                trackInfoText.BeginAnimation(TextBlock.OpacityProperty, fadeIn);
                StartTrackInfoFadeTimer();
            }
        }
    }

    private void StartTrackInfoFadeTimer()
    {
        trackInfoFadeTimer?.Stop();
        trackInfoFadeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        trackInfoFadeTimer.Tick += (s, e) =>
        {
            var fadeOut = new DoubleAnimation
            {
                From = 0.8,
                To = 0.0,
                Duration = TimeSpan.FromSeconds(0.5)
            };
            trackInfoText.BeginAnimation(TextBlock.OpacityProperty, fadeOut);
            trackInfoFadeTimer?.Stop();
        };
        trackInfoFadeTimer.Start();
    }
}
