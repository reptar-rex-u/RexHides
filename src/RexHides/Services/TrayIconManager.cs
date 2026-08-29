using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;
using RexHides.Features;
using RexHides.Models;
using RexHides.Win32Native;

namespace RexHides.Services;

/// <summary>
/// Owns the app's main tray icon plus one dynamic NotifyIcon per hidden window.
#if GHOME
/// Also owns the persistent Google Home tray icon when G Home mode is enabled.
#endif
/// </summary>
public class TrayIconManager : IDisposable
{
    private readonly WindowHidingService _hiding;
    private readonly SettingsService _settings;
    private readonly ElevationService _elevation;
    private readonly IGHomeModule _gHome;
    private readonly Dictionary<IntPtr, NotifyIcon> _windowIcons = [];
    private readonly Icon _appIcon;
    private readonly bool _useLightTheme = DetectLightTheme();
    private NotifyIcon? _mainIcon;
#if GHOME
    private NotifyIcon? _gHomeIcon;
    private Icon? _gHomeIconImage;
    private static Icon? _gHomeBrandIcon;
#endif

    public TrayIconManager(
        WindowHidingService hiding,
        SettingsService settings,
        ElevationService elevation,
        IGHomeModule gHome)
    {
        _hiding = hiding;
        _settings = settings;
        _elevation = elevation;
        _gHome = gHome;
        _appIcon = CreateAppIcon();

        _hiding.WindowHidden += OnWindowHidden;
        _hiding.WindowRestored += OnWindowRestored;
#if GHOME
        _gHome.StateChanged += SyncGHomeTrayIcon;
#endif
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

#if GHOME
        var gHomeMenu = new ToolStripMenuItem("G Home");
        gHomeMenu.DropDownItems.Add("Show G Home", null, (_, _) => _gHome.Show());
        gHomeMenu.DropDownItems.Add("Hide G Home", null, (_, _) => _gHome.Hide());
        gHomeMenu.DropDownItems.Add(new ToolStripSeparator());
        gHomeMenu.DropDownItems.Add("Close G Home", null, (_, _) => _gHome.CloseWindow());
        menu.Items.Add(gHomeMenu);
        menu.Items.Add(new ToolStripSeparator());
#endif

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

#if GHOME
        SyncGHomeTrayIcon();
#endif
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
#if GHOME
        DisposeGHomeTrayIcon();
#endif

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
#if GHOME
        if (_gHome.IsGHomeWindow(entry.Handle))
        {
            return;
        }
#endif

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
#if GHOME
        if (_gHome.IsGHomeWindow(entry.Handle))
        {
            return;
        }
#endif

        if (_windowIcons.Remove(entry.Handle, out NotifyIcon? icon))
        {
            icon.Visible = false;
            icon.Icon?.Dispose();
            icon.Dispose();
        }
    }

#if GHOME
    private void SyncGHomeTrayIcon()
    {
        if (!_gHome.IsEnabled)
        {
            DisposeGHomeTrayIcon();
            return;
        }

        EnsureGHomeTrayIcon();
        UpdateGHomeTrayPresentation();
    }

    private void EnsureGHomeTrayIcon()
    {
        if (_gHomeIcon != null)
        {
            return;
        }

        var menu = CreateContextMenu();
        menu.Items.Add("Show G Home", null, (_, _) => _gHome.Show());
        menu.Items.Add("Hide G Home", null, (_, _) => _gHome.Hide());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Close G Home", null, (_, _) => _gHome.CloseWindow());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit RexHides", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _gHomeIcon = new NotifyIcon
        {
            Visible = true,
            ContextMenuStrip = menu,
        };
        _gHomeIcon.MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                _gHome.Toggle();
                UpdateGHomeTrayPresentation();
            }
        };
    }

    private void UpdateGHomeTrayPresentation()
    {
        if (_gHomeIcon == null)
        {
            return;
        }

        ReplaceGHomeIconImage();
        string state = _gHome.IsHidden ? "hidden" : _gHome.IsConnected ? "ready" : "waiting";
        _gHomeIcon.Text = Truncate($"G Home ({state})", 63);
    }

    private void ReplaceGHomeIconImage()
    {
        _gHomeIconImage?.Dispose();
        _gHomeIconImage = CreateGHomeTrayIcon();
        if (_gHomeIcon != null)
        {
            _gHomeIcon.Icon = _gHomeIconImage;
        }
    }

    private void DisposeGHomeTrayIcon()
    {
        if (_gHomeIcon != null)
        {
            _gHomeIcon.Visible = false;
            _gHomeIcon.Dispose();
            _gHomeIcon = null;
        }

        _gHomeIconImage?.Dispose();
        _gHomeIconImage = null;
    }

    private Icon CreateGHomeTrayIcon()
    {
        _gHomeBrandIcon ??= LoadEmbeddedGHomeIcon();
        return (Icon)_gHomeBrandIcon.Clone();
    }

    private static Icon LoadEmbeddedGHomeIcon()
    {
        using Stream? stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("RexHides.Assets.ghome.ico");
        if (stream == null)
        {
            return CreateFallbackGHomeIcon();
        }

        try
        {
            return new Icon(stream);
        }
        catch (ArgumentException)
        {
            stream.Position = 0;
            using Image image = Image.FromStream(stream);
            return CreateIconFromImage(image);
        }
    }

    private static Icon CreateIconFromImage(Image image)
    {
        using var bitmap = new Bitmap(image, new Size(32, 32));
        IntPtr hIcon = bitmap.GetHicon();
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

    private static Icon CreateFallbackGHomeIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var back = new SolidBrush(Color.FromArgb(36, 36, 36));
            g.FillEllipse(back, 1, 1, 30, 30);
            using var glyph = new Pen(Color.FromArgb(229, 160, 13), 2.5f)
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
            };
            g.DrawArc(glyph, 8, 8, 16, 16, 200, 220);
            g.DrawLine(glyph, 16, 22, 16, 27);
            g.DrawLine(glyph, 12, 27, 20, 27);
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
#endif

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
