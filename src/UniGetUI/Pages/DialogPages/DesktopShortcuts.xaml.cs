using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using UniGetUI.Core.Logging;
using UniGetUI.Core.Tools;
using UniGetUI.Interface.Enums;
using UniGetUI.PackageEngine;
using UniGetUI.PackageEngine.Classes.Packages.Classes;
using UniGetUI.PackageEngine.Interfaces;
using UniGetUI.PackageEngine.Managers.WingetManager;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace UniGetUI.Interface
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>

    public sealed partial class DesktopShortcutsManager : Page
    {
        public event EventHandler? Close;
        private ObservableCollection<ShortcutEntry> desktopShortcuts = new ObservableCollection<ShortcutEntry>();

        public DesktopShortcutsManager()
        {
            InitializeComponent();
            DeletableDesktopShortcutsList.ItemsSource = desktopShortcuts;
            DeletableDesktopShortcutsList.DoubleTapped += DeletableDesktopShortcutsList_DoubleTapped;
        }

        public async Task UpdateData()
        {
            desktopShortcuts.Clear();

            foreach (var (shortcutPath, shortcutEnabled) in DesktopShortcutsDatabase.GetDatabase())
            {
                var shortcutEntry = new ShortcutEntry(shortcutPath, shortcutEnabled, desktopShortcuts);
                desktopShortcuts.Add(shortcutEntry);
            }
        }

        private async void DeletableDesktopShortcutsList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (DeletableDesktopShortcutsList.SelectedItem is ShortcutEntry shortcut)
            {
                shortcut.ResetConfiguration();
                desktopShortcuts.Remove(shortcut);
            }
        }

        private void CloseButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            Close?.Invoke(this, EventArgs.Empty);
        }

        private async void YesResetButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            foreach (ShortcutEntry shortcut in desktopShortcuts.ToArray())
            {
                shortcut.ResetConfiguration();
            }
            desktopShortcuts.Clear();
            ConfirmResetFlyout.Hide();
        }

        private void NoResetButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            ConfirmResetFlyout.Hide();
        }
    }

    public class ShortcutEntry
    {
        public string ShortcutPath { get; }
        private bool _enabled;
        public bool Enabled
        {
            get
            {
                return _enabled;
            }
            set
            {
                _enabled = value;
                DesktopShortcutsDatabase.Add(ShortcutPath, _enabled);
            }
        }
        public bool ShortcutExists
        {
            get
            {
                return File.Exists(ShortcutPath);
            }
        }
        private ObservableCollection<ShortcutEntry> List { get; }

        public ShortcutEntry(string shortcutPath, bool enabled, ObservableCollection<ShortcutEntry> list)
        {
            ShortcutPath = shortcutPath;
            Enabled = enabled;
            List = list;
        }

        public void OpenShortcutPath()
        {
            Process.Start("explorer.exe", "/select," + $"\"{ShortcutPath}\"");
        }

        public void ResetConfiguration()
        {
            DesktopShortcutsDatabase.Reset(ShortcutPath);
            List.Remove(this);
        }

        public void Enable()
        {
            Enabled = true;
        }

        public void Disable()
        {
            Enabled = false;
        }
    }
}
