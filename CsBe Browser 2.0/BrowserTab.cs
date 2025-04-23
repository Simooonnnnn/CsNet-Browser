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
        // Replace the existing PerformCsNetSearch method with this updated version
        // Add this method to your BrowserTab.cs class

        // Replace the existing PerformCsNetSearch method with this improved version
        // Add this method to your BrowserTab.cs class - replace the existing PerformCsNetSearch method

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

                // Set custom search state
                _isCustomSearch = true;
                _customSearchQuery = query;

                ShowProgressIndicator(query);

                // Get search context from web
                var searchContext = new List<string>();
                try
                {
                    _httpClient.DefaultRequestHeaders.Clear();
                    _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");

                    var searchUrl = $"https://duckduckgo.com/html/?q={Uri.EscapeDataString(query)}";
                    var response = await _httpClient.GetStringAsync(searchUrl);
                    var doc = new HtmlDocument();
                    doc.LoadHtml(response);

                    var snippets = doc.DocumentNode
                        .SelectNodes("//div[contains(@class, 'result__body')]//a[@class='result__snippet']")
                        ?.Select(a => CleanText(a.InnerText))
                        .Where(text => !string.IsNullOrWhiteSpace(text) && text.Length > 20)
                        .Take(5)
                        .ToList() ?? new List<string>();

                    searchContext.AddRange(snippets);
                }
                catch (Exception ex)
                {
                    // If web search fails, we can still proceed with just the AI
                    Console.WriteLine($"Web search error: {ex.Message}");
                }

                // Variables to store AI results or fallback content
                string aiResponse = "";
                List<string> keypoints = new List<string>();
                List<string> keywords = new List<string>();
                bool aiSucceeded = false;

                try
                {
                    // Try to initialize AI
                    await GemmaService.Instance.Initialize();

                    // Generate AI response
                    aiResponse = await GemmaService.Instance.GenerateResponse(query, searchContext);

                    // Extract keypoints and keywords
                    GemmaService.Instance.ExtractKeypoints(aiResponse, out keypoints, out keywords);
                    aiSucceeded = true;
                }
                catch (Exception ex)
                {
                    // AI failed, fallback to showing web results directly
                    aiResponse = "AI processing unavailable. Here are relevant search results:\n\n";

                    if (searchContext.Count > 0)
                    {
                        foreach (var text in searchContext)
                        {
                            aiResponse += "• " + text + "\n\n";
                        }
                    }
                    else
                    {
                        aiResponse += "No search results found. Try a different query.";
                    }

                    // Create simple keypoints from search context
                    keypoints = searchContext.Take(3).ToList();
                    if (keypoints.Count == 0)
                    {
                        keypoints.Add("No direct results for this query");
                    }

                    // Create keywords from the query itself
                    keywords = query.Split(' ')
                        .Where(w => w.Length > 3)
                        .Take(5)
                        .ToList();

                    if (keywords.Count == 0)
                    {
                        keywords.Add("search");
                    }

                    aiSucceeded = false;
                }

                // Generate HTML output (works whether AI succeeded or not)
                var htmlContent = GenerateAIFormattedOutput(query, aiResponse, keypoints, keywords, aiSucceeded);
                WebView.NavigateToString(htmlContent);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Search error: {ex.Message}", "CsNet Search Error", MessageBoxButton.OK, MessageBoxImage.Error);

                // Show a basic error page
                var errorHtml = $@"
<html>
<head>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; padding: 2em; max-width: 800px; margin: 0 auto; }}
        h2 {{ color: #d32f2f; }}
        p {{ line-height: 1.6; }}
    </style>
</head>
<body>
    <h2>Search Error</h2>
    <p>Sorry, we encountered an error while processing your search:</p>
    <p><strong>{WebUtility.HtmlEncode(ex.Message)}</strong></p>
    <p>Please try again with a different search query or check your network connection.</p>
</body>
</html>";
                WebView.NavigateToString(errorHtml);
            }
        }

        // Add this parameter to your existing GenerateAIFormattedOutput method
        private string GenerateAIFormattedOutput(string query, string aiResponse, List<string> keypoints, List<string> keywords, bool aiSucceeded = true)
        {
            var cleanKeypoints = keypoints.Count > 0 ? keypoints : ExtractBulletPoints(aiResponse);
            var cleanKeywords = keywords.Count > 0 ? keywords : ExtractKeywords(aiResponse).Take(8).ToList();

            var contentHtml = new System.Text.StringBuilder();

            contentHtml.Append($@"
        <div class='source-section'>
            <h3>{(aiSucceeded ? "Gemma AI Analysis" : "Search Results")}</h3>
            <div class='keywords'>
                <h4>Kernpunkte:</h4>
                <ul>
                    {string.Join("\n", cleanKeypoints
                                                            .Take(5)
                                                            .Select(point => $"<li>{WebUtility.HtmlEncode(point)}</li>"))}
                </ul>
            </div>
            <div class='keywords'>
                <h4>Schlüsselwörter:</h4>
                <div class='keyword-tags'>{string.Join(" ", cleanKeywords.Take(8).Select(k => $"<span class='keyword'>{WebUtility.HtmlEncode(k)}</span>"))}</div>
            </div>
            <div class='content'>
                <p>{WebUtility.HtmlEncode(aiResponse).Replace("\n", "<br />")}</p>
            </div>
        </div>
    ");

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
        .ai-badge {{
            display: inline-block;
            background: {(aiSucceeded ? "#6200FF" : "#707070")};
            color: white;
            padding: 0.3em 0.8em;
            border-radius: 1em;
            font-size: 0.9em;
            margin-left: 1em;
            vertical-align: middle;
        }}
    </style>
</head>
<body>
    <h2>Suchergebnisse für: {WebUtility.HtmlEncode(query)} <span class='ai-badge'>{(aiSucceeded ? "Gemma AI" : "Search Results")}</span></h2>
    {contentHtml}
</body>
</html>";
        }        // Add this new method for generating the AI output HTML
        private string GenerateAIFormattedOutput(string query, string aiResponse, List<string> keypoints, List<string> keywords)
        {
            var cleanKeypoints = keypoints.Count > 0 ? keypoints : ExtractBulletPoints(aiResponse);
            var cleanKeywords = keywords.Count > 0 ? keywords : ExtractKeywords(aiResponse).Take(8).ToList();

            var contentHtml = new System.Text.StringBuilder();

            contentHtml.Append($@"
        <div class='source-section'>
            <h3>Gemma AI Analysis</h3>
            <div class='keywords'>
                <h4>Kernpunkte:</h4>
                <ul>
                    {string.Join("\n", cleanKeypoints
                                                    .Take(5)
                                                    .Select(point => $"<li>{WebUtility.HtmlEncode(point)}</li>"))}
                </ul>
            </div>
            <div class='keywords'>
                <h4>Schlüsselwörter:</h4>
                <div class='keyword-tags'>{string.Join(" ", cleanKeywords.Take(8).Select(k => $"<span class='keyword'>{WebUtility.HtmlEncode(k)}</span>"))}</div>
            </div>
            <div class='content'>
                <p>{WebUtility.HtmlEncode(aiResponse).Replace("\n", "<br />")}</p>
            </div>
        </div>
    ");

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
        .ai-badge {{
            display: inline-block;
            background: #6200FF;
            color: white;
            padding: 0.3em 0.8em;
            border-radius: 1em;
            font-size: 0.9em;
            margin-left: 1em;
            vertical-align: middle;
        }}
    </style>
</head>
<body>
    <h2>Suchergebnisse für: {WebUtility.HtmlEncode(query)} <span class='ai-badge'>Gemma AI</span></h2>
    {contentHtml}
</body>
</html>";
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
                TabButton.SetResourceReference(Button.BackgroundProperty, "TabBackgroundColor");
                TabButton.SetResourceReference(Button.BorderBrushProperty, "BorderColor");
                var titleBlock = ((DockPanel)TabButton.Content).Children.OfType<TextBlock>().First();
                titleBlock.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
            }
            else
            {
                TabButton.SetResourceReference(Button.BackgroundProperty, "TabBackgroundColor");
                TabButton.SetResourceReference(Button.BorderBrushProperty, "BorderColor");
                var titleBlock = ((DockPanel)TabButton.Content).Children.OfType<TextBlock>().First();
                titleBlock.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundColor");
            }
        }
        private Grid CreateHomePanel()
        {
            var grid = new Grid { Background = Brushes.Transparent };
            var stackPanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, -100, 0, 0)
            };

            var logoViewbox = new Viewbox
            {
                Height = 96,
                Width = 360,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(0, 0, 0, 30)
            };

            // Create both light and dark mode logos
            var lightModeLogo = CreateLightModeLogo();
            var darkModeLogo = CreateDarkModeLogo();

            // Initially set the correct logo based on theme
            var initialLogo = ThemeManager.CurrentTheme == ThemeManager.Theme.Dark ? darkModeLogo : lightModeLogo;
            logoViewbox.Child = initialLogo;

            // Subscribe to theme changes
            ThemeManager.ThemeChanged += (s, theme) =>
            {
                logoViewbox.Child = theme == ThemeManager.Theme.Dark ? darkModeLogo : lightModeLogo;
            };

            stackPanel.Children.Add(logoViewbox);

            var searchBorder = new Border
            {
                Background = Application.Current.Resources["SearchBarBackgroundColor"] as SolidColorBrush,
                BorderThickness = new Thickness(0), // Changed to 0
                BorderBrush = null,
                CornerRadius = new CornerRadius(24),
                Width = 550,
                Margin = new Thickness(0, 0, 0, 20)
            };

            searchBorder.Background = Application.Current.Resources["TabBackgroundColor"] as SolidColorBrush;
            searchBorder.BorderBrush = Application.Current.Resources["BorderColor"] as SolidColorBrush;

            SearchBox = new TextBox();
            SearchBox.SetResourceReference(FrameworkElement.StyleProperty, "SearchTextBox");
            SearchBox.Height = 48;
            SearchBox.FontSize = 16;
            SearchBox.BorderThickness = new Thickness(1);
            SearchBox.Text = "Das Web durchsuchen";
            SearchBox.Foreground = Brushes.Gray;
            SearchBox.SetResourceReference(TextBox.BackgroundProperty, "SearchBarBackgroundColor");
            SearchBox.SetResourceReference(TextBox.BorderBrushProperty, "BorderColor");

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
            csbeButton.SetResourceReference(Button.BackgroundProperty, "TabBackgroundColor");
            csbeButton.SetResourceReference(Button.ForegroundProperty, "ForegroundColor");

            var csnetButton = new Button();
            csnetButton.SetResourceReference(FrameworkElement.StyleProperty, "ModernButton");
            csnetButton.Content = "CsNet Search";
            csnetButton.Width = 160;
            csnetButton.Height = 40;
            csnetButton.FontSize = 14;
            csnetButton.SetResourceReference(Button.BackgroundProperty, "TabBackgroundColor");
            csnetButton.SetResourceReference(Button.ForegroundProperty, "ForegroundColor");

            SearchBox.GotFocus += SearchBox_GotFocus;
            SearchBox.LostFocus += SearchBox_LostFocus;
            SearchBox.KeyDown += SearchBox_KeyDown;
            csbeButton.Click += (s, e) => NavigateToUrl("https://www.csbe.ch");
            csnetButton.Click += async (s, e) => await PerformCsNetSearch(SearchBox.Text);

            searchBorder.Child = SearchBox;
            buttonsPanel.Children.Add(csbeButton);
            buttonsPanel.Children.Add(csnetButton);

            stackPanel.Children.Add(searchBorder);
            stackPanel.Children.Add(buttonsPanel);

            grid.Children.Add(stackPanel);

            return grid;
        }

        private Canvas CreateLightModeLogo()
        {
            var canvas = new Canvas { Width = 284, Height = 96 };

            // Create the text path for "CsNet"
            var textPath = new Path
            {
                Data = Geometry.Parse("M2.368 47.664C2.368 43.312 3.37067 39.408 5.376 35.952C7.424 32.496 10.176 29.808 13.632 27.888C17.1307 25.9253 20.9493 24.944 25.088 24.944C29.824 24.944 34.0267 26.1173 37.696 28.464C41.408 30.768 44.096 34.0533 45.76 38.32H36.992C35.84 35.9733 34.24 34.224 32.192 33.072C30.144 31.92 27.776 31.344 25.088 31.344C22.144 31.344 19.52 32.0053 17.216 33.328C14.912 34.6507 13.0987 36.5493 11.776 39.024C10.496 41.4987 9.856 44.3787 9.856 47.664C9.856 50.9493 10.496 53.8293 11.776 56.304C13.0987 58.7787 14.912 60.6987 17.216 62.064C19.52 63.3867 22.144 64.048 25.088 64.048C27.776 64.048 30.144 63.472 32.192 62.32C34.24 61.168 35.84 59.4187 36.992 57.072H45.76C44.096 61.3387 41.408 64.624 37.696 66.928C34.0267 69.232 29.824 70.384 25.088 70.384C20.9067 70.384 17.088 69.424 13.632 67.504C10.176 65.5413 7.424 62.832 5.376 59.376C3.37067 55.92 2.368 52.016 2.368 47.664ZM67.1 70.576C64.3267 70.576 61.8307 70.0853 59.612 69.104C57.436 68.08 55.708 66.7147 54.428 65.008C53.148 63.2587 52.4653 61.3173 52.38 59.184H59.932C60.06 60.6773 60.764 61.936 62.044 62.96C63.3667 63.9413 65.0093 64.432 66.972 64.432C69.02 64.432 70.5987 64.048 71.708 63.28C72.86 62.4693 73.436 61.4453 73.436 60.208C73.436 58.8853 72.796 57.904 71.516 57.264C70.2787 56.624 68.2947 55.92 65.564 55.152C62.9187 54.4267 60.764 53.7227 59.1 53.04C57.436 52.3573 55.9853 51.312 54.748 49.904C53.5533 48.496 52.956 46.64 52.956 44.336C52.956 42.4587 53.5107 40.752 54.62 39.216C55.7293 37.6373 57.308 36.4 59.356 35.504C61.4467 34.608 63.836 34.16 66.524 34.16C70.5347 34.16 73.756 35.184 76.188 37.232C78.6627 39.2373 79.9853 41.9893 80.156 45.488H72.86C72.732 43.9093 72.092 42.6507 70.94 41.712C69.788 40.7733 68.2307 40.304 66.268 40.304C64.348 40.304 62.876 40.6667 61.852 41.392C60.828 42.1173 60.316 43.0773 60.316 44.272C60.316 45.2107 60.6573 46 61.34 46.64C62.0227 47.28 62.8547 47.792 63.836 48.176C64.8173 48.5173 66.268 48.9653 68.188 49.52C70.748 50.2027 72.8387 50.9067 74.46 51.632C76.124 52.3147 77.5533 53.3387 78.748 54.704C79.9427 56.0693 80.5613 57.8827 80.604 60.144C80.604 62.1493 80.0493 63.9413 78.94 65.52C77.8307 67.0987 76.252 68.336 74.204 69.232C72.1987 70.128 69.8307 70.576 67.1 70.576ZM125.032 70H117.736L95.7835 36.784V70H88.4875V25.456H95.7835L117.736 58.608V25.456H125.032V70ZM166.933 51.504C166.933 52.8267 166.847 54.0213 166.677 55.088H139.733C139.946 57.904 140.991 60.1653 142.869 61.872C144.746 63.5787 147.05 64.432 149.781 64.432C153.706 64.432 156.479 62.7893 158.101 59.504H165.972C164.906 62.7467 162.965 65.4133 160.149 67.504C157.375 69.552 153.919 70.576 149.781 70.576C146.41 70.576 143.381 69.8293 140.693 68.336C138.047 66.8 135.957 64.6667 134.421 61.936C132.927 59.1627 132.181 55.9627 132.181 52.336C132.181 48.7093 132.906 45.5307 134.357 42.8C135.85 40.0267 137.919 37.8933 140.565 36.4C143.253 34.9067 146.325 34.16 149.781 34.16C153.109 34.16 156.074 34.8853 158.677 36.336C161.279 37.7867 163.306 39.8347 164.757 42.48C166.207 45.0827 166.933 48.0907 166.933 51.504ZM159.317 49.2C159.274 46.512 158.314 44.3573 156.437 42.736C154.559 41.1147 152.234 40.304 149.461 40.304C146.943 40.304 144.789 41.1147 142.997 42.736C141.205 44.3147 140.138 46.4693 139.797 49.2H159.317ZM182.625 40.688V60.208C182.625 61.5307 182.923 62.4907 183.521 63.088C184.161 63.6427 185.227 63.92 186.721 63.92H191.201V70H185.441C182.155 70 179.638 69.232 177.889 67.696C176.139 66.16 175.265 63.664 175.265 60.208V40.688H171.105V34.736H175.265V25.968H182.625V34.736H191.201V40.688H182.625Z"),
                Fill = new SolidColorBrush(Colors.Black)
            };

            // Yellow rectangle
            var rect1 = new Rectangle
            {
                Width = 59,
                Height = 22,
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FCD202")),
                RadiusX = 11,
                RadiusY = 11
            };
            Canvas.SetLeft(rect1, 209);
            Canvas.SetTop(rect1, 10);

            // Red rectangle
            var rect2 = new Rectangle
            {
                Width = 59,
                Height = 22,
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E61F11")),
                RadiusX = 11,
                RadiusY = 11
            };
            Canvas.SetLeft(rect2, 225);
            Canvas.SetTop(rect2, 37);

            // Black rectangle
            var rect3 = new Rectangle
            {
                Width = 59,
                Height = 22,
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#180102")),
                RadiusX = 11,
                RadiusY = 11
            };
            Canvas.SetLeft(rect3, 209);
            Canvas.SetTop(rect3, 64);

            canvas.Children.Add(textPath);
            canvas.Children.Add(rect1);
            canvas.Children.Add(rect2);
            canvas.Children.Add(rect3);

            return canvas;
        }

        private Canvas CreateDarkModeLogo()
        {
            var canvas = new Canvas { Width = 284, Height = 96 };

            // Create the complete text path for "CsNet"
            var textPath = new Path
            {
                Data = Geometry.Parse("M2.368 47.664C2.368 43.312 3.37067 39.408 5.376 35.952C7.424 32.496 10.176 29.808 13.632 27.888C17.1307 25.9253 20.9493 24.944 25.088 24.944C29.824 24.944 34.0267 26.1173 37.696 28.464C41.408 30.768 44.096 34.0533 45.76 38.32H36.992C35.84 35.9733 34.24 34.224 32.192 33.072C30.144 31.92 27.776 31.344 25.088 31.344C22.144 31.344 19.52 32.0053 17.216 33.328C14.912 34.6507 13.0987 36.5493 11.776 39.024C10.496 41.4987 9.856 44.3787 9.856 47.664C9.856 50.9493 10.496 53.8293 11.776 56.304C13.0987 58.7787 14.912 60.6987 17.216 62.064C19.52 63.3867 22.144 64.048 25.088 64.048C27.776 64.048 30.144 63.472 32.192 62.32C34.24 61.168 35.84 59.4187 36.992 57.072H45.76C44.096 61.3387 41.408 64.624 37.696 66.928C34.0267 69.232 29.824 70.384 25.088 70.384C20.9067 70.384 17.088 69.424 13.632 67.504C10.176 65.5413 7.424 62.832 5.376 59.376C3.37067 55.92 2.368 52.016 2.368 47.664ZM67.1 70.576C64.3267 70.576 61.8307 70.0853 59.612 69.104C57.436 68.08 55.708 66.7147 54.428 65.008C53.148 63.2587 52.4653 61.3173 52.38 59.184H59.932C60.06 60.6773 60.764 61.936 62.044 62.96C63.3667 63.9413 65.0093 64.432 66.972 64.432C69.02 64.432 70.5987 64.048 71.708 63.28C72.86 62.4693 73.436 61.4453 73.436 60.208C73.436 58.8853 72.796 57.904 71.516 57.264C70.2787 56.624 68.2947 55.92 65.564 55.152C62.9187 54.4267 60.764 53.7227 59.1 53.04C57.436 52.3573 55.9853 51.312 54.748 49.904C53.5533 48.496 52.956 46.64 52.956 44.336C52.956 42.4587 53.5107 40.752 54.62 39.216C55.7293 37.6373 57.308 36.4 59.356 35.504C61.4467 34.608 63.836 34.16 66.524 34.16C70.5347 34.16 73.756 35.184 76.188 37.232C78.6627 39.2373 79.9853 41.9893 80.156 45.488H72.86C72.732 43.9093 72.092 42.6507 70.94 41.712C69.788 40.7733 68.2307 40.304 66.268 40.304C64.348 40.304 62.876 40.6667 61.852 41.392C60.828 42.1173 60.316 43.0773 60.316 44.272C60.316 45.2107 60.6573 46 61.34 46.64C62.0227 47.28 62.8547 47.792 63.836 48.176C64.8173 48.5173 66.268 48.9653 68.188 49.52C70.748 50.2027 72.8387 50.9067 74.46 51.632C76.124 52.3147 77.5533 53.3387 78.748 54.704C79.9427 56.0693 80.5613 57.8827 80.604 60.144C80.604 62.1493 80.0493 63.9413 78.94 65.52C77.8307 67.0987 76.252 68.336 74.204 69.232C72.1987 70.128 69.8307 70.576 67.1 70.576ZM125.032 70H117.736L95.7835 36.784V70H88.4875V25.456H95.7835L117.736 58.608V25.456H125.032V70ZM166.933 51.504C166.933 52.8267 166.847 54.0213 166.677 55.088H139.733C139.946 57.904 140.991 60.1653 142.869 61.872C144.746 63.5787 147.05 64.432 149.781 64.432C153.706 64.432 156.479 62.7893 158.101 59.504H165.972C164.906 62.7467 162.965 65.4133 160.149 67.504C157.375 69.552 153.919 70.576 149.781 70.576C146.41 70.576 143.381 69.8293 140.693 68.336C138.047 66.8 135.957 64.6667 134.421 61.936C132.927 59.1627 132.181 55.9627 132.181 52.336C132.181 48.7093 132.906 45.5307 134.357 42.8C135.85 40.0267 137.919 37.8933 140.565 36.4C143.253 34.9067 146.325 34.16 149.781 34.16C153.109 34.16 156.074 34.8853 158.677 36.336C161.279 37.7867 163.306 39.8347 164.757 42.48C166.207 45.0827 166.933 48.0907 166.933 51.504ZM159.317 49.2C159.274 46.512 158.314 44.3573 156.437 42.736C154.559 41.1147 152.234 40.304 149.461 40.304C146.943 40.304 144.789 41.1147 142.997 42.736C141.205 44.3147 140.138 46.4693 139.797 49.2H159.317ZM182.625 40.688V60.208C182.625 61.5307 182.923 62.4907 183.521 63.088C184.161 63.6427 185.227 63.92 186.721 63.92H191.201V70H185.441C182.155 70 179.638 69.232 177.889 67.696C176.139 66.16 175.265 63.664 175.265 60.208V40.688H171.105V34.736H175.265V25.968H182.625V34.736H191.201V40.688H182.625Z"),
                Fill = new SolidColorBrush(Colors.White)
            };

            // Light purple rectangle
            var rect1 = new Rectangle
            {
                Width = 59,
                Height = 22,
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BCB8FF")),
                RadiusX = 11,
                RadiusY = 11
            };
            Canvas.SetLeft(rect1, 209);
            Canvas.SetTop(rect1, 10);

            // Medium purple rectangle
            var rect2 = new Rectangle
            {
                Width = 59,
                Height = 22,
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8563FF")),
                RadiusX = 11,
                RadiusY = 11
            };
            Canvas.SetLeft(rect2, 225);
            Canvas.SetTop(rect2, 37);

            // Dark purple rectangle
            var rect3 = new Rectangle
            {
                Width = 59,
                Height = 22,
                Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6200FF")),
                RadiusX = 11,
                RadiusY = 11
            };
            Canvas.SetLeft(rect3, 209);
            Canvas.SetTop(rect3, 64);

            canvas.Children.Add(textPath);
            canvas.Children.Add(rect1);
            canvas.Children.Add(rect2);
            canvas.Children.Add(rect3);

            return canvas;
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (SearchBox.Text == "Das Web durchsuchen")
            {
                SearchBox.Text = string.Empty;
                SearchBox.Foreground = Application.Current.Resources["ForegroundColor"] as SolidColorBrush;
            }
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                SearchBox.Text = "Das Web durchsuchen";
                SearchBox.Foreground = new SolidColorBrush(Colors.Gray);
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
