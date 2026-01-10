using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Imaging;

namespace WsprPc;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        VersionText.Text = $"Version {GetVersionString()}";
        LoadSplashImage();
    }

    private void LoadSplashImage()
    {
        try
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string path = Path.Combine(baseDir, "splash.png");
            if (!File.Exists(path))
            {
                FallbackText.Visibility = Visibility.Visible;
                return;
            }

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            SplashImage.Source = image;
        }
        catch
        {
            FallbackText.Visibility = Visibility.Visible;
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
            string path = Path.Combine(baseDir, "BuildId.txt");
            if (File.Exists(path))
                return File.ReadAllText(path).Trim();
        }
        catch
        {
        }

        return string.Empty;
    }
}
