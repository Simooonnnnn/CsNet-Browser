using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

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

        private void CreateNewTab()
        {
            var tab = new BrowserTab();
            _tabs.Add(tab);
            TabsPanel.Children.Add(tab.TabButton);
            ContentArea.Children.Add(tab.WebView);
            ContentArea.Children.Add(tab.HomePanel);
            tab.TabButton.Click += (s, e) => SwitchToTab(tab);
            tab.CloseRequested += (s, e) => CloseTab(tab); // Add this line
            SwitchToTab(tab);
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
            _currentTab?.WebView.Reload();
        }

        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTab != null)
            {
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
