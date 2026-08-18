using System.Globalization;

namespace CleanExtract.Core;

public static class FileSizeFormatter
{
    public static string Format(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";

        double value = bytes;
        string[] units = ["KB", "MB", "GB", "TB"];
        var unit = "KB";
        for (var i = 0; i < units.Length; i++)
        {
            value /= 1024d;
            unit = units[i];
            if (value < 1024d || i == units.Length - 1)
                break;
        }

        var format = value >= 100 ? "0" : value >= 10 ? "0.0" : "0.00";
        return $"{value.ToString(format, CultureInfo.CurrentCulture)} {unit}";
    }
}
