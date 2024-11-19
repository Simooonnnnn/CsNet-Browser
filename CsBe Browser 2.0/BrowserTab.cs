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

                if (string.IsNullOrWhiteSpace(query) || query == "Search the Web")
                {
                    MessageBox.Show("Please enter a search query.");
                    return;
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

        private bool ContainsUnwantedDomain(string url)
        {
            var unwantedDomains = new[]
            {
        "facebook.com", "twitter.com", "instagram.com", "pinterest.com",
        "linkedin.com", "youtube.com", "reddit.com", "tumblr.com",
        "microsoft.com/en-us/legal", "privacy", "terms", "cookie"
    };

            return unwantedDomains.Any(domain => url.Contains(domain));
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

        private bool IsRelevantToQuery(string text, string query)
        {
            var queryWords = query.ToLower().Split(' ', ',', '.').Where(w => w.Length > 2);
            var textWords = text.ToLower().Split(' ');

            int matchCount = queryWords.Count(queryWord =>
                textWords.Any(word => word.Contains(queryWord)));

            return matchCount >= 1;
        }
        private async Task PerformGeneralSearch(string query)
        {
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

            ShowProgressIndicator(query);
            var contentAnalyzer = new ContentAnalyzer();
            var results = await contentAnalyzer.AnalyzeUrls(links, query, _httpClient);
            var htmlContent = GenerateFormattedOutput(query, results);
            WebView.NavigateToString(htmlContent);
        }

        private class ContentAnalyzer
        {
            private string CleanText(string text)
            {
                if (string.IsNullOrWhiteSpace(text)) return string.Empty;

                text = WebUtility.HtmlDecode(text);
                text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
                text = System.Text.RegularExpressions.Regex.Replace(text, @"[^\w\s\.,!?:;\-()]", "");
                text = System.Text.RegularExpressions.Regex.Replace(text, @"\.(?! )", ". ");

                // Don't convert everything to title case
                return text.Trim();
            }

            public async Task<SearchResults> AnalyzeUrls(List<string> urls, string query, HttpClient client)
            {
                var results = new SearchResults();
                var keywordWeights = ExtractKeywords(query);
                var queryTopics = DetermineQueryTopics(query);

                foreach (var url in urls)
                {
                    try
                    {
                        var content = await client.GetStringAsync(url);
                        var doc = new HtmlDocument();
                        doc.LoadHtml(content);

                        // Remove unnecessary elements
                        foreach (var node in doc.DocumentNode.SelectNodes("//script|//style|//nav|//header|//footer|//aside|//iframe|//form|//*[contains(@class, 'cookie')]|//*[contains(@class, 'ad')]|//*[contains(@class, 'banner')]")?.ToList() ?? new List<HtmlNode>())
                        {
                            node.Remove();
                        }

                        // Try to find the most relevant content section
                        var mainContent = doc.DocumentNode.SelectNodes("//article|//main|//div[contains(@class, 'content')]|//div[contains(@class, 'post')]")?.FirstOrDefault()
                            ?? doc.DocumentNode;

                        var paragraphs = mainContent
                            .SelectNodes(".//p|.//h1|.//h2|.//h3|.//li[not(ancestor::nav)]")
                            ?.Select(node => new { Text = CleanText(node.InnerText), Node = node })
                            .Where(p => IsRelevantContent(p.Text, keywordWeights, queryTopics))
                            .ToList();

                        if (paragraphs != null && paragraphs.Any())
                        {
                            var relevantContent = paragraphs
                                .OrderByDescending(p => CalculateRelevanceScore(p.Text, keywordWeights, queryTopics))
                                .Take(3)
                                .Select(p => p.Text)
                                .ToList();

                            results.AddContent(new Uri(url).Host, relevantContent);
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }

                return results;
            }

            private Dictionary<string, double> ExtractKeywords(string query)
            {
                var keywords = new Dictionary<string, double>();
                var words = query.ToLower().Split(new[] { ' ', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var word in words)
                {
                    if (word.Length > 2 && !IsStopWord(word))
                    {
                        keywords[word] = 1.0;

                        // Add common variations
                        if (word.EndsWith("s")) keywords[word.TrimEnd('s')] = 0.9;
                        if (word.EndsWith("es")) keywords[word.Substring(0, word.Length - 2)] = 0.9;
                        if (word.EndsWith("ing")) keywords[word.Substring(0, word.Length - 3)] = 0.9;
                        if (word.EndsWith("ed")) keywords[word.Substring(0, word.Length - 2)] = 0.9;
                    }
                }

                return keywords;
            }

            private Dictionary<string, HashSet<string>> DetermineQueryTopics(string query)
            {
                return new Dictionary<string, HashSet<string>>
                {
                    ["tech"] = new HashSet<string> { "processor", "cpu", "gpu", "ram", "memory", "storage", "battery", "display", "screen", "camera", "chip", "performance", "benchmark" },
                    ["specs"] = new HashSet<string> { "specifications", "features", "dimensions", "weight", "capacity", "size", "price", "cost" },
                    ["release"] = new HashSet<string> { "launch", "release", "announce", "debut", "unveil", "reveal" }
                };
            }

            private bool IsRelevantContent(string text, Dictionary<string, double> keywords, Dictionary<string, HashSet<string>> topics)
            {
                if (string.IsNullOrWhiteSpace(text) || text.Length < 30 || text.Length > 1000)
                    return false;

                var words = text.ToLower().Split(' ');
                var keywordCount = words.Count(w => keywords.Keys.Any(k => w.Contains(k)));
                var topicMatchCount = words.Count(w => topics.Values.Any(topicWords => topicWords.Any(t => w.Contains(t))));

                return (keywordCount >= 1 || topicMatchCount >= 2) && !ContainsUnwantedContent(text);
            }

            private double CalculateRelevanceScore(string text, Dictionary<string, double> keywords, Dictionary<string, HashSet<string>> topics)
            {
                var words = text.ToLower().Split(' ');
                double score = 0;

                // Keyword matching
                foreach (var word in words)
                {
                    var matchingKeyword = keywords.Keys.FirstOrDefault(k => word.Contains(k));
                    if (matchingKeyword != null)
                    {
                        score += keywords[matchingKeyword] * 2;  // Increase keyword weight
                    }
                }

                // Topic matching
                foreach (var topic in topics)
                {
                    foreach (var topicWord in topic.Value)
                    {
                        if (words.Any(w => w.Contains(topicWord)))
                        {
                            score += 0.5;  // Add topic relevance
                        }
                    }
                }

                // Prefer medium-length, focused paragraphs
                var lengthScore = Math.Min(1.0, text.Length / 200.0);
                if (text.Length > 500) lengthScore *= 0.7;  // Penalize very long paragraphs

                return score * lengthScore;
            }

            private bool ContainsUnwantedContent(string text)
            {
                var unwantedPatterns = new[]
                {
            "cookie", "privacy", "copyright", "subscribe", "newsletter",
            "sign up", "log in", "advertisement", "sponsored", "terms of service",
            "click here", "read more", "learn more"
        };

                text = text.ToLower();
                return unwantedPatterns.Any(pattern => text.Contains(pattern)) ||
                       text.Count(c => c == '$') > 2 ||  // Avoid price-heavy content
                       text.Count(c => c == '@') > 0;    // Avoid contact information
            }

            private bool IsStopWord(string word)
            {
                var stopWords = new HashSet<string>
        {
            "the", "be", "to", "of", "and", "a", "in", "that", "have",
            "i", "it", "for", "not", "on", "with", "he", "as", "you",
            "do", "at", "this", "but", "his", "by", "from", "they",
            "we", "say", "her", "she", "or", "an", "will", "my",
            "one", "all", "would", "there", "their", "what"
        };

                return stopWords.Contains(word);
            }
        }

        // Rest of the code remains the same (SearchResults class, GenerateFormattedOutput, and ShowProgressIndicator methods)
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

        private string GenerateFormattedOutput(string query, SearchResults results)
        {
            var contentHtml = new System.Text.StringBuilder();

            // Extract keywords from content
            var keywords = ExtractKeywords(string.Join(" ", results.ContentBySources.SelectMany(x => x.Value)));

            foreach (var source in results.ContentBySources)
            {
                contentHtml.Append($@"
            <div class='source-section'>
                <h3>From {WebUtility.HtmlEncode(source.Key)}</h3>
                <div class='keywords'>
                    <h4>Key Points:</h4>
                    <ul>
                        {string.Join("\n", source.Value
                                    .SelectMany(text => ExtractBulletPoints(text))
                                    .Take(5)
                                    .Select(point => $"<li>{WebUtility.HtmlEncode(point)}</li>"))}
                    </ul>
                </div>
                <div class='keywords'>
                    <h4>Keywords:</h4>
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
            }}
            h2 {{ 
                color: #1a73e8;
                font-size: 1.5em;
                margin-bottom: 1em;
                padding-bottom: 0.5em;
                border-bottom: 2px solid #f0f0f0;
            }}
            h4 {{
                color: #1a73e8;
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
                color: #1a73e8;
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
                background: #e8f0fe;
                color: #1967d2;
                padding: 0.3em 0.8em;
                border-radius: 1em;
                font-size: 0.9em;
            }}
            ul {{
                margin: 0.5em 0;
                padding-left: 1.5em;
            }}
            li {{
                margin: 0.3em 0;
                color: #202124;
            }}
            p {{
                color: #000;
                font-size: 1.1em;
                line-height: 1.8;
                margin-bottom: 1em;
            }}
        </style>
    </head>
    <body>
        <h2>{WebUtility.HtmlEncode(query)}</h2>
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
        private void ShowProgressIndicator(string query)
        {
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