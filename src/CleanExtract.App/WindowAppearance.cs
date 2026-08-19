using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;

namespace CleanExtract;

internal static class WindowAppearance
{
    public static void Apply(Window window, bool resizable = false)
    {
        WindowChrome.SetWindowChrome(window, new WindowChrome
        {
            CaptionHeight = 40,
            GlassFrameThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            ResizeBorderThickness = resizable ? new Thickness(6) : new Thickness(0),
            UseAeroCaptionButtons = false,
        });

        window.SourceInitialized += (_, _) =>
        {
            if (PresentationSource.FromVisual(window) is not HwndSource source)
                return;
            TrySetCornerPreference(source.Handle);
        };
    }

    private static void TrySetCornerPreference(nint hwnd)
    {
        try
        {
            var preference = DWMWCP_ROUND;
            _ = DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));

            var color = ToColorRef((Color)ColorConverter.ConvertFromString("#FFF4F3F0")!);
            _ = DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref color, sizeof(uint));
        }
        catch
        {
            // Older Windows builds may not support these attributes.
        }
    }

    private static uint ToColorRef(Color color) => (uint)(color.R | (color.G << 8) | (color.B << 16));

    private const int DWMWA_BORDER_COLOR = 34;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref uint attrValue, int attrSize);
}
