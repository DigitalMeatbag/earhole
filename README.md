# earhole

Doin' it to ya in ya earhole.

This is a C# WPF application that captures live audio data from the system loopback using WASAPI and visualizes it in real-time using SkiaSharp, creating a dynamic spectrum analyzer with colorful bars.

## Features

- Real-time audio spectrum visualization with selectable modes
- Fullscreen mode (press F or F11)
- Graceful exit with farewell message (press Q or Esc)
- Startup message when audio is detected
- Smooth animations and color transitions
- Mode menu (press ` to toggle)

## Prerequisites

- Windows 10/11 (WASAPI is Windows-specific)
- .NET 6.0 Runtime (for running published version) or .NET 6.0 SDK (for building from source)
- Stereo mix/loopback audio device enabled in Windows sound settings (for capturing system audio)

## Libraries Used

- **NAudio**: For WASAPI loopback audio capture
- **MathNet.Numerics**: For FFT (Fast Fourier Transform) calculations
- **SkiaSharp**: For high-performance 2D graphics rendering

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
- **Window Close Button**: Normal window close

## Visualization Modes

- **Spectrum Bars** (default): 256-bin spectrum analyzer with ROYGBIV coloring; bars scale with volume and frequency content.
	- ![Spectrum Bars](assets/earhole-spectrum-bars.png)
- **Particles**: Audio-reactive particle field launched from the bottom; color and velocity respond to frequency/intensity.
	- ![Particles](assets/earhole-particles.png)
- **The Circle**: Circular spectrum visualizer where frequencies ripple outward from center; segments are colored red (expanding), white (stable), or blue (contracting) based on velocity.
	- ![The Circle](assets/earhole-the-circle.png)
- **Two Circles**: Dual circular visualizers displaying true stereo separation; left channel (left circle) uses red/blue coloring, right channel (right circle) uses green/orange; overlapping segments blend additively to reveal stereo imaging and phase relationships.
	- ![Two Circles](assets/earhole-two-circles.png)
- **Fairies**: Seven ROYGBIV fairies fly organically across the screen, each tracking a dynamic frequency range; glow intensity reflects audio activity, speed and movement erraticism increase with rising intensity.
	- ![Fairies](assets/earhole-fairies.png)
- **The Wave**: Dual-channel waveform visualizer with stereo separation; right channel (red) displays upward, left channel (blue) displays downward. Beat detection triggers color change to white and spawns colorful glowing particles at random positions.
	- ![The Wave](assets/earhole-the-wave.png)

All modes update in real time; use the mode menu (`) to switch on the fly.

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