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
using System.Windows.Documents;
using System.DirectoryServices;

namespace CsBe_Browser_2._0
{
    public class BrowserTab : INotifyPropertyChanged

    {
        private readonly HttpClient _httpClient = new HttpClient();
        private bool _isCustomSearch = false;
        private string _customSearchQuery = string.Empty;
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
        private string CleanText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            text = WebUtility.HtmlDecode(text);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"[^\w\s\.,!?:;\-()]", "");
            return text.Trim();
        }
        private async Task PerformCsNetSearch(string query)
        {
            try
            {
                if (WebView.CoreWebView2 == null)
                {
                    await WebView.EnsureCoreWebView2Async();
                }

                if (string.IsNullOrWhiteSpace(query) || query == "Das Web durchsuchen")
                {
                    MessageBox.Show("Please enter a search query.");
                    return;
                }

                // Update the tab title
                var titleBlock = ((DockPanel)TabButton.Content).Children.OfType<TextBlock>().First();
                titleBlock.Text = "CsNet Suche";
                Title = "CsNet Suche";

                // Update the URL bar with the custom format
                var mainWindow = Application.Current.MainWindow as MainWindow;
                if (mainWindow != null)
                {
                    var addressBar = mainWindow.FindName("AddressBar") as TextBox;
                    if (addressBar != null)
                    {
                        addressBar.Text = $"cs-net.search/{Uri.EscapeDataString(query)}";
                        addressBar.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#202124"));
                    }
                }

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");

                var techTerms = new[] { "apple", "iphone", "ipad", "mac", "macbook", "processor", "chip", "specs", "m1", "m2", "m3" };
                bool isTechQuery = techTerms.Any(term => query.ToLower().Contains(term));


                var searchUrl = isTechQuery ?
                    $"https://duckduckgo.com/html/?q={Uri.EscapeDataString(query)}+site:apple.com+OR+site:macrumors.com+OR+site:9to5mac.com+OR+site:theverge.com" :
                    $"https://duckduckgo.com/html/?q={Uri.EscapeDataString(query)}";

                var response = await _httpClient.GetStringAsync(searchUrl);
                var doc = new HtmlDocument();
                doc.LoadHtml(response);

                var links = doc.DocumentNode
                    .SelectNodes("//div[contains(@class, 'result__body')]//a[@class='result__url']")
                    ?.Select(a => "https://" + a.InnerText.Trim())
                    .Where(url => !string.IsNullOrWhiteSpace(url) &&
                                 !ContainsUnwantedDomain(url))
                    .Take(5)
                    .ToList();

                if (links == null || !links.Any())
                {
                    MessageBox.Show("No search results found.");
                    return;
                }

                ShowProgressIndicator(query);

                var contentPieces = new List<string>();
                var sources = new HashSet<string>();

                foreach (var link in links)
                {
                    try
                    {
                        var pageContent = await _httpClient.GetStringAsync(link);
                        var pageDoc = new HtmlDocument();
                        pageDoc.LoadHtml(pageContent);

                        foreach (var node in pageDoc.DocumentNode.SelectNodes("//script|//style|//nav|//header|//footer|//aside|//iframe|//form|//*[contains(@class, 'cookie')]|//*[contains(@class, 'ad')]")?.ToList() ?? new List<HtmlNode>())
                        {
                            node.Remove();
                        }

                        sources.Add(new Uri(link).Host);

                        var contentNodes = pageDoc.DocumentNode.SelectNodes("//article//p|//main//p|//div[contains(@class, 'content')]//p|//div[contains(@class, 'post')]//p");
                        if (contentNodes != null)
                        {
                            var paragraphs = contentNodes
                                .Select(node => CleanText(node.InnerText))
                                .Where(text =>
                                    text.Length > 50 &&
                                    text.Length < 500 &&
                                    !ContainsUnwantedContent(text) &&
                                    IsRelevantToQuery(text, query))
                                .ToList();

                            contentPieces.AddRange(paragraphs);
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }

                if (!contentPieces.Any())
                {
                    MessageBox.Show("Could not extract meaningful information from the sources.");
                    return;
                }

                var results = new SearchResults();
                results.AddContent(string.Join(", ", sources), contentPieces.Take(4).ToList());

                var htmlContent = GenerateFormattedOutput(query, results);
                WebView.NavigateToString(htmlContent);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Search error: {ex.Message}");
            }
        }

        private void ShowProgressIndicator(string query)
        {
            // Update the tab title and URL bar here too for consistency
            var titleBlock = ((DockPanel)TabButton.Content).Children.OfType<TextBlock>().First();
            titleBlock.Text = "CsNet Suche";
            Title = "CsNet Suche";

            var mainWindow = Application.Current.MainWindow as MainWindow;
            if (mainWindow != null)
            {
                var addressBar = mainWindow.FindName("AddressBar") as TextBox;
                if (addressBar != null)
                {
                    addressBar.Text = $"cs-net.search/{Uri.EscapeDataString(query)}";
                    addressBar.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#202124"));
                }
            }

            var progressHtml = $@"
    <html>
    <head>
        <style>
            body {{
                font-family: 'Segoe UI', system-ui, -apple-system, sans-serif;
                display: flex;
                justify-content: center;
                align-items: center;
                height: 100vh;
                margin: 0;
                background: #f8f9fa;
            }}
            .loader {{
                text-align: center;
                padding: 2rem;
                background: white;
                border-radius: 1rem;
                box-shadow: 0 4px 6px -1px rgb(0 0 0 / 0.1);
            }}
            .spinner {{
                width: 40px;
                height: 40px;
                margin: 1rem auto;
                border: 3px solid #f3f3f3;
                border-top: 3px solid #3498db;
                border-radius: 50%;
                animation: spin 1s linear infinite;
            }}
            @keyframes spin {{
                0% {{ transform: rotate(0deg); }}
                100% {{ transform: rotate(360deg); }}
            }}
        </style>
    </head>
    <body>
        <div class='loader'>
            <div class='spinner'></div>
            <p>Analyzing results for:<br><strong>{WebUtility.HtmlEncode(query)}</strong></p>
        </div>
    </body>
    </html>";

            WebView.NavigateToString(progressHtml);
            WebView.Visibility = Visibility.Visible;
            HomePanel.Visibility = Visibility.Collapsed;
        }
        private string GenerateFormattedOutput(string query, SearchResults results)
        {
            var contentHtml = new System.Text.StringBuilder();

            var keywords = ExtractKeywords(string.Join(" ", results.ContentBySources.SelectMany(x => x.Value)));

            foreach (var source in results.ContentBySources)
            {
                contentHtml.Append($@"
            <div class='source-section'>
                <h3>Von {WebUtility.HtmlEncode(source.Key)}</h3>
                <div class='keywords'>
                    <h4>Kernpunkte:</h4>
                    <ul>
                        {string.Join("\n", source.Value
                                                    .SelectMany(text => ExtractBulletPoints(text))
                                                    .Take(5)
                                                    .Select(point => $"<li>{WebUtility.HtmlEncode(point)}</li>"))}
                    </ul>
                </div>
                <div class='keywords'>
                    <h4>Schlüsselwörter:</h4>
                    <div class='keyword-tags'>{string.Join(" ", keywords.Take(8).Select(k => $"<span class='keyword'>{WebUtility.HtmlEncode(k)}</span>"))}</div>
                </div>
                <div class='content'>
                    {string.Join("\n", source.Value.Select(content => $"<p>{WebUtility.HtmlEncode(content)}</p>"))}
                </div>
            </div>
        ");
            }

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
                color: #000000;
            }}
            h2 {{ 
                color: #180102;
                font-size: 1.5em;
                margin-bottom: 1em;
                padding-bottom: 0.5em;
                border-bottom: 2px solid #f0f0f0;
            }}
            h4 {{
                color: #180102;
                font-size: 1.1em;
                margin: 1em 0 0.5em 0;
            }}
            .source-section {{
                background: #f8f9fa;
                padding: 1.5em;
                border-radius: 8px;
                margin-bottom: 1em;
                box-shadow: 0 1px 3px rgba(0,0,0,0.1);
            }}
            h3 {{
                color: #180102;
                font-size: 1.2em;
                margin-bottom: 0.5em;
            }}
            .keywords {{
                margin: 1em 0;
                padding: 1em;
                background: #fff;
                border-radius: 4px;
            }}
            .keyword-tags {{
                display: flex;
                flex-wrap: wrap;
                gap: 0.5em;
            }}
            .keyword {{
                background: #f0f0f0;
                color: #000000;
                padding: 0.3em 0.8em;
                border-radius: 1em;
                font-size: 0.9em;
                border: 1px solid #d0d0d0;
            }}
            ul {{
                margin: 0.5em 0;
                padding-left: 1.5em;
            }}
            li {{
                margin: 0.3em 0;
                color: #000000;
            }}
            p {{
                color: #000000;
                font-size: 1.1em;
                line-height: 1.8;
                margin-bottom: 1em;
            }}
        </style>
    </head>
    <body>
        <h2>Suchergebnisse für: {WebUtility.HtmlEncode(query)}</h2>
        {contentHtml}
    </body>
    </html>";
        }
        private List<string> ExtractKeywords(string text)
        {
            var words = text.ToLower()
                .Split(new[] { ' ', '\n', '\r', '.', ',', ';', ':', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 3)
                .GroupBy(w => w)
                .OrderByDescending(g => g.Count())
                .Where(g => !IsStopWord(g.Key))
                .Select(g => g.Key)
                .ToList();

            return words;
        }

        private List<string> ExtractBulletPoints(string text)
        {
            var sentences = text.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 20 && s.Length < 150)
                .ToList();

            return sentences;
        }

        private bool IsStopWord(string word)
        {
            var stopWords = new HashSet<string>
    {
        "the", "be", "to", "of", "and", "a", "in", "that", "have",
        "i", "it", "for", "not", "on", "with", "he", "as", "you",
        "do", "at", "this", "but", "his", "by", "from", "they",
        "we", "say", "her", "she", "or", "an", "will", "my",
        "one", "all", "would", "there", "their", "what", "about"
    };

            return stopWords.Contains(word);
        }
        private class SearchResults
        {
            public Dictionary<string, List<string>> ContentBySources { get; } = new();

            public void AddContent(string source, List<string> content)
            {
                if (ContentBySources.ContainsKey(source))
                {
                    ContentBySources[source].AddRange(content);
                }
                else
                {
                    ContentBySources[source] = content;
                }
            }
        }

        private bool ContainsUnwantedContent(string text)
        {
            var unwantedPatterns = new[]
            {
            "cookie", "privacy", "copyright", "subscribe", "newsletter",
            "sign up", "log in", "advertisement", "sponsored", "terms of service",
            "click here", "read more", "learn more", "accept all cookies",
            "privacy settings", "cookie settings"
        };

            return text.ToLower().Contains("$") ||
                   text.Contains("@") ||
                   unwantedPatterns.Any(pattern => text.ToLower().Contains(pattern));
        }
        private bool ContainsUnwantedDomain(string url)
        {
            var unwantedDomains = new[]
            {
        "facebook.com", "instagram.com", "pinterest.com",
        "linkedin.com", "youtube.com", "tumblr.com",
        "microsoft.com/en-us/legal", "privacy", "terms", "cookie"
    };

            return unwantedDomains.Any(domain => url.Contains(domain));
        }
        private bool IsRelevantToQuery(string text, string query)
        {
            var queryWords = query.ToLower().Split(' ', ',', '.').Where(w => w.Length > 2);
            var textWords = text.ToLower().Split(' ');

            int matchCount = queryWords.Count(queryWord =>
                textWords.Any(word => word.Contains(queryWord)));

            return matchCount >= 1;
        }
        public BrowserTab(string title = "Neuer Tab")
        {
            Id = Guid.NewGuid().ToString();
            Title = title;

            // Create the close button with fixed positioning
            var closeButton = new Button
            {
                Content = "×",
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                FontSize = 14,
                Width = 16,
                Height = 16,
                Tag = Id,
                Cursor = Cursors.Hand,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5f6368")),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(4, 0, 10, 5)  // Consistent margins
            };

            closeButton.Click += (s, e) =>
            {
                e.Handled = true;
                CloseRequested?.Invoke(this, EventArgs.Empty);
            };

            // Create the title block with max width and ellipsis
            var titleBlock = new TextBlock
            {
                Text = Title,
                Margin = new Thickness(12, 0, 0, 2),
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11,
                FontFamily = new FontFamily("Segoe UI")
            };

            // Create container for title and close button
            var dockPanel = new DockPanel
            {
                LastChildFill = true,  // Changed to true to allow title to fill remaining space
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            // Add close button first, docked to the right
            dockPanel.Children.Add(closeButton);
            DockPanel.SetDock(closeButton, Dock.Right);

            // Add title block second, it will fill the remaining space
            dockPanel.Children.Add(titleBlock);

            // Create the tab button using the XAML style
            TabButton = new Button
            {
                Style = (Style)Application.Current.FindResource("TabButton"),
                Content = dockPanel,
                Tag = Id,
                MinWidth = 130,  // Minimum width to ensure close button visibility
                MaxWidth = 240,  // Maximum width to prevent tabs from getting too large
                HorizontalContentAlignment = HorizontalAlignment.Stretch
            };

            // Mouse enter/leave events
            TabButton.MouseEnter += (s, e) => { if (!IsSelected) TabButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e8eaed")); };
            TabButton.MouseLeave += (s, e) => { if (!IsSelected) TabButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ffffff")); };

            // Property changed event
            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(IsSelected))
                {
                    UpdateTabAppearance();
                }
            };

            // Initialize WebView and HomePanel
            WebView = new WebView2 { Visibility = Visibility.Collapsed };
            HomePanel = CreateHomePanel();
            HomePanel.Visibility = Visibility.Visible;

            // Navigation completed event
            WebView.NavigationCompleted += (s, e) =>
            {
                if (_isCustomSearch)
                {
                    // Keep the custom title and URL for CsNet searches
                    var titleBlock = ((DockPanel)TabButton.Content).Children.OfType<TextBlock>().First();
                    titleBlock.Text = "CsNet Suche";
                    Title = "CsNet Suche";

                    var mainWindow = Application.Current.MainWindow as MainWindow;
                    if (mainWindow != null)
                    {
                        var addressBar = mainWindow.FindName("AddressBar") as TextBox;
                        if (addressBar != null)
                        {
                            addressBar.Text = $"cs-net.search/{Uri.EscapeDataString(_customSearchQuery)}";
                            addressBar.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#202124"));
                        }
                    }
                }
                else
                {
                    // Regular navigation handling
                    Title = WebView.CoreWebView2.DocumentTitle;
                    var titleBlock = ((DockPanel)TabButton.Content).Children.OfType<TextBlock>().First();
                    titleBlock.Text = string.IsNullOrEmpty(Title) ? "Neuer Tab" : Title;

                    if (WebView.Source != null)
                    {
                        var url = WebView.Source.ToString();
                        var mainWindow = Application.Current.MainWindow as MainWindow;
                        if (mainWindow != null)
                        {
                            var addressBar = mainWindow.FindName("AddressBar") as TextBox;
                            if (addressBar != null)
                            {
                                try
                                {
                                    var uri = new Uri(url);
                                    string domain = uri.Host;

                                    if (uri.Scheme == "about")
                                    {
                                        addressBar.Text = uri.AbsoluteUri;
                                        return;
                                    }

                                    if (domain.StartsWith("www."))
                                    {
                                        domain = domain.Substring(4);
                                    }

                                    addressBar.Text = url;

                                    if (!string.IsNullOrEmpty(uri.PathAndQuery) && uri.PathAndQuery != "/")
                                    {
                                        addressBar.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5f6368"));
                                    }
                                    else
                                    {
                                        addressBar.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#202124"));
                                    }
                                }
                                catch
                                {
                                    addressBar.Text = url;
                                    addressBar.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#202124"));
                                }
                            }
                        }
                    }
                }
            };

        }
        private void UpdateTabAppearance()
        {
            if (IsSelected)
            {
                TabButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ffffff"));
                TabButton.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e8eaed"));
                var titleBlock = ((DockPanel)TabButton.Content).Children.OfType<TextBlock>().First();
                titleBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#202124"));
            }
            else
            {
                TabButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ffffff"));
                TabButton.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e8eaed"));
                var titleBlock = ((DockPanel)TabButton.Content).Children.OfType<TextBlock>().First();
                titleBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5f6368"));
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
            SearchBox.BorderThickness = new Thickness(1);
            SearchBox.Text = "Das Web durchsuchen";
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
            if (SearchBox.Text == "Das Web durchsuchen")
            {
                SearchBox.Text = string.Empty;
                SearchBox.Foreground = Brushes.Black;
            }
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                SearchBox.Text = "Das Web durchsuchen";
                SearchBox.Foreground = Brushes.Gray;
            }
        }

        private void SearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string searchText = SearchBox.Text.Trim();
                if (!string.IsNullOrWhiteSpace(searchText) && searchText != "Das Web durchsuchen")
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
