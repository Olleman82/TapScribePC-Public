using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using WsprPc.Models;
using WsprPc.Stores;
using WpfMessageBox = System.Windows.MessageBox;
using WpfButton = System.Windows.Controls.Button;

namespace WsprPc;

public partial class HistoryWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    private readonly HistoryStore _store;
    private ICollectionView _view;
    private bool _darkMode;

    public HistoryWindow(HistoryStore store)
    {
        InitializeComponent();
        _store = store;

        InitializeView();
        UpdateItemCount();

        // Event handlers
        SearchBox.TextChanged += (_, _) => ApplyFilters();
        DateFilterCombo.SelectionChanged += DateFilterCombo_SelectionChanged;
        FromDatePicker.SelectedDateChanged += (_, _) => ApplyFilters();
        ToDatePicker.SelectedDateChanged += (_, _) => ApplyFilters();
        TypeFilterCombo.SelectionChanged += (_, _) => ApplyFilters();
        SelectAllCheckBox.Checked += (_, _) => HistoryDataGrid.SelectAll();
        SelectAllCheckBox.Unchecked += (_, _) => HistoryDataGrid.UnselectAll();
        DeleteSelectedButton.Click += DeleteSelectedButton_Click;
        ClearAllButton.Click += ClearAllButton_Click;
        CloseButton.Click += (_, _) => Close();

        // Apply dark title bar
        Loaded += (_, _) =>
        {
            if (Owner is MainWindow mw)
            {
                _darkMode = mw.DarkModeToggle.IsChecked == true;
            }
            ApplyTitleBarTheme(_darkMode);
        };
    }

    private void InitializeView()
    {
        var source = new CollectionViewSource { Source = _store.Items };
        _view = source.View;
        _view.Filter = FilterItem;
        HistoryDataGrid.ItemsSource = _view;
    }

    private void ApplyTitleBarTheme(bool dark)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        int value = dark ? 1 : 0;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
    }

    private void DateFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DateFilterCombo.SelectedItem is ComboBoxItem item && item.Tag?.ToString() == "custom")
        {
            CustomDatePanel.Visibility = Visibility.Visible;
            // Set default dates
            if (FromDatePicker.SelectedDate == null)
                FromDatePicker.SelectedDate = DateTime.Today.AddDays(-7);
            if (ToDatePicker.SelectedDate == null)
                ToDatePicker.SelectedDate = DateTime.Today;
        }
        else
        {
            CustomDatePanel.Visibility = Visibility.Collapsed;
        }
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        _view.Refresh();
        UpdateItemCount();
    }

    private bool FilterItem(object obj)
    {
        if (obj is not HistoryItem item)
            return false;

        // Text filter
        string search = SearchBox.Text.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            if (!item.Output.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !item.DateDisplay.Contains(search, StringComparison.OrdinalIgnoreCase) &&
                !item.TimeDisplay.Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // Date filter
        if (DateFilterCombo.SelectedItem is ComboBoxItem dateItem)
        {
            string? tag = dateItem.Tag?.ToString();
            DateTime today = DateTime.Today;

            bool passesDateFilter = tag switch
            {
                "today" => item.Timestamp.Date == today,
                "7days" => item.Timestamp.Date >= today.AddDays(-7),
                "30days" => item.Timestamp.Date >= today.AddDays(-30),
                "custom" => CheckCustomDateRange(item.Timestamp),
                _ => true // "all"
            };

            if (!passesDateFilter)
                return false;
        }

        // Type filter
        if (TypeFilterCombo.SelectedItem is ComboBoxItem typeItem)
        {
            string? tag = typeItem.Tag?.ToString();

            bool passesTypeFilter = tag switch
            {
                "transcription" => item.Type == HistoryItemType.Transcription,
                "ai" => item.Type == HistoryItemType.AI,
                _ => true // "all"
            };

            if (!passesTypeFilter)
                return false;
        }

        return true;
    }

    private bool CheckCustomDateRange(DateTime timestamp)
    {
        DateTime? from = FromDatePicker.SelectedDate;
        DateTime? to = ToDatePicker.SelectedDate;

        if (from.HasValue && timestamp.Date < from.Value.Date)
            return false;
        if (to.HasValue && timestamp.Date > to.Value.Date)
            return false;

        return true;
    }

    private void UpdateItemCount()
    {
        int total = _store.Items.Count;
        int visible = 0;
        foreach (var _ in _view)
            visible++;

        ItemCountText.Text = visible == total
            ? $"{total} poster"
            : $"{visible} av {total} poster";
    }

    private void CopyRowButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton btn && btn.DataContext is HistoryItem item)
        {
            try
            {
                System.Windows.Clipboard.SetText(item.Output);
                btn.Content = "✓";

                var timer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(1)
                };
                timer.Tick += (_, _) =>
                {
                    btn.Content = "📋";
                    timer.Stop();
                };
                timer.Start();
            }
            catch
            {
                WpfMessageBox.Show("Kunde inte kopiera till urklipp.", "Fel", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void DeleteSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = HistoryDataGrid.SelectedItems.Cast<HistoryItem>().ToList();
        if (selected.Count == 0)
        {
            WpfMessageBox.Show("Markera minst en post att ta bort.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = WpfMessageBox.Show(
            $"Vill du ta bort {selected.Count} markerade poster?",
            "Bekräfta borttagning",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _store.Delete(selected.Select(i => i.Id));
            RefreshView();
            SelectAllCheckBox.IsChecked = false;
        }
    }

    private void ClearAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (_store.Items.Count == 0)
        {
            WpfMessageBox.Show("Historiken är redan tom.", "Information", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = WpfMessageBox.Show(
            $"Vill du ta bort ALL historik ({_store.Items.Count} poster)?\n\nDetta kan inte ångras.",
            "Bekräfta borttagning",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            _store.Clear();
            RefreshView();
            SelectAllCheckBox.IsChecked = false;
        }
    }

    private void RefreshView()
    {
        _store.Reload();
        InitializeView();
        ApplyFilters();
    }
}
