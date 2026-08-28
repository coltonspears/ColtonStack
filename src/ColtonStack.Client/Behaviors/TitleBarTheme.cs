using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ColtonStack.Client.Behaviors;

/// <summary>
/// Attached behavior that themes the native title bar to match the app: dark mode on Windows 10,
/// plus exact caption/border colors on Windows 11. Set <c>TitleBarTheme.IsDark="True"</c> on a
/// Window in XAML — no code-behind, and unsupported OS builds simply ignore the attributes.
/// </summary>
public static partial class TitleBarTheme
{
    private const int UseImmersiveDarkMode = 20; // DWMWA_USE_IMMERSIVE_DARK_MODE (Win10 1809+)
    private const int BorderColor = 34;          // DWMWA_BORDER_COLOR (Win11)
    private const int CaptionColor = 35;         // DWMWA_CAPTION_COLOR (Win11)
    private const int TextColor = 36;            // DWMWA_TEXT_COLOR (Win11)

    // COLORREF is 0x00BBGGRR. These mirror Theme.xaml: Brush.Background and Brush.Text.
    private const int CaptionColorRef = 0x211D1A; // #1A1D21
    private const int TextColorRef = 0xD3D2D1;    // #D1D2D3

    public static readonly DependencyProperty IsDarkProperty = DependencyProperty.RegisterAttached(
        "IsDark", typeof(bool), typeof(TitleBarTheme), new PropertyMetadata(false, OnIsDarkChanged));

    public static bool GetIsDark(Window window) => (bool)window.GetValue(IsDarkProperty);

    public static void SetIsDark(Window window, bool value) => window.SetValue(IsDarkProperty, value);

    private static void OnIsDarkChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not Window window || e.NewValue is not true)
        {
            return;
        }

        if (new WindowInteropHelper(window).Handle is not 0)
        {
            Apply(window);
        }
        else
        {
            window.SourceInitialized += (_, _) => Apply(window);
        }
    }

    private static void Apply(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;

        // Return values are deliberately ignored: each attribute is best-effort per OS build.
        var darkMode = 1;
        _ = DwmSetWindowAttribute(handle, UseImmersiveDarkMode, ref darkMode, sizeof(int));

        var caption = CaptionColorRef;
        _ = DwmSetWindowAttribute(handle, CaptionColor, ref caption, sizeof(int));

        var border = CaptionColorRef;
        _ = DwmSetWindowAttribute(handle, BorderColor, ref border, sizeof(int));

        var text = TextColorRef;
        _ = DwmSetWindowAttribute(handle, TextColor, ref text, sizeof(int));
    }

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
}
