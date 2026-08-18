namespace CleanExtract.Core.Logging;

public interface IAppLog
{
    void Debug(string message);

    void Info(string message);

    void Warn(string message);

    void Error(string message, Exception? exception = null);
}
