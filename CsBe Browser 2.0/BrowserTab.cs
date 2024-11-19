using Microsoft.Web.WebView2.Wpf;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Linq;
using System.Windows.Input;
using System.ComponentModel;
using HtmlAgilityPack;
using System.Collections.Generic;
using System.Net;

namespace CsBe_Browser_2._0
{
    public class BrowserTab : INotifyPropertyChanged
    {
        private readonly HttpClient _httpClient = new HttpClient();
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
        private async Task PerformCsNetSearch(string query)
        {
            try
            {
                if (WebView.CoreWebView2 == null)
                {
                    await WebView.EnsureCoreWebView2Async();
                }

                if (string.IsNullOrWhiteSpace(query) || query == "Search the Web")
                {
                    MessageBox.Show("Please enter a search query.");
                    return;
                }

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");

                var searchUrl = $"https://duckduckgo.com/html/?q={Uri.EscapeDataString(query)}";
                var response = await _httpClient.GetStringAsync(searchUrl);

                var doc = new HtmlDocument();
                doc.LoadHtml(response);

                var links = doc.DocumentNode
                    .SelectNodes("//div[contains(@class, 'result__body')]//a[@class='result__url']")
                    ?.Select(a => "https://" + a.InnerText.Trim())
                    .Where(url => !string.IsNullOrWhiteSpace(url))
                    .Take(5)
                    .ToList();

                if (links == null || !links.Any())
                {
                    MessageBox.Show("No search results found.");
                    return;
                }

                var progressMessage = $@"
        <html><body style='font-family: Arial; padding: 20px;'>
            <h2>Analyzing results for: {WebUtility.HtmlEncode(query)}</h2>
            <p>Please wait...</p>
        </body></html>";
                WebView.NavigateToString(progressMessage);
                WebView.Visibility = Visibility.Visible;
                HomePanel.Visibility = Visibility.Collapsed;

                var contentPieces = new List<string>();
                var sources = new HashSet<string>();
                var keyPoints = new HashSet<string>();

                foreach (var link in links)
                {
                    try
                    {
                        var pageContent = await _httpClient.GetStringAsync(link);
                        var pageDoc = new HtmlDocument();
                        pageDoc.LoadHtml(pageContent);

                        foreach (var node in pageDoc.DocumentNode.SelectNodes("//script|//style")?.ToList() ?? new List<HtmlNode>())
                        {
                            node.Remove();
                        }

                        sources.Add(new Uri(link).Host);

                        // Extract main content paragraphs
                        var paragraphs = pageDoc.DocumentNode
                            .SelectNodes("//p[string-length(text()) > 50]|//article//p|//main//p")
                            ?.Select(node => node.InnerText.Trim())
                            .Where(text =>
                                text.Length > 50 &&
                                text.Length < 500 &&
                                !text.ToLower().Contains("cookie") &&
                                !text.ToLower().Contains("copyright") &&
                                !text.ToLower().Contains("privacy"))
                            .ToList() ?? new List<string>();

                        // Extract key points from lists
                        var listItems = pageDoc.DocumentNode
                            .SelectNodes("//li")
                            ?.Select(node => node.InnerText.Trim())
                            .Where(text =>
                                text.Length > 20 &&
                                text.Length < 200 &&
                                !text.ToLower().Contains("cookie"))
                            .ToList() ?? new List<string>();

                        contentPieces.AddRange(paragraphs);
                        foreach (var item in listItems)
                        {
                            keyPoints.Add(CleanText(item));
                        }
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }

                if (!contentPieces.Any())
                {
                    MessageBox.Show("Could not extract meaningful information from the sources.");
                    return;
                }

                var summary = GenerateSummary(contentPieces);
                var htmlContent = GenerateFormattedOutput(query, summary, keyPoints, sources);
                WebView.NavigateToString(htmlContent);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Search error: {ex.Message}");
            }
        }

        private string CleanText(string text)
        {
            // Remove extra whitespace and special characters
            text = WebUtility.HtmlDecode(text);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"[^\w\s\-\.,:]", " ");

            // Capitalize first letter of sentence
            text = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text.ToLower());

            return text.Trim();
        }

        private string GenerateSummary(List<string> contentPieces)
        {
            // Combine similar sentences and create a coherent summary
            var mainPoints = contentPieces
                .Select(p => CleanText(p))
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct()
                .Take(3)
                .ToList();

            return string.Join("\n\n", mainPoints);
        }

        private string GenerateFormattedOutput(string query, string summary, HashSet<string> keyPoints, HashSet<string> sources)
        {
            var selectedKeyPoints = keyPoints
                .OrderByDescending(p => p.Length)
                .Take(5)
                .ToList();

            return $@"
    <html>
    <head>
        <style>
            body {{ 
                font-family: 'Segoe UI', Arial, sans-serif;
                line-height: 1.6;
                padding: 2em;
                max-width: 800px;
                margin: 0 auto;
                background: #fff;
            }}
            h2 {{ 
                color: #1a73e8;
                font-size: 1.5em;
                margin-bottom: 1em;
                padding-bottom: 0.5em;
                border-bottom: 2px solid #f0f0f0;
            }}
            .summary {{
                background: #f8f9fa;
                padding: 1.5em;
                border-radius: 8px;
                margin-bottom: 1em;
                box-shadow: 0 1px 3px rgba(0,0,0,0.1);
                color: #000;
            }}
            .key-points {{
                margin-top: 1.5em;
                color: #000;
            }}
            .key-points ul {{
                margin: 0;
                padding-left: 1.5em;
            }}
            .key-points li {{
                margin-bottom: 0.5em;
            }}
            .sources {{
                font-size: 0.9em;
                color: #666;
                margin-top: 2em;
                padding-top: 1em;
                border-top: 1px solid #eee;
            }}
        </style>
    </head>
    <body>
        <h2>{WebUtility.HtmlEncode(query)}</h2>
        <div class='summary'>
            {WebUtility.HtmlEncode(summary).Replace("\n\n", "<br><br>")}
        </div>
        {(selectedKeyPoints.Any() ? $@"
        <div class='key-points'>
            <ul>
                {string.Join("\n", selectedKeyPoints.Select(p => $"<li>{WebUtility.HtmlEncode(p)}</li>"))}
            </ul>
        </div>" : "")}
        <div class='sources'>
            Sources: {string.Join(", ", sources)}
        </div>
    </body>
    </html>";
        }
        public BrowserTab(string title = "New Tab")
        {
            Id = Guid.NewGuid().ToString();
            Title = title;

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

            var csnetButton = new Button();
            csnetButton.SetResourceReference(FrameworkElement.StyleProperty, "ModernButton");
            csnetButton.Content = "CsNet Search";
            csnetButton.Width = 160;
            csnetButton.Height = 40;
            csnetButton.FontSize = 14;

            SearchBox.GotFocus += SearchBox_GotFocus;
            SearchBox.LostFocus += SearchBox_LostFocus;
            SearchBox.KeyDown += SearchBox_KeyDown;
            csbeButton.Click += (s, e) => NavigateToUrl("https://www.csbe.ch");
            csnetButton.Click += async (s, e) => await PerformCsNetSearch(SearchBox.Text);

            searchBorder.Child = SearchBox;
            buttonsPanel.Children.Add(csbeButton);
            buttonsPanel.Children.Add(csnetButton);

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