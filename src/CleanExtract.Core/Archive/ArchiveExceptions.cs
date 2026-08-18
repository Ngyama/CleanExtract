namespace CleanExtract.Core.Archive;

public class ArchiveException : Exception
{
    public ArchiveException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}

public class PasswordRequiredException : ArchiveException
{
    public PasswordRequiredException(string message = "This archive requires a password.")
        : base(message)
    {
    }
}

public class WrongPasswordException : PasswordRequiredException
{
    public WrongPasswordException(string message = "The password is incorrect.")
        : base(message)
    {
    }
}

public class ArchiveBackendNotFoundException : ArchiveException
{
    public ArchiveBackendNotFoundException(string path)
        : base($"7-Zip executable was not found at \"{path}\". Reinstall Clean Extract or restore the bundled resources folder.")
    {
        Path = path;
    }

    public string Path { get; }
}

public class UnsupportedArchiveException : ArchiveException
{
    public UnsupportedArchiveException(string message)
        : base(message)
    {
    }
}

public class CorruptedArchiveException : ArchiveException
{
    public CorruptedArchiveException(string message)
        : base(message)
    {
    }
}

public class MissingVolumeException : ArchiveException
{
    public MissingVolumeException(string message)
        : base(message)
    {
    }
}

public class OperationCancelledArchiveException : ArchiveException
{
    public OperationCancelledArchiveException()
        : base("The operation was cancelled.")
    {
    }
}
