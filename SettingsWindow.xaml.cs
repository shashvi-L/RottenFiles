using System;
using System.Windows;
using System.Windows.Controls;
using RottenFiles.Services;

namespace RottenFiles;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();

        // Load current settings
        ExpiryDaysBox.Text = Config.ExpiryDays.ToString();
        OverripeDaysBox.Text = Config.OverripeDays.ToString();
        FolderTextBox.Text = Config.WatchedFolder;

        foreach (ComboBoxItem item in NotificationModeCombo.Items)
        {
            if (item.Tag.ToString() == Config.NotificationMode.ToString())
            {
                NotificationModeCombo.SelectedItem = item;
                break;
            }
        }

        CustomDaysBox.Text = Config.CustomNotificationDays.ToString();
        DigestTimeBox.Text = Config.DigestTime ?? "09:00";
        StartupCheckBox.IsChecked = StartupManager.IsStartupEnabled();

        NotificationModeCombo.SelectionChanged += (s, e) => UpdatePanelVisibility();
        UpdatePanelVisibility();
    }

    private void UpdatePanelVisibility()
    {
        if (NotificationModeCombo.SelectedItem is ComboBoxItem item)
        {
            var mode = item.Tag.ToString();
            CustomDaysPanel.Visibility = mode == "CustomOffset" ? Visibility.Visible : Visibility.Collapsed;
            DigestTimePanel.Visibility = mode == "DailyDigest" ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Watched Folder",
            InitialDirectory = FolderTextBox.Text
        };

        if (dialog.ShowDialog() == true)
        {
            FolderTextBox.Text = dialog.FolderName;
        }
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(ExpiryDaysBox.Text, out int expiry) || expiry < 1)
        { MessageBox.Show("Expiry days must be a positive number."); return; }
        if (!int.TryParse(OverripeDaysBox.Text, out int overripe) || overripe < 1)
        { MessageBox.Show("Overripe days must be a positive number."); return; }

        Config.ExpiryDays = expiry;
        Config.OverripeDays = overripe;
        if (NotificationModeCombo.SelectedItem is ComboBoxItem modeItem)
            Config.NotificationMode = Enum.Parse<NotificationMode>(modeItem.Tag.ToString()!);
        if (int.TryParse(CustomDaysBox.Text, out int customDays))
            Config.CustomNotificationDays = customDays;
        Config.DigestTime = DigestTimeBox.Text;
        Config.WatchedFolder = FolderTextBox.Text;
        Config.Save();

        StartupManager.SetStartup(StartupCheckBox.IsChecked == true);

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}