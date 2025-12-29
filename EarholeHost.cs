using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using earhole.Services;

namespace earhole;

public static class EarholeHost
{
    public static ServiceProvider BuildHost()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<AudioCaptureService>();
        services.AddSingleton<BeatDetectionService>();
        services.AddSingleton<ModeManagementService>();
        services.AddSingleton<UINotificationServiceFactory>();
        services.AddSingleton<KeyboardCommandHandlerFactory>();
        services.AddSingleton<MediaSessionManager>();
        
        return services.BuildServiceProvider();
    }
}

// Factories for services needing UI elements
public class UINotificationServiceFactory
{
    private readonly IConfiguration _config;
    public UINotificationServiceFactory(IConfiguration config) { _config = config; }
    public UINotificationService Create(System.Windows.Controls.TextBlock statusText,
                                        System.Windows.Controls.TextBlock trackInfoText,
                                        System.Windows.Controls.TextBlock modeInfoText,
                                        System.Windows.Controls.TextBlock fpsText)
    {
        // Optionally use config for fade duration, etc.
        return new UINotificationService(statusText, trackInfoText, modeInfoText, fpsText);
    }
}

public class KeyboardCommandHandlerFactory
{
    public KeyboardCommandHandler Create(ModeManagementService modeService, MediaSessionManager? mediaSessionManager)
    {
        return new KeyboardCommandHandler(modeService, mediaSessionManager);
    }
}
