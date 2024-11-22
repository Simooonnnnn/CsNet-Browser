﻿using Microsoft.Web.WebView2.Wpf;
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
using System.Windows.Shapes; // Add this line at the top with other using statements

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


                var searchUrl = $"https://duckduckgo.com/html/?q={Uri.EscapeDataString(query)}";
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

                        // Remove unwanted nodes
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

                            // Score the paragraphs based on keyword relevance
                            var scoredParagraphs = paragraphs.Select(p => new
                            {
                                Paragraph = p,
                                Score = CalculateRelevanceScore(p, query) // New scoring function
                            })
                            .Where(x => x.Score > 0) // Only keep relevant paragraphs
                            .OrderByDescending(x => x.Score) // Order by relevance
                            .Select(x => x.Paragraph)
                            .ToList();

                            contentPieces.AddRange(scoredParagraphs);
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
        private int CalculateRelevanceScore(string text, string query)
        {
            var queryWords = query.ToLower().Split(' ', ',', '.').Where(w => w.Length > 2).ToList();
            int score = 0;

            foreach (var word in queryWords)
            {
                if (text.ToLower().Contains(word))
                {
                    score += 1; // Increment score for each match
                }
            }

            return score;
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

            var logoViewbox = new Viewbox
            {
                Height = 75,
                Width = 282,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(0, 0, 0, 30)
            };

            var logoPath1 = new Path
            {
                Data = Geometry.Parse("M207 64.2857C207 58.3684 211.797 53.5714 217.714 53.5714H254.619C260.537 53.5714 265.333 58.3684 265.333 64.2857C265.333 70.203 260.537 75 254.619 75H217.714C211.797 75 207 70.203 207 64.2857Z"),
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#180102"))
            };

            var logoPath2 = new Path
            {
                Data = Geometry.Parse("M207 10.7143C207 4.79695 211.797 0 217.714 0H254.619C260.537 0 265.333 4.79695 265.333 10.7143C265.333 16.6316 260.537 21.4286 254.619 21.4286H217.714C211.797 21.4286 207 16.6316 207 10.7143Z"),
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FCD202"))
            };

            var logoPath3 = new Path
            {
                Data = Geometry.Parse("M223.667 36.9048C223.667 30.9874 228.464 26.1905 234.381 26.1905H271.286C277.203 26.1905 282 30.9874 282 36.9048C282 42.8221 277.203 47.619 271.286 47.619H234.381C228.464 47.619 223.667 42.8221 223.667 36.9048Z"),
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E61F11"))
            };

            var logoPath4 = new Path
            {
                Data = Geometry.Parse("M0.368164 36.664C0.368164 32.312 1.37083 28.408 3.37616 24.952C5.42416 21.496 8.17616 18.808 11.6322 16.888C15.1308 14.9253 18.9495 13.944 23.0882 13.944C27.8242 13.944 32.0268 15.1173 35.6962 17.464C39.4082 19.768 42.0962 23.0533 43.7602 27.32H34.9922C33.8402 24.9733 32.2402 23.224 30.1922 22.072C28.1442 20.92 25.7762 20.344 23.0882 20.344C20.1442 20.344 17.5202 21.0053 15.2162 22.328C12.9122 23.6507 11.0988 25.5493 9.77617 28.024C8.49616 30.4987 7.85616 33.3787 7.85616 36.664C7.85616 39.9493 8.49616 42.8293 9.77617 45.304C11.0988 47.7787 12.9122 49.6987 15.2162 51.064C17.5202 52.3867 20.1442 53.048 23.0882 53.048C25.7762 53.048 28.1442 52.472 30.1922 51.32C32.2402 50.168 33.8402 48.4187 34.9922 46.072H43.7602C42.0962 50.3387 39.4082 53.624 35.6962 55.928C32.0268 58.232 27.8242 59.384 23.0882 59.384C18.9068 59.384 15.0882 58.424 11.6322 56.504C8.17616 54.5413 5.42416 51.832 3.37616 48.376C1.37083 44.92 0.368164 41.016 0.368164 36.664Z"),
                Fill = new SolidColorBrush(Colors.Black)
            };

            var logoPath5 = new Path
            {
                Data = Geometry.Parse("M65.1002 59.576C62.3268 59.576 59.8308 59.0853 57.6122 58.104C55.4362 57.08 53.7082 55.7147 52.4282 54.008C51.1482 52.2587 50.4655 50.3173 50.3802 48.184H57.9322C58.0602 49.6773 58.7642 50.936 60.0442 51.96C61.3668 52.9413 63.0095 53.432 64.9722 53.432C67.0202 53.432 68.5988 53.048 69.7082 52.28C70.8602 51.4693 71.4362 50.4453 71.4362 49.208C71.4362 47.8853 70.7962 46.904 69.5162 46.264C68.2788 45.624 66.2948 44.92 63.5642 44.152C60.9188 43.4267 58.7642 42.7227 57.1002 42.04C55.4362 41.3573 53.9855 40.312 52.7482 38.904C51.5535 37.496 50.9562 35.64 50.9562 33.336C50.9562 31.4587 51.5108 29.752 52.6202 28.216C53.7295 26.6373 55.3082 25.4 57.3562 24.504C59.4468 23.608 61.8362 23.16 64.5242 23.16C68.5348 23.16 71.7562 24.184 74.1882 26.232C76.6628 28.2373 77.9855 30.9893 78.1562 34.488H70.8602C70.7322 32.9093 70.0922 31.6507 68.9402 30.712C67.7882 29.7733 66.2308 29.304 64.2682 29.304C62.3482 29.304 60.8762 29.6667 59.8522 30.392C58.8282 31.1173 58.3162 32.0773 58.3162 33.272C58.3162 34.2107 58.6575 35 59.3402 35.64C60.0228 36.28 60.8548 36.792 61.8362 37.176C62.8175 37.5173 64.2682 37.9653 66.1882 38.52C68.7482 39.2027 70.8388 39.9067 72.4602 40.632C74.1242 41.3147 75.5535 42.3387 76.7482 43.704C77.9428 45.0693 78.5615 46.8827 78.6042 49.144C78.6042 51.1493 78.0495 52.9413 76.9402 54.52C75.8308 56.0987 74.2522 57.336 72.2042 58.232C70.1988 59.128 67.8308 59.576 65.1002 59.576Z"),
                Fill = new SolidColorBrush(Colors.Black)
            };

            var logoPath6 = new Path
            {
                Data = Geometry.Parse("M123.032 59H115.736L93.7837 25.784V59H86.4877V14.456H93.7837L115.736 47.608V14.456H123.032V59Z"),
                Fill = new SolidColorBrush(Colors.Black)
            };

            var logoPath7 = new Path
            {
                Data = Geometry.Parse("M164.933 40.504C164.933 41.8267 164.847 43.0213 164.677 44.088H137.733C137.946 46.904 138.991 49.1653 140.869 50.872C142.746 52.5787 145.05 53.432 147.781 53.432C151.706 53.432 154.479 51.7893 156.101 48.504H163.973C162.906 51.7467 160.965 54.4133 158.149 56.504C155.375 58.552 151.919 59.576 147.781 59.576C144.41 59.576 141.381 58.8293 138.693 57.336C136.047 55.8 133.957 53.6667 132.421 50.936C130.927 48.1627 130.181 44.9627 130.181 41.336C130.181 37.7093 130.906 34.5307 132.357 31.8C133.85 29.0267 135.919 26.8933 138.565 25.4C141.253 23.9067 144.325 23.16 147.781 23.16C151.109 23.16 154.074 23.8853 156.677 25.336C159.279 26.7867 161.306 28.8347 162.757 31.48C164.207 34.0827 164.933 37.0907 164.933 40.504ZM157.317 38.2C157.274 35.512 156.314 33.3573 154.437 31.736C152.559 30.1147 150.234 29.304 147.461 29.304C144.943 29.304 142.789 30.1147 140.997 31.736C139.205 33.3147 138.138 35.4693 137.797 38.2H157.317Z"),
                Fill = new SolidColorBrush(Colors.Black)
            };

            var logoPath8 = new Path
            {
                Data = Geometry.Parse("M180.625 29.688V49.208C180.625 50.5307 180.923 51.4907 181.521 52.088C182.161 52.6427 183.227 52.92 184.721 52.92H189.201V59H183.441C180.155 59 177.638 58.232 175.889 56.696C174.139 55.16 173.265 52.664 173.265 49.208V29.688H169.105V23.736H173.265V14.968H180.625V23.736H189.201V29.688H180.625Z"),
                Fill = new SolidColorBrush(Colors.Black)
            };

            var canvas = new Canvas { Width = 282, Height = 75 };
            canvas.Children.Add(logoPath1);
            canvas.Children.Add(logoPath2);
            canvas.Children.Add(logoPath3);
            canvas.Children.Add(logoPath4);
            canvas.Children.Add(logoPath5);
            canvas.Children.Add(logoPath6);
            canvas.Children.Add(logoPath7);
            canvas.Children.Add(logoPath8);

            logoViewbox.Child = canvas;
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

            stackPanel.Children.Add(logoViewbox);
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
