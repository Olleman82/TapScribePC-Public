using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using WsprPc.Models;

namespace WsprPc;

public partial class PromptPickerWindow : Window
{
    public PromptDefinition? SelectedPrompt { get; private set; }

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

    private void PromptListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (PromptListBox.SelectedItem is PromptDefinition selected)
        {
            SelectedPrompt = selected;
            DialogResult = true;
        }
    }
}
