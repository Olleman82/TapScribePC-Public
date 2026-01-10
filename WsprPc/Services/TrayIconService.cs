using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;

namespace WsprPc.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _statusItem;
    private readonly Window _window;
    private readonly Bitmap _baseBitmap;
    private Icon _baseIcon;
    private Icon _iconIdle;
    private Icon _iconListening;
    private Icon _iconProcessing;
    private Icon _iconError;

    public TrayIconService(Window window, Func<string> statusProvider)
    {
        _window = window;

        _statusItem = new ToolStripMenuItem("Status: Väntar") { Enabled = false };
        var openItem = new ToolStripMenuItem("Öppna inställningar", null, (_, _) => ShowWindow());
        var exitItem = new ToolStripMenuItem("Avsluta", null, (_, _) => System.Windows.Application.Current.Shutdown());

        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(openItem);
        menu.Items.Add(exitItem);

        _baseBitmap = LoadBaseBitmap() ?? SystemIcons.Application.ToBitmap();
        _baseIcon = CreateIconFromBitmap(_baseBitmap);
        _iconIdle = _baseIcon;
        _iconListening = CreateStatusIcon(_baseBitmap, Color.FromArgb(120, 0, 255, 0)); // Green overlay
        _iconProcessing = CreateStatusIcon(_baseBitmap, Color.FromArgb(180, 255, 140, 0)); // Vivid Orange (DarkOrange)
        _iconError = CreateStatusIcon(_baseBitmap, Color.FromArgb(120, 255, 0, 0)); // Red overlay

        _icon = new NotifyIcon
        {
            Icon = _baseIcon,
            Text = "TapScribe PC",
            Visible = true,
            ContextMenuStrip = menu
        };
        _icon.DoubleClick += (_, _) => ShowWindow();

        UpdateStatus(statusProvider());
    }

    public void UpdateStatus(string status)
    {
        var display = string.IsNullOrWhiteSpace(status) ? "Väntar" : status.Trim();
        _statusItem.Text = $"Status: {display}";
        _icon.Text = TruncateTooltip($"TapScribe PC • {display}");

        try
        {
            _icon.Icon = ResolveStatusIcon(display);
        }
        catch
        {
            try { _icon.Icon = _baseIcon; } catch { }
        }
    }

    public void ShowBalloon(string title, string message)
    {
        _icon.BalloonTipTitle = title;
        _icon.BalloonTipText = message;
        _icon.BalloonTipIcon = ToolTipIcon.Info;
        _icon.ShowBalloonTip(2500);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _iconIdle.Dispose();
        if (!ReferenceEquals(_iconListening, _baseIcon)) _iconListening.Dispose();
        if (!ReferenceEquals(_iconProcessing, _baseIcon)) _iconProcessing.Dispose();
        if (!ReferenceEquals(_iconError, _baseIcon)) _iconError.Dispose();
        _baseBitmap.Dispose();
        _icon.Dispose();
    }

    private void ShowWindow()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            _window.Show();
            _window.WindowState = WindowState.Normal;
            _window.Activate();
        });
    }

    private Icon ResolveStatusIcon(string status)
    {
        if (status.Contains("Lyssnar", StringComparison.OrdinalIgnoreCase))
            return _iconListening;
        if (status.Contains("Bearbetar", StringComparison.OrdinalIgnoreCase))
            return _iconProcessing;
        if (status.Contains("Fel", StringComparison.OrdinalIgnoreCase))
            return _iconError;
        return _iconIdle;
    }

    private static Icon CreateStatusIcon(Bitmap baseBitmap, Color statusColor)
    {
        using var bitmap = (Bitmap)baseBitmap.Clone();
        using (var g = Graphics.FromImage(bitmap))
        using (var brush = new SolidBrush(statusColor))
        {
            g.FillRectangle(brush, 0, 0, bitmap.Width, bitmap.Height);
        }
        return CreateIconFromBitmap(bitmap);
    }

    private static Bitmap? LoadBaseBitmap()
    {
        try
        {
            var exePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(exePath))
            {
                var icon = Icon.ExtractAssociatedIcon(exePath);
                if (icon != null)
                    return icon.ToBitmap();
            }
        }
        catch
        {
        }

        return null;
    }

    private static Icon CreateIconFromBitmap(Bitmap bitmap)
    {
        var hIcon = bitmap.GetHicon();
        var icon = (Icon)Icon.FromHandle(hIcon).Clone();
        DestroyIcon(hIcon);
        return icon;
    }

    private static string TruncateTooltip(string text)
    {
        const int maxLen = 63;
        if (string.IsNullOrWhiteSpace(text))
            return "TapScribe PC";
        return text.Length <= maxLen ? text : text.Substring(0, maxLen);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
