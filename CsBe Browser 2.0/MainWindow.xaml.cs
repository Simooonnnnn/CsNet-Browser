using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes; // Add this line with your other using statements

public static class VisualTreeHelperExtensions
{
    public static IEnumerable<DependencyObject> VisualDescendants(this DependencyObject root)
    {
        if (root == null)
            yield break;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;

            foreach (var descendant in VisualDescendants(child))
                yield return descendant;
        }
    }
}
namespace CsBe_Browser_2._0
{
    public partial class MainWindow : Window
    {
        private readonly List<BrowserTab> _tabs;
        private BrowserTab _currentTab;

        public MainWindow()
        {
            InitializeComponent();
            _tabs = new List<BrowserTab>();
            CreateNewTab();
        }
        private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.CurrentTheme = ThemeManager.CurrentTheme == ThemeManager.Theme.Light
                ? ThemeManager.Theme.Dark
                : ThemeManager.Theme.Light;
        }

        // Add these window control methods
        private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximize();
            }
            else
            {
                this.DragMove();
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximize();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ToggleMaximize()
        {
            var maximizeIcon = MaximizeButton.Content as Path;
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                if (maximizeIcon != null)
                    maximizeIcon.Data = Geometry.Parse("M 0,0 H 10 V 10 H 0 Z");
            }
            else
            {
                this.WindowState = WindowState.Maximized;
                if (maximizeIcon != null)
                    maximizeIcon.Data = Geometry.Parse("M 0,2 H 8 V 10 H 0 Z M 2,0 H 10 V 8 H 8 V 2 H 2 Z");
            }
        }
        private void CreateNewTab()
        {
            try
            {
                var tab = new BrowserTab();

                // Debug checks
                if (tab.TabButton == null)
                    MessageBox.Show("TabButton is null");
                if (tab.WebView == null)
                    MessageBox.Show("WebView is null");
                if (tab.HomePanel == null)
                    MessageBox.Show("HomePanel is null");

                if (TabsPanel == null)
                    MessageBox.Show("TabsPanel is null");
                if (ContentArea == null)
                    MessageBox.Show("ContentArea is null");

                _tabs.Add(tab);

                if (tab.TabButton != null)
                    TabsPanel.Children.Add(tab.TabButton);

                if (tab.WebView != null)
                    ContentArea.Children.Add(tab.WebView);

                if (tab.HomePanel != null)
                    ContentArea.Children.Add(tab.HomePanel);

                tab.TabButton.Click += (s, e) => SwitchToTab(tab);
                tab.CloseRequested += (s, e) => CloseTab(tab);
                SwitchToTab(tab);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating new tab: {ex.Message}\n{ex.StackTrace}");
            }
        }
        private void CloseTab(BrowserTab tab)
        {
            if (_tabs.Count <= 1) return;

            var index = _tabs.IndexOf(tab);
            _tabs.Remove(tab);
            TabsPanel.Children.Remove(tab.TabButton);
            ContentArea.Children.Remove(tab.WebView);
            ContentArea.Children.Remove(tab.HomePanel);

            if (_currentTab == tab)
            {
                var newIndex = Math.Min(index, _tabs.Count - 1);
                SwitchToTab(_tabs[newIndex]);
            }
        }

        // Remove the old CloseTab_Click method as we're now using the event-based approach
        private void SwitchToTab(BrowserTab tab)
        {
            if (_currentTab != null)
            {
                _currentTab.IsSelected = false;
                _currentTab.WebView.Visibility = Visibility.Collapsed;
                _currentTab.HomePanel.Visibility = Visibility.Collapsed;
            }

            _currentTab = tab;
            _currentTab.IsSelected = true;
            AddressBar.Text = _currentTab.WebView.Source?.ToString() ?? string.Empty;

            if (_currentTab.WebView.Source != null)
            {
                _currentTab.WebView.Visibility = Visibility.Visible;
                _currentTab.HomePanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                _currentTab.WebView.Visibility = Visibility.Collapsed;
                _currentTab.HomePanel.Visibility = Visibility.Visible;
            }
        }

        private void NewTab_Click(object sender, RoutedEventArgs e)
        {
            CreateNewTab();
        }

        private void AddressBar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                NavigateToUrl(AddressBar.Text);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTab?.WebView.CanGoBack == true)
                _currentTab.WebView.GoBack();
        }

        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTab?.WebView.CanGoForward == true)
                _currentTab.WebView.GoForward();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTab == null) return;

            // Only reload if we're on an actual webpage (not the home panel)
            if (_currentTab.WebView.Visibility == Visibility.Visible && _currentTab.WebView.Source != null && _currentTab.WebView.Source.ToString() != "about:blank")
            {
                _currentTab.WebView.Reload();
            }
            // If we're on the home panel, just ensure it's visible
            else if (_currentTab.HomePanel.Visibility == Visibility.Visible)
            {
                // No need to reload - home panel is static content
                return;
            }
        }
        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTab != null)
            {
                // Reset custom search state
                if (_currentTab is BrowserTab browserTab)
                {
                    var field = browserTab.GetType().GetField("_isCustomSearch",
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance);
                    if (field != null)
                    {
                        field.SetValue(browserTab, false);
                    }
                }

                // Navigate to about:blank instead of setting Source to null
                _currentTab.WebView.Source = new Uri("about:blank");
                _currentTab.WebView.Visibility = Visibility.Collapsed;

                // Show the home panel
                _currentTab.HomePanel.Visibility = Visibility.Visible;

                // Clear both search boxes
                AddressBar.Text = string.Empty;
                var searchBox = _currentTab.HomePanel.VisualDescendants().OfType<TextBox>().FirstOrDefault();
                if (searchBox != null)
                {
                    searchBox.Text = "Das Web durchsuchen";
                    searchBox.Foreground = Brushes.Gray;
                }

                // Reset the tab title
                var titleBlock = ((DockPanel)_currentTab.TabButton.Content).Children.OfType<TextBlock>().First();
                titleBlock.Text = "Neuer Tab";
                _currentTab.Title = "Neuer Tab";
            }
        }
        private void NavigateToUrl(string input)
        {
            if (_currentTab == null) return;

            try
            {
                input = input.Trim();
                if (!input.StartsWith("http://") && !input.StartsWith("https://"))
                {
                    if (!input.Contains("."))
                    {
                        input = "https://www.google.com/search?q=" + Uri.EscapeDataString(input);
                    }
                    else
                    {
                        input = "https://" + input;
                    }
                }

                _currentTab.WebView.Source = new Uri(input);
                _currentTab.WebView.Visibility = Visibility.Visible;
                _currentTab.HomePanel.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Navigation error: {ex.Message}");
            }
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            var searchBox = (TextBox)sender;
            if (searchBox.Text == "Search the Web")
            {
                searchBox.Text = string.Empty;
                searchBox.Foreground = Brushes.Black;
            }
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var searchBox = (TextBox)sender;
            if (string.IsNullOrWhiteSpace(searchBox.Text))
            {
                searchBox.Text = "Search the Web";
                searchBox.Foreground = Brushes.Gray;
            }
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var searchBox = (TextBox)sender;
                string searchText = searchBox.Text.Trim();
                if (!string.IsNullOrWhiteSpace(searchText) && searchText != "Search the Web")
                {
                    NavigateToUrl(searchText);
                }
            }
        }

        private void GoogleSearchButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateToUrl("https://www.google.com");
        }

        private void CsbeButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateToUrl("https://www.csbe.ch");
        }

        private void AddressBar_TextChanged(object sender, TextChangedEventArgs e)
        {

        }
    }
}
