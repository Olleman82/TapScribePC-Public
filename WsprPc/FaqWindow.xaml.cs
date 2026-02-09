using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WsprPc;

public partial class FaqWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private bool _darkMode;

    public FaqWindow()
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
    }

    private void ApplyTitleBarTheme(bool dark)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        int value = dark ? 1 : 0;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        string query = SearchBox.Text?.Trim().ToLower() ?? "";

        if (FaqItemsPanel == null) return;

        foreach (UIElement child in FaqItemsPanel.Children)
        {
            if (child is FrameworkElement elem)
            {
                bool match = string.IsNullOrEmpty(query) || CheckMatch(elem, query);
                elem.Visibility = match ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    private bool CheckMatch(FrameworkElement element, string query)
    {
        if (element is System.Windows.Controls.TextBlock tb)
        {
            string text = tb.Text ?? "";
            foreach (var inline in tb.Inlines)
            {
                if (inline is System.Windows.Documents.Run r)
                    text += " " + r.Text;
            }
            return text.ToLower().Contains(query);
        }
        else if (element is System.Windows.Controls.Border b && b.Child is FrameworkElement borderChild)
        {
            return CheckMatch(borderChild, query);
        }
        else if (element is System.Windows.Controls.Panel panel)
        {
            foreach (FrameworkElement panelChild in panel.Children)
            {
                if (CheckMatch(panelChild, query)) return true;
            }
        }
        return false;
    }
}
