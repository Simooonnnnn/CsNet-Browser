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
        public string Id { get; }
        public string Title { get; set; }
        public Button TabButton { get; private set; }  // Change these to private set
        public WebView2 WebView { get; private set; }
        public Grid HomePanel { get; private set; }
        private bool _isCustomSearch = false;
        private string _customSearchQuery = string.Empty;

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

        // Add the constructor here
        public BrowserTab()
        {
            Id = Guid.NewGuid().ToString();
            Title = "Neuer Tab";

            // Initialize WebView2
            WebView = new WebView2();
            WebView.Visibility = Visibility.Collapsed;

            // Initialize HomePanel
            HomePanel = CreateHomePanel();
            HomePanel.Visibility = Visibility.Visible;

            // Create the close button
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
                Margin = new Thickness(4, 0, 10, 5)
            };

            closeButton.Click += (s, e) =>
            {
                e.Handled = true;
                CloseRequested?.Invoke(this, EventArgs.Empty);
            };

            // Create title block
            var titleBlock = new TextBlock
            {
                Text = Title,
                Margin = new Thickness(12, 0, 0, 2),
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11,
                FontFamily = new FontFamily("Segoe UI")
            };

            // Create DockPanel for tab content
            var dockPanel = new DockPanel
            {
                LastChildFill = true,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            dockPanel.Children.Add(closeButton);
            DockPanel.SetDock(closeButton, Dock.Right);
            dockPanel.Children.Add(titleBlock);

            // Initialize TabButton
            TabButton = new Button
            {
                Style = (Style)Application.Current.FindResource("TabButton"),
                Content = dockPanel,
                Tag = Id,
                MinWidth = 130,
                MaxWidth = 240,
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

            // Navigation completed event
            WebView.NavigationCompleted += (s, e) =>
            {
                if (_isCustomSearch)
                {
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
                                addressBar.Text = url;
                                addressBar.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#202124"));
                            }
                        }
                    }
                }
            };
        }
        private List<string> ExtractRelevantLinks(HtmlDocument doc, string query)
        {
            try
            {
                var linkNodes = doc.DocumentNode.SelectNodes("//a[contains(@class, 'result__a')]");
                if (linkNodes == null || !linkNodes.Any())
                {
                    MessageBox.Show("No links found. The HTML structure may have changed.");
                    return new List<string>();
                }

                return linkNodes
                    .Select(a => a.GetAttributeValue("href", ""))
                    .Where(url => Uri.IsWellFormedUriString(url, UriKind.Absolute))
                    .Distinct()
                    .ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error extracting links: {ex.Message}");
                return new List<string>();
            }
        }

        private string CleanText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            text = WebUtility.HtmlDecode(text);
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
            text = System.Text.RegularExpressions.Regex.Replace(text, @"[^\w\s\.,!?:;\-()]", "");
            return text.Trim();
        }

        private readonly Dictionary<string, double> DomainTrustScores = new()
        {
            // Reference & Education (Highest Trust)
            {"wikipedia.org", 5.0},  // Top priority
            {"britannica.com", 4.8},
            {"stanford.edu", 4.7},
            {"mit.edu", 4.7},
            {"harvard.edu", 4.7},
            
            // Science & Research
            {"nature.com", 4.6},
            {"science.org", 4.6},
            {"scientificamerican.com", 4.5},
            {"newscientist.com", 4.4},
            
            // Health & Medicine
            {"nih.gov", 4.6},
            {"who.int", 4.6},
            {"mayoclinic.org", 4.5},
            {"webmd.com", 4.0},
            {"healthline.com", 3.8},
            
            // Technology
            {"techcrunch.com", 4.2},
            {"theverge.com", 4.1},
            {"arstechnica.com", 4.2},
            {"wired.com", 4.1},
            {"cnet.com", 4.0},
            
            // News & Current Events
            {"reuters.com", 4.5},
            {"apnews.com", 4.5},
            {"bbc.com", 4.4},
            {"npr.org", 4.3},
            {"economist.com", 4.4},
            
            // Business & Finance
            {"bloomberg.com", 4.3},
            {"ft.com", 4.3},
            {"forbes.com", 4.0},
            {"wsj.com", 4.2},
            
            // General Knowledge
            {"nationalgeographic.com", 4.4},
            {"smithsonianmag.com", 4.3},
            {"scholarpedia.org", 4.2},
            
            // Education Platforms
            {"coursera.org", 4.0},
            {"edx.org", 4.0},
            {"khanacademy.org", 4.0}
        };

        private class ContentPiece
        {
            public string Text { get; set; }
            public string Source { get; set; }
            public double RelevanceScore { get; set; }
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
        private async Task PerformCsNetSearch(string query)
        {
            try
            {
                _isCustomSearch = true;
                _customSearchQuery = query;

                if (WebView.CoreWebView2 == null)
                {
                    await WebView.EnsureCoreWebView2Async();
                }

                if (string.IsNullOrWhiteSpace(query) || query == "Das Web durchsuchen")
                {
                    MessageBox.Show("Please enter a search query.");
                    return;
                }

                UpdateUIForSearch(query);
                ShowProgressIndicator(query);

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");

                var searchUrl = BuildSearchUrl(query);
                var response = await _httpClient.GetStringAsync(searchUrl);
                var doc = new HtmlDocument();
                doc.LoadHtml(response);

                var links = ExtractRelevantLinks(doc, query);

                if (!links.Any())
                {
                    MessageBox.Show("No search results found. Try a different query.");
                    return;
                }

                // Navigate to the first link as a test
                if (WebView.CoreWebView2 != null)
                {
                    WebView.CoreWebView2.Navigate(links.First());
                }
                else
                {
                    MessageBox.Show("WebView2 is not initialized. Try again later.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Search error: {ex.Message}");
            }
        }
        private string BuildSearchUrl(string query)
        {
            var queryTerms = query.ToLower().Split(' ');
            var preferredDomains = new List<string>
    {
        "wikipedia.org", "britannica.com", "stanford.edu", "mit.edu", "harvard.edu", // General trusted sources
        "spiegel.de", "faz.net", "zeit.de", "nzz.ch", "srf.ch",                    // German trusted sources
        "nature.com", "science.org", "who.int", "mayoclinic.org", "healthline.com" // Science & health
    };

            var domainQuery = string.Join("+OR+", preferredDomains.Select(domain => $"site:{domain}"));
            return $"https://duckduckgo.com/html/?q={Uri.EscapeDataString(query)}+({domainQuery})";
        }

        private double GetDomainTrustScore(string url)
        {
            var domain = new Uri(url).Host.ToLower();
            if (domain.StartsWith("www."))
            {
                domain = domain.Substring(4);
            }

            if (DomainTrustScores.TryGetValue(domain, out double score))
            {
                return score;
            }

            foreach (var trustedDomain in DomainTrustScores.Keys)
            {
                if (domain.EndsWith(trustedDomain))
                {
                    return DomainTrustScores[trustedDomain];
                }
            }

            return 1.0;
        }

        private async Task<List<ContentPiece>> ExtractAndAnalyzeContent(List<string> links, string query)
        {
            var contentPieces = new List<ContentPiece>();
            var queryTokens = new HashSet<string>(
                query.ToLower().Split(' ')
                    .Where(t => t.Length > 2)
                    .Select(NormalizeToken));

            foreach (var link in links)
            {
                try
                {
                    var pageContent = await _httpClient.GetStringAsync(link);
                    var pageDoc = new HtmlDocument();
                    pageDoc.LoadHtml(pageContent);

                    CleanupDocument(pageDoc);

                    var contentNodes = ExtractContentNodes(pageDoc);
                    foreach (var node in contentNodes)
                    {
                        var text = CleanText(node.InnerText);
                        if (IsQualityContent(text))
                        {
                            var relevanceScore = CalculateContentRelevance(text, queryTokens);
                            if (relevanceScore > 0.3)
                            {
                                var domainTrustScore = GetDomainTrustScore(link);
                                var finalScore = relevanceScore * domainTrustScore;

                                contentPieces.Add(new ContentPiece
                                {
                                    Text = text,
                                    Source = new Uri(link).Host,
                                    RelevanceScore = finalScore
                                });
                            }
                        }
                    }
                }
                catch
                {
                    continue;
                }
            }

            return contentPieces
                .OrderByDescending(p => p.RelevanceScore)
                .Take(8)
                .ToList();
        }

        private void CleanupDocument(HtmlDocument doc)
        {
            var nodesToRemove = doc.DocumentNode.SelectNodes(
                "//script|//style|//nav|//header|//footer|//aside|//iframe|" +
                "//form|//*[contains(@class, 'cookie')]|//*[contains(@class, 'ad')]|" +
                "//*[contains(@class, 'comment')]|//*[contains(@class, 'sidebar')]|" +
                "//*[contains(@class, 'related')]|//*[contains(@class, 'share')]|" +
                "//*[contains(@class, 'popup')]|//*[contains(@class, 'newsletter')]");

            if (nodesToRemove != null)
            {
                foreach (var node in nodesToRemove)
                {
                    node.Remove();
                }
            }
        }

        private IEnumerable<HtmlNode> ExtractContentNodes(HtmlDocument doc)
        {
            var contentSelectors = new[]
            {
                "//article//p",
                "//main//p",
                "//div[contains(@class, 'content')]//p",
                "//div[contains(@class, 'post')]//p",
                "//div[contains(@class, 'article')]//p",
                "//div[contains(@class, 'text')]//p",
                "//div[contains(@class, 'role', 'main')]//p"
            };

            foreach (var selector in contentSelectors)
            {
                var nodes = doc.DocumentNode.SelectNodes(selector);
                if (nodes != null && nodes.Any())
                {
                    return nodes;
                }
            }

            return Enumerable.Empty<HtmlNode>();
        }

        private double CalculateContentRelevance(string text, HashSet<string> queryTokens)
        {
            var textTokens = text.ToLower()
                .Split(new[] { ' ', '.', ',', ';', ':', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(NormalizeToken)
                .ToList();

            if (textTokens.Count < 10) return 0;

            var matchingTokens = textTokens.Count(t => queryTokens.Contains(t));
            var densityScore = (double)matchingTokens / textTokens.Count;
            var lengthScore = Math.Min(1.0, textTokens.Count / 100.0);

            var informationDensityScore = CalculateInformationDensity(text);

            return (densityScore * 0.5 + lengthScore * 0.2 + informationDensityScore * 0.3);
        }

        private double CalculateInformationDensity(string text)
        {
            var numberCount = System.Text.RegularExpressions.Regex.Matches(text, @"\d+").Count;
            var factIndicators = new[] { "because", "therefore", "however", "according to", "research", "study", "found", "shows" };
            var factIndicatorCount = factIndicators.Sum(indicator =>
                text.ToLower().Split(new[] { ' ', '.', ',' }).Count(word => word == indicator));

            var normalizedNumbers = Math.Min(1.0, numberCount / 5.0);
            var normalizedFacts = Math.Min(1.0, factIndicatorCount / 3.0);

            return (normalizedNumbers + normalizedFacts) / 2.0;
        }
        private string NormalizeToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return token;

            if (token.EndsWith("ing")) token = token.Substring(0, token.Length - 3);
            else if (token.EndsWith("ed")) token = token.Substring(0, token.Length - 2);
            else if (token.EndsWith("s")) token = token.Substring(0, token.Length - 1);
            else if (token.EndsWith("ly")) token = token.Substring(0, token.Length - 2);

            return token;
        }

        private bool IsQualityContent(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            if (text.Length < 50 || text.Length > 1000) return false;
            if (ContainsUnwantedContent(text)) return false;

            var sentences = text.Split('.', '!', '?').Where(s => s.Length > 0);
            var avgWordsPerSentence = sentences.Average(s => s.Split(' ').Length);
            if (avgWordsPerSentence < 5 || avgWordsPerSentence > 40) return false;

            var punctuationDensity = text.Count(c => ".,!?;:".Contains(c)) / (double)text.Length;
            if (punctuationDensity > 0.15) return false;

            var words = text.Split(' ');
            var avgWordLength = words.Average(w => w.Length);
            if (avgWordLength < 3 || avgWordLength > 12) return false;

            var capsRatio = text.Count(char.IsUpper) / (double)text.Length;
            if (capsRatio > 0.3) return false;

            if (ContainsRepetitivePatterns(text)) return false;

            return true;
        }

        private bool ContainsRepetitivePatterns(string text)
        {
            var words = text.ToLower().Split(' ');
            var wordCounts = words.GroupBy(w => w)
                                .ToDictionary(g => g.Key, g => g.Count());

            var totalWords = words.Length;
            foreach (var count in wordCounts.Values)
            {
                if (count > totalWords * 0.2)
                {
                    return true;
                }
            }

            return false;
        }

        private Dictionary<string, List<string>> OrganizeContent(List<ContentPiece> contentPieces, string query)
        {
            var groupedContent = contentPieces
                .GroupBy(p => p.Source)
                .Select(g => new
                {
                    Source = g.Key,
                    Pieces = g.OrderByDescending(p => p.RelevanceScore).ToList(),
                    AverageScore = g.Average(p => p.RelevanceScore),
                    TrustScore = GetDomainTrustScore(g.Key)
                })
                .OrderByDescending(g => g.TrustScore * g.AverageScore)
                .ToDictionary(
                    g => g.Source,
                    g => g.Pieces
                        .Select(p => p.Text)
                        .Take(3)
                        .ToList()
                );

            return groupedContent
                .Take(4)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
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
                "one", "all", "would", "there", "their", "what", "about",
                "der", "die", "das", "und", "in", "zu", "den", "für",
                "ist", "mit", "dem", "nicht", "von", "sie", "ist", "auch",
                "auf", "sich", "als", "ein", "eine", "aber", "aus", "durch"
            };

            return stopWords.Contains(word);
        }

        private bool ContainsUnwantedContent(string text)
        {
            var unwantedPatterns = new[]
            {
                "cookie", "privacy", "copyright", "subscribe", "newsletter",
                "sign up", "log in", "advertisement", "sponsored", "terms of service",
                "click here", "read more", "learn more", "accept all cookies",
                "privacy settings", "cookie settings", "advertisement", "sponsored content",
                "subscription required", "premium content", "members only", "register now",
                "sign in to continue", "create account", "download now", "limited time offer"
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
                "twitter.com", "tiktok.com", "reddit.com",
                "microsoft.com/en-us/legal", "privacy", "terms", "cookie",
                "login", "signin", "account", "advertising", "ads"
            };

            return unwantedDomains.Any(domain => url.ToLower().Contains(domain));
        }
        private void ShowProgressIndicator(string query)
        {
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
            </div>");
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
        private void UpdateUIForSearch(string query)
        {
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

            WebView.Source = new Uri($"about:blank");
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