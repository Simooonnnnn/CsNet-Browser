using Microsoft.Web.WebView2.Wpf;
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Linq;
using System.Windows.Input;
using System.ComponentModel; // Add this for INotifyPropertyChanged

namespace CsBe_Browser_2._0
{
    public class BrowserTab : INotifyPropertyChanged
    {
        public string Id { get; }
        public string Title { get; set; }
        public Button TabButton { get; }
        public WebView2 WebView { get; }
        public Grid HomePanel { get; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        private TextBox SearchBox { get; set; }
        public event EventHandler CloseRequested;
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public BrowserTab(string title = "New Tab")
        {
            Id = Guid.NewGuid().ToString();
            Title = title;

            // Create a more modern close button
            var closeButton = new Button
            {
                Content = "×",
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(3),
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0),
                Width = 24,
                Height = 24,
                Tag = Id,
                Cursor = Cursors.Hand,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5f6368"))
            };

            closeButton.Click += (s, e) =>
            {
                e.Handled = true;
                CloseRequested?.Invoke(this, EventArgs.Empty);
            };

            // Tab title with better styling
            var titleBlock = new TextBlock
            {
                Text = Title,
                Margin = new Thickness(12, 0, 28, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3c4043"))
            };

            var grid = new Grid();
            grid.Children.Add(titleBlock);
            grid.Children.Add(closeButton);

            // Modern tab button styling
            TabButton = new Button
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f2f2f2")),
                BorderThickness = new Thickness(1, 1, 1, 0),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#dedede")),
                Padding = new Thickness(0, 8, 0, 8),
                Margin = new Thickness(0, 4, -1, 0),
                MinWidth = 180,
                MaxWidth = 220,
                Height = 36,
                Content = grid,
                Tag = Id,
                Cursor = Cursors.Hand
            };

            // Add hover effect
            TabButton.MouseEnter += (s, e) =>
            {
                if (!IsSelected)
                {
                    TabButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e8eaed"));
                }
            };

            TabButton.MouseLeave += (s, e) =>
            {
                if (!IsSelected)
                {
                    TabButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f2f2f2"));
                }
            };

            // Update tab appearance when selected
            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(IsSelected))
                {
                    UpdateTabAppearance();
                }
            };

            WebView = new WebView2 { Visibility = Visibility.Collapsed };
            HomePanel = CreateHomePanel();
            HomePanel.Visibility = Visibility.Visible;

            WebView.NavigationCompleted += (s, e) =>
            {
                Title = WebView.CoreWebView2.DocumentTitle;
                ((Grid)TabButton.Content).Children.OfType<TextBlock>().First().Text =
                    string.IsNullOrEmpty(Title) ? "New Tab" : Title;
            };
        }

        private void UpdateTabAppearance()
        {
            if (IsSelected)
            {
                TabButton.Background = Brushes.White;
                TabButton.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#dadce0"));
                TabButton.BorderThickness = new Thickness(1, 2, 1, 0);
                var titleBlock = ((Grid)TabButton.Content).Children.OfType<TextBlock>().First();
                titleBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#202124"));
            }
            else
            {
                TabButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f2f2f2"));
                TabButton.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#dedede"));
                TabButton.BorderThickness = new Thickness(1, 1, 1, 0);
                var titleBlock = ((Grid)TabButton.Content).Children.OfType<TextBlock>().First();
                titleBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3c4043"));
            }
        }

        private Grid CreateHomePanel()
        {
            var grid = new Grid { Background = Brushes.White };
            var stackPanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, -100, 0, 0)
            };

            var logo = new Image
            {
                Height = 120,
                Width = 320,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(0, 0, 0, 30)
            };

            try
            {
                logo.Source = new BitmapImage(
                    new Uri("pack://application:,,,/CsBe Browser 2.0;component/Resources/CsNet Logo.png"));
            }
            catch
            {
                // Logo not found - can be left empty
            }

            var searchBorder = new Border
            {
                Background = Brushes.White,
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#e8eaed")),
                CornerRadius = new CornerRadius(24),
                Width = 550,
                Margin = new Thickness(0, 0, 0, 20)
            };

            SearchBox = new TextBox();
            SearchBox.SetResourceReference(FrameworkElement.StyleProperty, "SearchTextBox");
            SearchBox.Height = 48;
            SearchBox.FontSize = 16;
            SearchBox.BorderThickness = new Thickness(0);
            SearchBox.Text = "Search the Web";
            SearchBox.Foreground = Brushes.Gray;

            var buttonsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 15, 0, 0)
            };

            var csbeButton = new Button();
            csbeButton.SetResourceReference(FrameworkElement.StyleProperty, "ModernButton");
            csbeButton.Content = "CsBe Startseite";
            csbeButton.Width = 160;
            csbeButton.Height = 40;
            csbeButton.FontSize = 14;
            csbeButton.Margin = new Thickness(0, 0, 15, 0);

            var googleButton = new Button();
            googleButton.SetResourceReference(FrameworkElement.StyleProperty, "ModernButton");
            googleButton.Content = "Google Search";
            googleButton.Width = 160;
            googleButton.Height = 40;
            googleButton.FontSize = 14;

            SearchBox.GotFocus += SearchBox_GotFocus;
            SearchBox.LostFocus += SearchBox_LostFocus;
            SearchBox.KeyDown += SearchBox_KeyDown;
            csbeButton.Click += (s, e) => NavigateToUrl("https://www.csbe.ch");
            googleButton.Click += (s, e) => NavigateToUrl("https://www.google.com");

            searchBorder.Child = SearchBox;
            buttonsPanel.Children.Add(csbeButton);
            buttonsPanel.Children.Add(googleButton);

            stackPanel.Children.Add(logo);
            stackPanel.Children.Add(searchBorder);
            stackPanel.Children.Add(buttonsPanel);

            grid.Children.Add(stackPanel);

            return grid;
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (SearchBox.Text == "Search the Web")
            {
                SearchBox.Text = string.Empty;
                SearchBox.Foreground = Brushes.Black;
            }
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                SearchBox.Text = "Search the Web";
                SearchBox.Foreground = Brushes.Gray;
            }
        }

        private void SearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string searchText = SearchBox.Text.Trim();
                if (!string.IsNullOrWhiteSpace(searchText) && searchText != "Search the Web")
                {
                    NavigateToUrl(searchText);
                }
            }
        }

        private void NavigateToUrl(string input)
        {
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

                WebView.Source = new Uri(input);
                WebView.Visibility = Visibility.Visible;
                HomePanel.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Navigation error: {ex.Message}");
            }
        }
    }
}