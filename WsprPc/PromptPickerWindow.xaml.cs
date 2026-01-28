using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using WsprPc.Models;

namespace WsprPc;

public partial class PromptPickerWindow : Window
{
    public PromptDefinition? SelectedPrompt { get; private set; }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(System.IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    public PromptPickerWindow(IEnumerable<PromptDefinition> prompts)
    {
        InitializeComponent();

        var list = prompts.ToList();
        PromptListBox.ItemsSource = list;
        PromptListBox.DisplayMemberPath = nameof(PromptDefinition.Title);
        if (list.Count > 0)
            PromptListBox.SelectedIndex = 0;

        OkButton.Click += (_, _) =>
        {
            SelectedPrompt = PromptListBox.SelectedItem as PromptDefinition;
            DialogResult = SelectedPrompt != null;
        };

        CancelButton.Click += (_, _) =>
        {
            DialogResult = false;
        };

        Loaded += (_, _) =>
        {
            ApplyDarkMode();
            Activate();
            Topmost = true;
            Focus();
            PromptListBox.Focus();
        };

        PromptListBox.SelectionChanged += (_, _) =>
        {
            if (PromptListBox.SelectedItem is PromptDefinition selected)
            {
                SelectedPrompt = selected;
                DialogResult = true;
            }
        };
    }

    private void ApplyDarkMode()
    {
        var interopHelper = new System.Windows.Interop.WindowInteropHelper(this);
        int useDarkMode = 1;
        DwmSetWindowAttribute(interopHelper.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int));
    }

    private void PromptListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (PromptListBox.SelectedItem is PromptDefinition selected)
        {
            SelectedPrompt = selected;
            DialogResult = true;
        }
    }
}
