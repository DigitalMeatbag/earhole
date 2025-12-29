using System.Diagnostics;
using Windows.Media.Control;

namespace earhole;

public class MediaSessionManager : IDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager? sessionManager;
    private GlobalSystemMediaTransportControlsSession? currentSession;
    
    public event EventHandler<MediaTrackInfo>? TrackChanged;
    public event EventHandler<string>? MediaPlayerChanged;
    
    public bool IsMediaPlaying { get; private set; }
    public MediaTrackInfo? CurrentTrack { get; private set; }
    public string? CurrentMediaPlayer { get; private set; }

    public async Task InitializeAsync()
    {
        try
        {
            sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            
            if (sessionManager != null)
            {
                sessionManager.CurrentSessionChanged += OnCurrentSessionChanged;
                await UpdateCurrentSession();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to initialize media session manager: {ex.Message}");
        }
    }

    private async void OnCurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
    {
        await UpdateCurrentSession();
    }

    private async Task UpdateCurrentSession()
    {
        try
        {
            // Unsubscribe from old session
            if (currentSession != null)
            {
                currentSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                currentSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            }

            // Get the best session from all available sessions
            currentSession = await GetBestMediaSession();

            if (currentSession != null)
            {
                System.Diagnostics.Debug.WriteLine($"Session found: {currentSession.SourceAppUserModelId}");
                
                // Subscribe to new session events
                currentSession.MediaPropertiesChanged += OnMediaPropertiesChanged;
                currentSession.PlaybackInfoChanged += OnPlaybackInfoChanged;

                // Get initial media info
                await UpdateMediaInfo();
                UpdatePlaybackInfo();

                // Notify about media player change
                var appName = currentSession.SourceAppUserModelId;
                if (CurrentMediaPlayer != appName)
                {
                    CurrentMediaPlayer = appName;
                    MediaPlayerChanged?.Invoke(this, appName);
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("No active media session found");
                CurrentTrack = null;
                IsMediaPlaying = false;
                CurrentMediaPlayer = null;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to update current session: {ex.Message}");
        }
    }

    private async Task<GlobalSystemMediaTransportControlsSession?> GetBestMediaSession()
    {
        try
        {
            if (sessionManager == null) return null;
            
            var sessions = sessionManager.GetSessions();
            System.Diagnostics.Debug.WriteLine($"Found {sessions.Count} total sessions");
            
            if (sessions.Count == 0) return null;

            // Prioritized music apps
            var musicApps = new[] { "Spotify", "iTunes", "MusicBee", "Foobar", "AIMP", "VLC" };
            
            // Filter out non-music apps (video editors, browsers showing videos, etc.)
            var nonMusicApps = new[] { "Clipchamp", "DaVinci", "Premiere", "Vegas" };
            
            GlobalSystemMediaTransportControlsSession? bestSession = null;
            GlobalSystemMediaTransportControlsSession? spotifySession = null;
            GlobalSystemMediaTransportControlsSession? playingSession = null;
            
            foreach (var session in sessions)
            {
                var appId = session.SourceAppUserModelId;
                System.Diagnostics.Debug.WriteLine($"  - Session: {appId}");
                
                // Skip non-music apps
                if (nonMusicApps.Any(app => appId.Contains(app, StringComparison.OrdinalIgnoreCase)))
                {
                    System.Diagnostics.Debug.WriteLine($"    Skipping non-music app");
                    continue;
                }
                
                // Check if playing
                var playbackInfo = session.GetPlaybackInfo();
                var isPlaying = playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                System.Diagnostics.Debug.WriteLine($"    Playing: {isPlaying}");
                
                // Prioritize Spotify
                if (appId.Contains("Spotify", StringComparison.OrdinalIgnoreCase))
                {
                    spotifySession = session;
                    if (isPlaying) return session; // Return immediately if Spotify is playing
                }
                
                // Track if it's a music app
                if (musicApps.Any(app => appId.Contains(app, StringComparison.OrdinalIgnoreCase)))
                {
                    if (isPlaying && playingSession == null)
                    {
                        playingSession = session;
                    }
                    if (bestSession == null)
                    {
                        bestSession = session;
                    }
                }
                
                // Fallback: any playing session
                if (isPlaying && playingSession == null)
                {
                    playingSession = session;
                }
            }
            
            // Priority: Spotify > playing music app > any music app > any playing session > first session
            var result = spotifySession ?? playingSession ?? bestSession ?? sessionManager.GetCurrentSession();
            if (result != null)
            {
                System.Diagnostics.Debug.WriteLine($"Selected session: {result.SourceAppUserModelId}");
            }
            return result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to get best session: {ex.Message}");
            return sessionManager?.GetCurrentSession();
        }
    }

    private async void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
    {
        await UpdateMediaInfo();
    }

    private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
    {
        UpdatePlaybackInfo();
    }

    private async Task UpdateMediaInfo()
    {
        try
        {
            if (currentSession == null) return;

            var mediaProperties = await currentSession.TryGetMediaPropertiesAsync();
            
            System.Diagnostics.Debug.WriteLine($"Media properties retrieved: {mediaProperties != null}");
            
            if (mediaProperties != null)
            {
                var trackInfo = new MediaTrackInfo
                {
                    Title = mediaProperties.Title,
                    Artist = mediaProperties.Artist,
                    AlbumTitle = mediaProperties.AlbumTitle,
                    AlbumArtist = mediaProperties.AlbumArtist,
                    TrackNumber = mediaProperties.TrackNumber,
                    AlbumTrackCount = mediaProperties.AlbumTrackCount
                };

                System.Diagnostics.Debug.WriteLine($"Track: '{trackInfo.Title}' by '{trackInfo.Artist}'");
                // If we got empty data from Spotify, try window title fallback
                if (string.IsNullOrEmpty(trackInfo.Title) && 
                    CurrentMediaPlayer?.Contains("Spotify", StringComparison.OrdinalIgnoreCase) == true)
                {
                    var spotifyTrack = GetSpotifyTrackFromWindowTitle();
                    if (spotifyTrack != null)
                    {
                        trackInfo = spotifyTrack;
                        System.Diagnostics.Debug.WriteLine($"Using Spotify window title: '{trackInfo.Title}' by '{trackInfo.Artist}'");
                    }
                }
                // Update current track
                var trackChanged = !trackInfo.Equals(CurrentTrack);
                CurrentTrack = trackInfo;
                
                // Always fire event if we have track info (for initial load)
                if (trackChanged || TrackChanged != null)
                {
                    TrackChanged?.Invoke(this, trackInfo);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to update media info: {ex.Message}");
        }
    }

    private void UpdatePlaybackInfo()
    {
        try
        {
            if (currentSession == null) return;

            var playbackInfo = currentSession.GetPlaybackInfo();
            var wasPlaying = IsMediaPlaying;
            IsMediaPlaying = playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
            
            // You could add an event here if you want to track play/pause changes
            if (wasPlaying != IsMediaPlaying)
            {
                System.Diagnostics.Debug.WriteLine($"Playback state changed: {(IsMediaPlaying ? "Playing" : "Paused/Stopped")}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to update playback info: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (currentSession != null)
        {
            currentSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            currentSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
        }

        if (sessionManager != null)
        {
            sessionManager.CurrentSessionChanged -= OnCurrentSessionChanged;
        }
    }

    private MediaTrackInfo? GetSpotifyTrackFromWindowTitle()
    {
        try
        {
            var spotifyProcesses = Process.GetProcessesByName("Spotify");
            foreach (var process in spotifyProcesses)
            {
                try
                {
                    var title = process.MainWindowTitle;
                    
                    // Spotify window title format: "Artist - Song Title"
                    // Empty or "Spotify" means paused/no track
                    if (!string.IsNullOrEmpty(title) && 
                        title != "Spotify" && 
                        title != "Spotify Premium" &&
                        title != "Spotify Free")
                    {
                        var parts = title.Split(new[] { " - " }, 2, StringSplitOptions.None);
                        if (parts.Length == 2)
                        {
                            return new MediaTrackInfo
                            {
                                Artist = parts[0].Trim(),
                                Title = parts[1].Trim()
                            };
                        }
                    }
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to read Spotify window title: {ex.Message}");
        }
        return null;
    }
}

public class MediaTrackInfo
{
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string AlbumTitle { get; set; } = string.Empty;
    public string AlbumArtist { get; set; } = string.Empty;
    public int TrackNumber { get; set; }
    public int AlbumTrackCount { get; set; }

    public override bool Equals(object? obj)
    {
        if (obj is not MediaTrackInfo other) return false;
        
        return Title == other.Title &&
               Artist == other.Artist &&
               AlbumTitle == other.AlbumTitle &&
               TrackNumber == other.TrackNumber;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Title, Artist, AlbumTitle, TrackNumber);
    }

    public override string ToString()
    {
        if (!string.IsNullOrEmpty(Artist) && !string.IsNullOrEmpty(Title))
            return $"{Artist} - {Title}";
        if (!string.IsNullOrEmpty(Title))
            return Title;
        return "Unknown Track";
    }
}
