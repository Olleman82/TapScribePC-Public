using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using WpfMessageBox = System.Windows.MessageBox;
using WsprPc.Models;

namespace WsprPc;

public partial class MemoryEditorWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private bool _darkMode;

    public MemoryItem Result { get; private set; }

    public MemoryEditorWindow(MemoryItem? existing = null)
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            // Owner is set after constructor, check it in Loaded
            if (this.Owner is MainWindow mw)
            {
                _darkMode = mw.DarkModeToggle.IsChecked == true;
            }
            ApplyTitleBarTheme(_darkMode);
        };

        if (existing != null)
        {
            Result = existing;
            TitleBox.Text = existing.Title;
            ContentBox.Text = existing.Content;
        }
        else
        {
            Result = new MemoryItem();
        }

        OkButton.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(TitleBox.Text))
            {
            WpfMessageBox.Show("Ange en titel.");
                return;
            }

            Result.Title = TitleBox.Text.Trim();
            Result.Content = ContentBox.Text.Trim();
            DialogResult = true;
        };

        CancelButton.Click += (_, _) =>
        {
            DialogResult = false;
        };
    }

    private void ApplyTitleBarTheme(bool dark)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        int value = dark ? 1 : 0;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
    }
}
