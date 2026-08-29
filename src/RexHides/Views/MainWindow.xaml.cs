using System.ComponentModel;
using System.Windows;
using RexHides.ViewModels;

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

#if GHOME
        GHomeHost.Content = new GHomeSection();
        GHomeHost.Visibility = Visibility.Visible;
#endif
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
