using System.Windows.Controls;
using System.Windows.Input;
using RexHides.Models;
using RexHides.Win32Native;

namespace RexHides.Views;

/// <summary>Read-only textbox that records a modifier+key combination.</summary>
public partial class HotkeyCaptureBox : UserControl
{
    private string _committedText = string.Empty;

    public HotkeyCaptureBox()
    {
        InitializeComponent();
    }

    public event EventHandler<HotkeyDefinition>? HotkeyCaptured;

    public void SetDisplay(string text)
    {
        _committedText = text;
        CaptureBox.Text = text;
    }

    private void CaptureBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        CaptureBox.Text = "Press a key combination...";
    }

    private void CaptureBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        CaptureBox.Text = _committedText;
    }

    private void CaptureBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        Key key = e.Key == Key.System ? e.SystemKey : e.Key;

        if (key is Key.Escape)
        {
            Keyboard.ClearFocus();
            return;
        }

        // Ignore presses of bare modifier keys; wait for the terminal key.
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.None)
        {
            return;
        }

        uint modifiers = 0;
        ModifierKeys pressed = Keyboard.Modifiers;
        if (pressed.HasFlag(ModifierKeys.Control)) modifiers |= Win32Constants.MOD_CONTROL;
        if (pressed.HasFlag(ModifierKeys.Alt)) modifiers |= Win32Constants.MOD_ALT;
        if (pressed.HasFlag(ModifierKeys.Shift)) modifiers |= Win32Constants.MOD_SHIFT;
        if (pressed.HasFlag(ModifierKeys.Windows)) modifiers |= Win32Constants.MOD_WIN;

        if (modifiers == 0)
        {
            CaptureBox.Text = "Include Ctrl, Alt, Shift or Win...";
            return;
        }

        var hotkey = new HotkeyDefinition
        {
            Modifiers = modifiers,
            VirtualKey = (uint)KeyInterop.VirtualKeyFromKey(key),
        };

        _committedText = hotkey.ToString();
        CaptureBox.Text = _committedText;
        Keyboard.ClearFocus();
        HotkeyCaptured?.Invoke(this, hotkey);
    }
}
