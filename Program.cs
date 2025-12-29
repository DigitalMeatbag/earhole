namespace earhole;

public class Program
{
    [STAThread]
    public static void Main()
    {
        // Build DI host before creating MainWindow
        var provider = EarholeHost.BuildHost();
        var app = new App();
        var mainWindow = new MainWindow();
        app.MainWindow = mainWindow;
        mainWindow.Show();
        app.Run();
    }
}