using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using RexHides.ViewModels;
using RexHides.Win32Native;

namespace RexHides.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        HotkeyBox.SetDisplay(viewModel.HotkeyText);
        HotkeyBox.HotkeyCaptured += (_, hotkey) => viewModel.ApplyHotkey(hotkey);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // Bento background #10131B and text #F2F5FB, as 0x00BBGGRR.
        NativeMethods.UseDarkTitleBar(new WindowInteropHelper(this).Handle, 0x001B1310, 0x00FBF5F2);
    }

    /// <summary>Closing the window keeps the app alive in the tray unless a real exit was requested.</summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!App.IsExitRequested)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }
}
