namespace DevBootstrapper.Services;

/// <summary>Writes installation log entries to a file under %LOCALAPPDATA%\DevBootstrapper\Logs.</summary>
public sealed class LogService : IDisposable
{
    private readonly string _logPath;
    private readonly StreamWriter _writer;
    private readonly object _lock = new();

    public string LogPath => _logPath;

    public LogService()
    {
        string logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DevBootstrapper", "Logs");

        Directory.CreateDirectory(logDir);

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        _logPath = Path.Combine(logDir, $"install_{timestamp}.log");
        _writer = new StreamWriter(_logPath, append: false) { AutoFlush = true };
    }

    public void Log(string message)
    {
        string entry = $"[{DateTime.Now:HH:mm:ss}] {message}";
        lock (_lock)
        {
            _writer.WriteLine(entry);
        }

        Console.WriteLine(entry);
    }

    public void Dispose() => _writer.Dispose();
}
