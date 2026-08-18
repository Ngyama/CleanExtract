namespace CleanExtract.Core.Logging;

public sealed class NullLog : IAppLog
{
    public static NullLog Instance { get; } = new();

    public void Debug(string message)
    {
    }

    public void Info(string message)
    {
    }

    public void Warn(string message)
    {
    }

    public void Error(string message, Exception? exception = null)
    {
    }
}
