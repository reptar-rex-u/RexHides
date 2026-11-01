using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Microsoft.Win32;
using RexHides.Models;
using RexHides.Win32Native;

namespace RexHides.Services;

/// <summary>
/// Owns the app's main tray icon plus one dynamic NotifyIcon per hidden window.
/// </summary>
public class TrayIconManager : IDisposable
{
    private readonly WindowHidingService _hiding;
    private readonly SettingsService _settings;
    private readonly ElevationService _elevation;
    private readonly Dictionary<IntPtr, NotifyIcon> _windowIcons = [];
    private readonly Icon _appIcon;
    private readonly bool _useLightTheme = DetectLightTheme();
    private NotifyIcon? _mainIcon;

    public TrayIconManager(
        WindowHidingService hiding,
        SettingsService settings,
        ElevationService elevation)
    {
        _hiding = hiding;
        _settings = settings;
        _elevation = elevation;
        _appIcon = CreateAppIcon();

        _hiding.WindowHidden += OnWindowHidden;
        _hiding.WindowRestored += OnWindowRestored;
    }

    public event EventHandler? OpenSettingsRequested;

    public event EventHandler? ExitRequested;

    public event EventHandler? RestartElevatedRequested;

    public Icon AppIcon => _appIcon;

    public void Initialize()
    {
        var menu = CreateContextMenu();
        menu.Items.Add("Open RexHides", null, (_, _) => OpenSettingsRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Restore All Windows", null, (_, _) => _hiding.RestoreAll());
        menu.Items.Add(new ToolStripSeparator());

        if (!_elevation.IsElevated)
        {
            menu.Items.Add("Restart as Administrator", null,
                (_, _) => RestartElevatedRequested?.Invoke(this, EventArgs.Empty));
            menu.Items.Add(new ToolStripSeparator());
        }

        menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _mainIcon = new NotifyIcon
        {
            Icon = _appIcon,
            Text = _elevation.IsElevated ? "RexHides (Administrator)" : "RexHides",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _mainIcon.DoubleClick += (_, _) => OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    public void ShowBalloon(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
    {
        if (_settings.Current.ShowBalloonTips && _mainIcon != null)
        {
            _mainIcon.ShowBalloonTip(4000, title, message, icon);
        }
    }

    public void Dispose()
    {
        foreach (NotifyIcon icon in _windowIcons.Values)
        {
            icon.Visible = false;
            icon.Icon?.Dispose();
            icon.Dispose();
        }

        _windowIcons.Clear();

        if (_mainIcon != null)
        {
            _mainIcon.Visible = false;
            _mainIcon.Dispose();
            _mainIcon = null;
        }

        _appIcon.Dispose();
    }

    private void OnWindowHidden(HiddenWindowEntry entry)
    {
        IntPtr hwnd = entry.Handle;
        Icon icon = IconExtractor.GetIconForWindow(hwnd, entry.ExePath);

        var menu = CreateContextMenu();
        menu.Items.Add("Restore", null, (_, _) => _hiding.Restore(hwnd));
        menu.Items.Add("Close Window", null, (_, _) => _hiding.CloseWindow(hwnd));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Restore All Windows", null, (_, _) => _hiding.RestoreAll());

        var notifyIcon = new NotifyIcon
        {
            Icon = icon,
            Text = Truncate($"{entry.Title}", 63),
            Visible = true,
            ContextMenuStrip = menu,
        };
        notifyIcon.MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                _hiding.Restore(hwnd);
            }
        };

        _windowIcons[hwnd] = notifyIcon;
    }

    private void OnWindowRestored(HiddenWindowEntry entry)
    {
        if (_windowIcons.Remove(entry.Handle, out NotifyIcon? icon))
        {
            icon.Visible = false;
            icon.Icon?.Dispose();
            icon.Dispose();
        }
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..(max - 1)] + "\u2026";

    private ContextMenuStrip CreateContextMenu() =>
        new()
        {
            Renderer = new TrayMenuRenderer(_useLightTheme),
            ShowImageMargin = false,
        };

    private static bool DetectLightTheme()
    {
        try
        {
            using RegistryKey? personalization = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return personalization?.GetValue("AppsUseLightTheme") is object value
                && Convert.ToInt32(value) != 0;
        }
        catch (Exception)
        {
            return true;
        }
    }

    private sealed class TrayMenuRenderer(bool useLightTheme) : ToolStripProfessionalRenderer(
        useLightTheme ? new ProfessionalColorTable() : new DarkTrayColorTable())
    {
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (!useLightTheme)
            {
                e.TextColor = e.Item.Enabled
                    ? Color.FromArgb(235, 235, 235)
                    : Color.FromArgb(145, 145, 145);
            }

            base.OnRenderItemText(e);
        }
    }

    private sealed class DarkTrayColorTable : ProfessionalColorTable
    {
        private static readonly Color Background = Color.FromArgb(40, 40, 40);
        private static readonly Color Border = Color.FromArgb(80, 80, 80);
        private static readonly Color Selection = Color.FromArgb(60, 60, 60);

        public override Color MenuBorder => Border;
        public override Color MenuItemBorder => Color.FromArgb(95, 95, 95);
        public override Color MenuItemSelected => Selection;
        public override Color MenuItemSelectedGradientBegin => Selection;
        public override Color MenuItemSelectedGradientEnd => Selection;
        public override Color MenuItemPressedGradientBegin => Color.FromArgb(70, 70, 70);
        public override Color MenuItemPressedGradientMiddle => Color.FromArgb(70, 70, 70);
        public override Color MenuItemPressedGradientEnd => Color.FromArgb(70, 70, 70);
        public override Color ToolStripDropDownBackground => Background;
        public override Color ImageMarginGradientBegin => Background;
        public override Color ImageMarginGradientMiddle => Background;
        public override Color ImageMarginGradientEnd => Background;
        public override Color SeparatorDark => Border;
        public override Color SeparatorLight => Color.FromArgb(60, 60, 60);
    }

    /// <summary>Draws the RexHides tray icon at runtime (charcoal disc with a Plex-gold minimize-to-tray glyph).</summary>
    private static Icon CreateAppIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var back = new SolidBrush(Color.FromArgb(36, 36, 36));
            g.FillEllipse(back, 1, 1, 30, 30);

            using var glyph = new Pen(Color.FromArgb(229, 160, 13), 3f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
                LineJoin = LineJoin.Round,
            };

            g.DrawLine(glyph, 16, 6, 16, 18);
            g.DrawLine(glyph, 11, 14, 16, 19);
            g.DrawLine(glyph, 21, 14, 16, 19);
            g.DrawLine(glyph, 8, 23, 8, 26);
            g.DrawLine(glyph, 8, 26, 24, 26);
            g.DrawLine(glyph, 24, 26, 24, 23);
        }

        IntPtr hIcon = bmp.GetHicon();
        try
        {
            using Icon temp = Icon.FromHandle(hIcon);
            return (Icon)temp.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(hIcon);
        }
    }
}
