using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using LLama;
using LLama.Common;

namespace CsBe_Browser_2._0
{
    public class GemmaService
    {
        private static GemmaService _instance;
        private string _modelPath;
        private bool _isInitialized = false;
        private readonly object _lock = new object();
        private bool _usingSimulationMode = true;
        private readonly HttpClient _httpClient = new HttpClient();

        // Direct references to loaded model objects
        private LLamaWeights _model;
        private ModelParams _modelParams;

        // Debug/log file
        private string _logFilePath;
        private bool _showedSuccessMessage = false;

        public static GemmaService Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new GemmaService();
                }
                return _instance;
            }
        }

        private GemmaService()
        {
            // Set up HTTP client for fallback mode
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "CsNetBrowser/1.0");
            _httpClient.Timeout = TimeSpan.FromSeconds(10);

            // Set up log file
            _logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ai_debug.log");
            LogMessage("GemmaService instance created");
        }

        // Log helper methods for debugging
        private void LogMessage(string message)
        {
            try
            {
                File.AppendAllText(_logFilePath, $"[{DateTime.Now}] INFO: {message}\n");
            }
            catch { /* Ignore errors writing to log */ }
        }

        private void LogError(string message, Exception ex = null)
        {
            try
            {
                string errorText = $"[{DateTime.Now}] ERROR: {message}";
                if (ex != null)
                {
                    errorText += $"\n  Exception: {ex.GetType().Name}: {ex.Message}";
                    errorText += $"\n  StackTrace: {ex.StackTrace}";
                }
                File.AppendAllText(_logFilePath, errorText + "\n");
            }
            catch { /* Ignore errors writing to log */ }
        }

        public async Task Initialize()
        {
            if (_isInitialized)
                return;

            lock (_lock)
            {
                if (_isInitialized)
                    return;

                try
                {
                    LogMessage("Starting initialization");

                    // Get the model path from settings
                    _modelPath = ModelSelector.GetStoredModelPath();
                    LogMessage($"Model path from settings: {_modelPath}");

                    if (string.IsNullOrEmpty(_modelPath) || !File.Exists(_modelPath))
                    {
                        LogMessage("Model file not found - using simulation mode");
                        _usingSimulationMode = true;
                        _isInitialized = true;
                        return;
                    }

                    // We have a model but will only try to load it when actually generating a response
                    _isInitialized = true;
                    LogMessage("Initialization complete - model will be loaded on first use");
                }
                catch (Exception ex)
                {
                    LogError("Error during initialization", ex);
                    _usingSimulationMode = true;
                    _isInitialized = true;
                }
            }
        }

        public async Task<string> GenerateResponse(string query, List<string> contextTexts = null)
        {
            if (!_isInitialized)
            {
                await Initialize();
            }

            LogMessage($"GenerateResponse called for query: '{query}'");
            LogMessage($"Using simulation mode: {_usingSimulationMode}");

            if (!_usingSimulationMode)
            {
                // Try to use the AI model
                try
                {
                    LogMessage("Attempting to use GGUF model for response");

                    // If we haven't tried to load the model yet, do it now
                    if (_model == null)
                    {
                        LogMessage("Model not loaded yet, loading now");
                        await LoadModelAsync();
                    }

                    // If model loaded successfully, use it
                    if (_model != null)
                    {
                        LogMessage("Model loaded, generating AI response");
                        string aiResponse = await GenerateAIResponse(query, contextTexts);

                        // If we successfully generated a response, show success message once
                        if (!_showedSuccessMessage)
                        {
                            _showedSuccessMessage = true;
                            await Application.Current.Dispatcher.InvokeAsync(() => {
                                MessageBox.Show(
                                    "Successfully loaded and using your GGUF model for search!\n\n" +
                                    "The AI model will now process all your searches.",
                                    "AI Model Working",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                            });
                        }

                        return aiResponse;
                    }
                }
                catch (Exception ex)
                {
                    LogError("Error using AI model", ex);
                    _usingSimulationMode = true;

                    await Application.Current.Dispatcher.InvokeAsync(() => {
                        MessageBox.Show(
                            $"Could not use AI model: {ex.Message}\n\n" +
                            "Falling back to search mode without AI.\n" +
                            "You can try a different model by clicking the model configuration button.",
                            "AI Model Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    });
                }
            }

            // If we get here, we're in simulation mode or AI failed
            LogMessage("Using simulation mode for response");
            return await GenerateSimulatedResponse(query, contextTexts);
        }

        private async Task LoadModelAsync()
        {
            LogMessage($"Loading model from: {_modelPath}");

            try
            {
                // Create model parameters
                _modelParams = new ModelParams(_modelPath)
                {
                    ContextSize = 1024,     // Smaller context size for compatibility
                    GpuLayerCount = 0       // CPU only
                };

                LogMessage("Created ModelParams successfully");

                // Load model - use Task.Run to avoid freezing UI
                await Task.Run(() => {
                    _model = LLamaWeights.LoadFromFile(_modelParams);
                });

                LogMessage("Model loaded successfully");
                _usingSimulationMode = false;
            }
            catch (Exception ex)
            {
                LogError("Failed to load model", ex);
                _model = null;
                _usingSimulationMode = true;
                throw;
            }
        }

        private async Task<string> GenerateAIResponse(string query, List<string> contextTexts)
        {
            LogMessage("Starting AI response generation");

            try
            {
                // Create context and executor
                using var context = _model.CreateContext(_modelParams);
                var executor = new InteractiveExecutor(context);

                LogMessage("Created context and executor");

                // Build prompt
                string prompt = BuildPrompt(query, contextTexts);
                LogMessage($"Built prompt: {prompt.Substring(0, Math.Min(100, prompt.Length))}...");

                // Set up inference parameters
                var inferenceParams = new InferenceParams
                {
                    MaxTokens = 800,
                    Temperature = 0.7f,
                    TopP = 0.9f
                };

                // Run inference
                LogMessage("Starting inference");
                var response = new StringBuilder();

                // Use Task.Run to prevent UI thread blocking
                await Task.Run(async () => {
                    try
                    {
                        await foreach (var text in executor.InferAsync(prompt, inferenceParams))
                        {
                            // Append this chunk of generated text
                            response.Append(text);

                            // For very long responses, stop after reasonable length
                            if (response.Length > 4000) break;
                        }
                        LogMessage("Inference completed successfully");
                    }
                    catch (Exception ex)
                    {
                        LogError("Error during inference", ex);
                        throw;
                    }
                });

                // Process response
                string result = response.ToString().Trim();
                LogMessage($"Raw response length: {result.Length} characters");

                // Try to clean up response if needed
                if (result.Contains("Response:"))
                {
                    int index = result.IndexOf("Response:");
                    result = result.Substring(index + "Response:".Length).Trim();
                    LogMessage("Extracted response part after 'Response:' marker");
                }

                return result;
            }
            catch (Exception ex)
            {
                LogError("Error generating AI response", ex);
                throw;
            }
        }

        private string BuildPrompt(string query, List<string> contextTexts)
        {
            var sb = new StringBuilder();

            // Simple instruction prompt
            sb.AppendLine("You are a helpful search assistant. Provide an informative response to the following search query:");
            sb.AppendLine($"\nQuery: {query}");

            // Add context if available
            if (contextTexts != null && contextTexts.Count > 0)
            {
                sb.AppendLine("\nRelevant information from search results:");
                int count = 1;
                foreach (var text in contextTexts.Take(5))
                {
                    sb.AppendLine($"{count}. {text}");
                    count++;
                }
            }

            // Add response marker
            sb.AppendLine("\nResponse:");

            return sb.ToString();
        }

        // Fallback simulation response
        private async Task<string> GenerateSimulatedResponse(string query, List<string> contextTexts = null)
        {
            try
            {
                StringBuilder response = new StringBuilder();

                // If we have context, use it to build a reasonable response
                if (contextTexts != null && contextTexts.Count > 0)
                {
                    // First, try to find sentences most relevant to the query
                    var relevantSentences = new List<string>();

                    foreach (var text in contextTexts)
                    {
                        var sentences = ExtractRelevantSentences(text, query, 2);
                        relevantSentences.AddRange(sentences);
                    }

                    // Limit to most relevant sentences
                    relevantSentences = relevantSentences.Take(5).ToList();

                    if (relevantSentences.Count > 0)
                    {
                        // Start with an introduction
                        response.AppendLine($"Here's information about {query}:");
                        response.AppendLine();

                        // Add relevant information as bullet points
                        foreach (var sentence in relevantSentences)
                        {
                            response.AppendLine($"• {sentence.Trim()}");
                        }

                        // Add a conclusion
                        response.AppendLine();
                        response.AppendLine($"These details should provide helpful information about {query}. For more specific information, consider refining your search terms.");
                    }
                    else
                    {
                        // Just use the context directly
                        response.AppendLine($"Here's what I found about {query}:");
                        response.AppendLine();

                        foreach (var text in contextTexts.Take(3))
                        {
                            response.AppendLine($"• {text.Trim()}");
                        }
                    }
                }
                else
                {
                    // No context - try to generate generic response or try a web API fallback
                    try
                    {
                        // Try a fallback to DuckDuckGo search API
                        var searchResult = await GetDuckDuckGoResults(query);
                        if (!string.IsNullOrEmpty(searchResult))
                        {
                            response.Append(searchResult);
                        }
                        else
                        {
                            // Generic fallback
                            response.AppendLine($"Information about {query}:");
                            response.AppendLine();
                            response.AppendLine($"• {query} is a search term that might refer to various topics.");
                            response.AppendLine($"• To get more specific results, try adding more details to your search.");
                            response.AppendLine($"• You can also try searching for related terms to find what you're looking for.");
                        }
                    }
                    catch
                    {
                        // If web API fails, fall back to generic response
                        response.AppendLine($"Information about {query}:");
                        response.AppendLine();
                        response.AppendLine($"• {query} is a search term that might refer to various topics.");
                        response.AppendLine($"• To get more specific results, try adding more details to your search.");
                        response.AppendLine($"• You can also try searching for related terms to find what you're looking for.");
                    }
                }

                return response.ToString();
            }
            catch (Exception ex)
            {
                // Even the simulation failed somehow
                return $"Search for: {query}\n\nPlease try a different search term. Error: {ex.Message}";
            }
        }

        // Helper method to get some search results from DuckDuckGo
        private async Task<string> GetDuckDuckGoResults(string query)
        {
            try
            {
                var url = $"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(query)}";
                var response = await _httpClient.GetStringAsync(url);

                // Simple parsing of results
                var resultMatches = Regex.Matches(response, "<a class=\"result__snippet\".*?>(.*?)</a>");

                if (resultMatches.Count > 0)
                {
                    var relevantResults = new List<string>();

                    foreach (Match match in resultMatches)
                    {
                        if (match.Groups.Count > 1)
                        {
                            string text = match.Groups[1].Value;
                            text = Regex.Replace(text, "<.*?>", ""); // Remove HTML tags
                            text = text.Replace("&nbsp;", " ").Replace("&amp;", "&").Replace("&quot;", "\"");

                            if (!string.IsNullOrWhiteSpace(text) && text.Length > 20)
                            {
                                relevantResults.Add(text);
                            }

                            if (relevantResults.Count >= 5)
                                break;
                        }
                    }

                    if (relevantResults.Count > 0)
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine($"Here's information about {query}:");
                        sb.AppendLine();

                        foreach (var result in relevantResults)
                        {
                            sb.AppendLine($"• {result.Trim()}");
                        }

                        return sb.ToString();
                    }
                }

                return string.Empty; // No results
            }
            catch
            {
                return string.Empty; // Error occurred
            }
        }

        // Helper method to extract sentences that are relevant to a query
        private List<string> ExtractRelevantSentences(string text, string query, int maxSentences)
        {
            var queryWords = query.ToLower().Split(' ', ',', '.', '!', '?')
                .Where(w => w.Length > 3)
                .ToList();

            var sentences = Regex.Split(text, @"(?<=[.!?])\s+")
                .Where(s => s.Length > 10 && s.Length < 200)
                .ToList();

            // Score sentences by number of query words they contain
            var scoredSentences = sentences
                .Select(s => new
                {
                    Sentence = s,
                    Score = queryWords.Count(w => s.ToLower().Contains(w))
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Take(maxSentences)
                .Select(x => x.Sentence)
                .ToList();

            return scoredSentences.Count > 0 ? scoredSentences : sentences.Take(maxSentences).ToList();
        }

        public void ExtractKeypoints(string text, out List<string> keypoints, out List<string> keywords)
        {
            keypoints = new List<string>();
            keywords = new List<string>();

            try
            {
                // Extract bullet points as keypoints
                var lines = text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("•") || trimmed.StartsWith("-") || trimmed.StartsWith("*"))
                    {
                        keypoints.Add(trimmed.Substring(1).Trim());
                    }
                }

                // If no bullet points found, try to extract sentences
                if (keypoints.Count == 0)
                {
                    keypoints = text.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .Where(s => s.Length > 15 && s.Length < 150)
                        .Take(5)
                        .ToList();
                }

                // Extract potential keywords
                var words = text.ToLower()
                    .Split(new[] { ' ', '\n', '\r', '.', ',', ';', ':', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(w => w.Length > 4)
                    .GroupBy(w => w)
                    .OrderByDescending(g => g.Count())
                    .Take(10)
                    .Select(g => g.Key)
                    .ToList();

                keywords.AddRange(words);
            }
            catch
            {
                // Fallback if extraction fails
                if (keypoints.Count == 0)
                {
                    keypoints.Add("Information about the search query");
                    keypoints.Add("Check results for more details");
                }

                if (keywords.Count == 0)
                {
                    keywords.AddRange(new[] { "search", "results", "information" });
                }
            }
        }
    }
}