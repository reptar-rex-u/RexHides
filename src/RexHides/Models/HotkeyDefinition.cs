using System.Text;
using RexHides.Win32Native;

namespace RexHides.Models;

public class HotkeyDefinition
{
    /// <summary>Combination of MOD_ALT / MOD_CONTROL / MOD_SHIFT / MOD_WIN flags.</summary>
    public uint Modifiers { get; set; }

    /// <summary>Win32 virtual-key code.</summary>
    public uint VirtualKey { get; set; }

    public static HotkeyDefinition Default => new()
    {
        Modifiers = Win32Constants.MOD_CONTROL | Win32Constants.MOD_ALT,
        VirtualKey = 0x48, // 'H'
    };

    public override string ToString()
    {
        var sb = new StringBuilder();
        if ((Modifiers & Win32Constants.MOD_WIN) != 0) sb.Append("Win+");
        if ((Modifiers & Win32Constants.MOD_CONTROL) != 0) sb.Append("Ctrl+");
        if ((Modifiers & Win32Constants.MOD_ALT) != 0) sb.Append("Alt+");
        if ((Modifiers & Win32Constants.MOD_SHIFT) != 0) sb.Append("Shift+");
        sb.Append(KeyName(VirtualKey));
        return sb.ToString();
    }

    private static string KeyName(uint vk)
    {
        var key = System.Windows.Input.KeyInterop.KeyFromVirtualKey((int)vk);
        return key == System.Windows.Input.Key.None ? $"0x{vk:X2}" : key.ToString();
    }
}
