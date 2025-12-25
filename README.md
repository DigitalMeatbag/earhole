# earhole

Doin' it to ya in ya earhole.

This is a C# WPF application that captures live audio data from the system loopback using WASAPI and visualizes it in real-time using SkiaSharp, creating a dynamic spectrum analyzer with colorful bars.

## Features

- Real-time audio spectrum visualization with selectable modes
- **Now Playing integration**: Displays current track from Spotify and other media players
- Fullscreen mode (press F or F11)
- Graceful exit with farewell message (press Q or Esc)
- Startup message when audio is detected
- Smooth animations and color transitions
- Mode menu (press ` to toggle)

## Prerequisites

- Windows 10/11 (WASAPI is Windows-specific)
  - **Minimum**: Windows 10 version 1809 (build 17763) or later for media integration
- .NET 6.0 Runtime (for running published version) or .NET 6.0 SDK (for building from source)
- Stereo mix/loopback audio device enabled in Windows sound settings (for capturing system audio)

## Libraries Used

- **NAudio**: For WASAPI loopback audio capture
- **MathNet.Numerics**: For FFT (Fast Fourier Transform) calculations
- **SkiaSharp.Views.WPF**: For high-performance 2D graphics rendering in WPF
- **SkiaSharp.Svg & Svg.Skia**: For SVG rendering support (used in Cold War mode)

## How to Run

### Option 1: Run from Source (Requires .NET SDK)

1. Clone or download the repository
2. Open a terminal in the project directory
3. Run `dotnet run`
4. The application window will open
5. Play audio on your system to see the visualization
6. Use F/F11 for fullscreen, Q/Esc to quit, ` to open the mode menu

### Option 2: Download Pre-built Executable (Standalone)

The repository includes pre-built releases that require no additional installations:

1. Go to the [Releases](https://github.com/DigitalMeatbag/earhole/releases) page
2. Download the latest `earhole-v*.zip` file
3. Extract the ZIP file
4. Run `earhole.exe` directly - no .NET installation required!

**Note**: Releases contain everything needed (~150MB). Just extract and run!

## Controls

- **F** or **F11**: Toggle fullscreen mode
- **Q** or **Esc**: Quit the application
- **`** (backtick): Toggle mode menu to switch visualizers
- **I**: Toggle between temporary (default) and persistent track info display
- **Window Close Button**: Normal window close

## Now Playing Integration

earhole automatically detects and displays track information from media players running on your system:

- **Supported Players**: Works with Spotify, iTunes, MusicBee, VLC, web browsers, and other media apps using Windows Media Session API
- **Smart Detection**: Automatically prioritizes music players over video editors and other non-music apps
- **Spotify Fallback**: If Spotify doesn't expose metadata via the Windows API, earhole automatically reads the window title as a fallback
- **Display Modes**:
  - **Temporary (default)**: Track info fades in when a song starts, displays for 3 seconds, then fades out
  - **Persistent**: Press **I** to toggle; track info remains visible continuously in the top-left corner
- **Automatic Updates**: Track info updates automatically when songs change

**Note**: Track detection works best when only one media player is active. If multiple apps are playing media (e.g., Spotify and a video editor), close or pause non-music apps for best results.

## Visualization Modes

- **🔀 Shuffle** (default): Automatically cycles through all visualizer modes every 30 seconds with true randomization; never shows the same mode twice in a row. Perfect for variety during long listening sessions.
- **📊 Spectrum Bars**: 256-bin spectrum analyzer with ROYGBIV coloring; bars scale with volume and frequency content.
	- ![Spectrum Bars](assets/earhole-spectrum-bars.png)
- **✨ Particles**: Audio-reactive particle field launched from the bottom; color and velocity respond to frequency/intensity.
	- ![Particles](assets/earhole-particles.png)
- **⭕ The Circle**: Circular spectrum visualizer where frequencies ripple outward from center; segments are colored red (expanding), white (stable), or blue (contracting) based on velocity.
	- ![The Circle](assets/earhole-the-circle.png)
- **⚫ Two Circles**: Dual circular visualizers displaying true stereo separation; left channel (left circle) uses red/blue coloring, right channel (right circle) uses green/orange; overlapping segments blend additively to reveal stereo imaging and phase relationships.
	- ![Two Circles](assets/earhole-two-circles.png)
- **🧚 Fairies**: Seven ROYGBIV fairies fly organically across the screen, each tracking a dynamic frequency range; glow intensity reflects audio activity, speed and movement erraticism increase with rising intensity.
	- ![Fairies](assets/earhole-fairies.png)
- **🌊 The Wave**: Dual-channel waveform visualizer with stereo separation; right channel (red) displays upward, left channel (blue) displays downward. Beat detection triggers color change to white and spawns colorful glowing particles at random positions.
	- ![The Wave](assets/earhole-the-wave.png)
- **💥 Cold War**: Interactive geopolitical visualizer displaying an authentic 1980s world map with Soviet (red) and Western (blue) alliance territories. Audio spectrum samples trigger territorial events: left channel dominance creates small defensive explosions, while right channel dominance launches arcing missiles toward opposing territories with large impact explosions. Beat detection doubles explosion brightness for dramatic sync with the music's rhythm.
	- ![Cold War](assets/earhole-cold-war.png)

All modes update in real time; use the mode menu (`) to switch on the fly. When you select shuffle mode, it resumes cycling; when you select a specific mode, it stays on that mode until you change it.

## Building from Source

If you want to modify the code:

1. Install .NET 6.0 SDK
2. Run `dotnet build` to compile
3. Run `dotnet run` to test
4. To create your own published version: `dotnet publish earhole.csproj -c Release -r win-x64 --self-contained -o publish`

### Automated Release Build

For convenience, use the included PowerShell script to automate the entire release process:

```powershell
.\build-release.ps1
```

This script will:
- Build the project in Release mode
- Publish as a self-contained executable
- Create a timestamped ZIP archive (e.g., `earhole-v2025-12-23.zip`)

The ZIP contains the entire publish output (including native dependencies like SkiaSharp). Distribute the ZIP as-is; users should extract and run `earhole.exe` from the extracted folder (do not move the exe out of that folder).

## Troubleshooting

- **No visualization**: Ensure stereo mix/loopback is enabled in Windows Sound settings
- **Audio not detected**: Check that applications are playing audio through the system
- **Performance issues**: The app uses FFT on audio samples; ensure your system can handle real-time processing

## License

See LICENSE file for details.