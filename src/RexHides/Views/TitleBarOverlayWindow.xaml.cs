using System.Windows;
using System.Windows.Interop;
using RexHides.Win32Native;

namespace RexHides.Views;

/// <summary>
/// Tiny borderless topmost window hosting the tray-minimize button that floats
/// over the target window's title bar. WS_EX_NOACTIVATE keeps it from ever
/// stealing focus; WS_EX_TOOLWINDOW keeps it out of Alt-Tab.
/// </summary>
public partial class TitleBarOverlayWindow : Window
{
    public TitleBarOverlayWindow()
    {
        InitializeComponent();
    }

    public event EventHandler? HideRequested;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        long exStyle = NativeMethods.GetWindowLongPtr(hwnd, Win32Constants.GWL_EXSTYLE).ToInt64();
        exStyle |= Win32Constants.WS_EX_NOACTIVATE | Win32Constants.WS_EX_TOOLWINDOW;
        NativeMethods.SetWindowLongPtr(hwnd, Win32Constants.GWL_EXSTYLE, new IntPtr(exStyle));
    }

    private void HideButton_Click(object sender, RoutedEventArgs e)
    {
        HideRequested?.Invoke(this, EventArgs.Empty);
    }
}
