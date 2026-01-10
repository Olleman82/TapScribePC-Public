using System.Windows;

namespace WsprPc;

public partial class WelcomeWindow : Window
{
    public bool ShouldDownloadModel { get; private set; }
    public bool AutoStartSelected { get; private set; }

    public WelcomeWindow(string recommendedLabel)
    {
        InitializeComponent();
        RecommendedModelText.Text = $"Vi rekommenderar: {recommendedLabel}.";

        SkipButton.Click += (_, _) =>
        {
            ShouldDownloadModel = false;
            AutoStartSelected = AutoStartCheckBox.IsChecked == true;
            DialogResult = false;
            Close();
        };
        DownloadButton.Click += (_, _) =>
        {
            ShouldDownloadModel = true;
            AutoStartSelected = AutoStartCheckBox.IsChecked == true;
            DialogResult = true;
            Close();
        };
    }
}
