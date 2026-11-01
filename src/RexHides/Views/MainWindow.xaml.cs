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
        StateChanged += (_, _) => UpdateMaximizeState();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => SystemCommands.MinimizeWindow(this);

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            SystemCommands.RestoreWindow(this);
        }
        else
        {
            SystemCommands.MaximizeWindow(this);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void UpdateMaximizeState()
    {
        bool maximized = WindowState == WindowState.Maximized;
        MaximizeButton.Content = maximized ? "\uE923" : "\uE922";
        MaximizeButton.ToolTip = maximized ? "Restore" : "Maximize";

        // With custom chrome, a maximized window extends past the screen by the resize frame.
        RootGrid.Margin = maximized ? SystemParameters.WindowResizeBorderThickness : new Thickness(0);
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
