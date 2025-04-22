using System;
using System.Threading.Tasks;
using System.IO;
using LLama;
using LLama.Common;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Windows;

namespace CsBe_Browser_2._0
{
    public class GemmaService
    {
        private static GemmaService _instance;
        private LLamaWeights _model;
        private ModelParams _modelParams;
        private string _modelPath;
        private bool _isInitialized = false;
        private readonly object _lock = new object();

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
            // Model path will be determined at initialization time
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
                    // Get the model path from settings
                    _modelPath = ModelSelector.GetStoredModelPath();

                    // Check if the model file exists
                    if (string.IsNullOrEmpty(_modelPath) || !File.Exists(_modelPath))
                    {
                        throw new FileNotFoundException("Model file not found. Please configure a valid model file.");
                    }

                    // Configure model parameters - use safer defaults to prevent memory issues
                    _modelParams = new ModelParams(_modelPath)
                    {
                        ContextSize = 1024,    // Reduced from 2048 to use less memory
                        GpuLayerCount = 0,     // Use CPU only
                        BatchSize = 512,       // Reduced batch size
                        Seed = 1337            // Fixed seed for reproducibility
                    };

                    // Instead of direct loading, wrap in try-catch with detailed error handling
                    try
                    {
                        // Load the model with increased protection
                        _model = LLamaWeights.LoadFromFile(_modelParams);
                        _isInitialized = true;
                    }
                    catch (AccessViolationException)
                    {
                        // Specific handling for memory issues
                        MessageBox.Show(
                            "Memory access error while loading the model. This could be due to:\n\n" +
                            "1. The model is too large for available memory\n" +
                            "2. The model format is incompatible\n" +
                            "3. You need to run the application as administrator\n\n" +
                            "Try using a smaller model (e.g., 1B parameters or quantized Q4 version).",
                            "Model Loading Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);

                        throw new Exception("The model could not be loaded due to memory access issues.");
                    }
                    catch (Exception ex)
                    {
                        // Log more details and rethrow
                        MessageBox.Show(
                            $"Failed to load model: {ex.Message}\n\n" +
                            "Please try a different model file.",
                            "Model Loading Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        throw new Exception($"Failed to initialize AI model: {ex.Message}", ex);
                    }
                }
                catch (Exception ex)
                {
                    // Don't leave the model in a half-initialized state
                    _isInitialized = false;
                    _model = null;
                    throw new Exception($"Failed to initialize AI model: {ex.Message}", ex);
                }
            }
        }

        public async Task<string> GenerateResponse(string query, List<string> contextTexts = null)
        {
            try
            {
                if (!_isInitialized)
                {
                    await Initialize();
                }

                // If we get here but still not initialized, there was an error during initialization
                if (!_isInitialized || _model == null)
                {
                    return "The AI model could not be initialized. Please check your model file or try a different one.";
                }

                // Create a context for inference
                using var context = _model.CreateContext(_modelParams);
                var executor = new InteractiveExecutor(context);

                // Build the prompt with user query and optional context snippets
                var promptBuilder = new StringBuilder();

                // Add system prompt - try a simpler format that works with most models
                promptBuilder.AppendLine("You are a helpful search assistant. Please summarize information about the following query:");

                promptBuilder.AppendLine("\nQuery: " + query);

                // Add context if available
                if (contextTexts != null && contextTexts.Count > 0)
                {
                    promptBuilder.AppendLine("\nContext information:");
                    foreach (var text in contextTexts)
                    {
                        promptBuilder.AppendLine("- " + text);
                    }
                }

                // Add a clear request for the response
                promptBuilder.AppendLine("\nSummary:");

                var prompt = promptBuilder.ToString();

                // Create inference params with more conservative settings
                var inferenceParams = new InferenceParams
                {
                    MaxTokens = 500,     // Reduced from 800
                    Temperature = 0.7f,  // Add some temperature
                    TopP = 0.9f,         // Add some nucleus sampling
                };

                // Run inference with timeout
                var response = new StringBuilder();
                var inferenceTask = Task.Run(async () =>
                {
                    await foreach (var text in executor.InferAsync(prompt, inferenceParams))
                    {
                        response.Append(text);
                    }
                });

                // Add timeout to prevent infinite loops
                if (await Task.WhenAny(inferenceTask, Task.Delay(30000)) != inferenceTask)
                {
                    return "The AI response took too long to generate. Please try a simpler query.";
                }

                await inferenceTask; // Make sure it's complete

                // Clean up the response - use a simpler approach
                var responseText = response.ToString().Trim();

                // Try to remove the original prompt if present
                if (responseText.StartsWith(prompt))
                {
                    responseText = responseText.Substring(prompt.Length).Trim();
                }
                else if (responseText.Contains("Summary:"))
                {
                    int startIdx = responseText.IndexOf("Summary:") + "Summary:".Length;
                    if (startIdx < responseText.Length)
                    {
                        responseText = responseText.Substring(startIdx).Trim();
                    }
                }

                return responseText;
            }
            catch (Exception ex)
            {
                // Provide more helpful error messaging
                return $"Error generating AI response: {ex.Message}\n\nPlease try again with a simpler query or check your model configuration.";
            }
        }

        public void ExtractKeypoints(string text, out List<string> keypoints, out List<string> keywords)
        {
            // Simple extraction logic - this could be enhanced with more sophisticated NLP
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

                // Extract potential keywords (words that appear frequently)
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
                    keypoints.Add("Information extracted from the search query");
                }

                if (keywords.Count == 0)
                {
                    keywords.Add("search");
                    keywords.Add("results");
                }
            }
        }
    }
}