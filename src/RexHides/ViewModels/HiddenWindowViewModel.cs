using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RexHides.Models;
using RexHides.Services;

namespace RexHides.ViewModels;

public partial class HiddenWindowViewModel : ObservableObject
{
    private readonly WindowHidingService _hiding;
    private readonly Action<HiddenWindowEntry> _createAutoHideRule;

    public HiddenWindowViewModel(
        HiddenWindowEntry entry,
        WindowHidingService hiding,
        Action<HiddenWindowEntry> createAutoHideRule)
    {
        Entry = entry;
        _hiding = hiding;
        _createAutoHideRule = createAutoHideRule;
    }

    public HiddenWindowEntry Entry { get; }

    public string Title => Entry.Title;

    public string ProcessName => Entry.ProcessName;

    public string HiddenAt => Entry.HiddenAtUtc.ToLocalTime().ToString("HH:mm:ss");

    [RelayCommand]
    private void Restore() => _hiding.Restore(Entry.Handle);

    [RelayCommand]
    private void CloseWindow() => _hiding.CloseWindow(Entry.Handle);

    [RelayCommand]
    private void CreateAutoHideRule() => _createAutoHideRule(Entry);
}
