using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WsprPc;

public partial class AboutWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private bool _darkMode;

    public AboutWindow()
    {
        InitializeComponent();

        LinkedInLink.Click += (_, _) => OpenUrl("https://www.linkedin.com/in/olle-soderqvist/");
        WebsiteLink.Click += (_, _) => OpenUrl("https://aiolle.se");
        KbLabbetLink.Click += (_, _) => OpenUrl("https://huggingface.co/KBLab");
        
        CheckUpdatesButton.Click += async (_, _) => {
            if (this.Owner is MainWindow main)
            {
                CheckUpdatesButton.IsEnabled = false;
                CheckUpdatesButton.Content = "Söker...";
                await main.ManualCheckForUpdatesAsync();
                CheckUpdatesButton.IsEnabled = true;
                CheckUpdatesButton.Content = "Sök efter uppdatering";
            }
        };

        CloseButton.Click += (_, _) => Close();
        VersionText.Text = $"Version {GetVersionString()}";

        Loaded += (_, _) =>
        {
            // Owner is set after constructor, check it in Loaded
            if (this.Owner is MainWindow mw)
            {
                _darkMode = mw.DarkModeToggle.IsChecked == true;
                ApplyTheme(_darkMode);
            }
            ApplyTitleBarTheme(_darkMode);
        };
    }

    private void ApplyTitleBarTheme(bool dark)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        int value = dark ? 1 : 0;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
    }

    private void ApplyTheme(bool dark)
    {
        var res = Resources;
        if (dark)
        {
            res["WindowBg"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 23, 42));
            res["CardBg"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 41, 59));
            res["TextPrimary"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(241, 245, 249));
            res["TextSecondary"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(148, 163, 184));
            res["BorderColor"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(51, 65, 85));
        }
        else
        {
            res["WindowBg"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 250, 252));
            res["CardBg"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255));
            res["TextPrimary"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(15, 23, 42));
            res["TextSecondary"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(71, 85, 105));
            res["BorderColor"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(226, 232, 240));
        }
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // Ignore failures to open the browser.
        }
    }

    private static string GetVersionString()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version?.ToString() ?? "okänd";
    }

    private static string ReadBuildId()
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string path = System.IO.Path.Combine(baseDir, "BuildId.txt");
            if (System.IO.File.Exists(path))
                return System.IO.File.ReadAllText(path).Trim();
        }
        catch
        {
        }

        return string.Empty;
    }
}
