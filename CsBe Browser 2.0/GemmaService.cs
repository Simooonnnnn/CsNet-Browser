using System;
using System.Threading.Tasks;
using System.IO;
using LLama;
using LLama.Common;
using System.Collections.Generic;
using System.Text;
using System.Linq;

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
            // Set default model path relative to the executable
            _modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models", "gemma-3-1b-it-Q5_K_M.gguf");
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
                    // Check if the model file exists
                    if (!File.Exists(_modelPath))
                    {
                        throw new FileNotFoundException($"Model file not found at: {_modelPath}");
                    }

                    // Configure model parameters
                    _modelParams = new ModelParams(_modelPath)
                    {
                        ContextSize = 2048,
                        // Removed Seed property as it doesn't exist
                        GpuLayerCount = 0 // Use CPU only
                    };

                    // Load the model
                    _model = LLamaWeights.LoadFromFile(_modelParams);
                    _isInitialized = true;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to initialize Gemma model: {ex.Message}", ex);
                }
            }
        }

        public async Task<string> GenerateResponse(string query, List<string> contextTexts = null)
        {
            if (!_isInitialized)
            {
                await Initialize();
            }

            try
            {
                // Create a context for inference
                using var context = _model.CreateContext(_modelParams);
                var executor = new InteractiveExecutor(context);

                // Build the prompt with user query and optional context snippets
                var promptBuilder = new StringBuilder();

                // Add system prompt
                promptBuilder.AppendLine("<start_of_turn>system\nYou are a helpful search assistant. When given a query and optional context, summarize the most relevant information in a clear, concise manner. Focus on providing accurate facts and organize your response with bullet points for key information.<end_of_turn>");

                // Add context if available
                if (contextTexts != null && contextTexts.Count > 0)
                {
                    promptBuilder.AppendLine("<start_of_turn>user\nQuery: " + query);
                    promptBuilder.AppendLine("\nContext information:");
                    foreach (var text in contextTexts)
                    {
                        promptBuilder.AppendLine("- " + text);
                    }
                    promptBuilder.AppendLine("<end_of_turn>");
                }
                else
                {
                    promptBuilder.AppendLine("<start_of_turn>user\nQuery: " + query + "<end_of_turn>");
                }

                // Add model turn marker
                promptBuilder.AppendLine("<start_of_turn>model");

                var prompt = promptBuilder.ToString();

                // Create inference params - updated to match current API
                var inferenceParams = new InferenceParams
                {
                    MaxTokens = 800,
                    // Removed Temperature, TopP and AntiPrompt properties as they don't exist in this version
                };

                // Set stop sequences - this is the alternative to AntiPrompt
                var stopStrings = new List<string> { "<end_of_turn>", "<start_of_turn>" };

                // Run inference
                var response = new StringBuilder();
                await foreach (var text in executor.InferAsync(prompt, inferenceParams))
                {
                    response.Append(text);

                    // Check if we've hit any stop strings
                    foreach (var stop in stopStrings)
                    {
                        if (response.ToString().EndsWith(stop))
                        {
                            // Remove the stop string
                            response.Length -= stop.Length;
                            break;
                        }
                    }
                }

                // Clean up the response
                var cleanResponse = response.ToString().Trim();

                return cleanResponse;
            }
            catch (Exception ex)
            {
                return $"Error generating AI response: {ex.Message}";
            }
        }

        public void ExtractKeypoints(string text, out List<string> keypoints, out List<string> keywords)
        {
            // Simple extraction logic - this could be enhanced with more sophisticated NLP
            keypoints = new List<string>();
            keywords = new List<string>();

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
    }
}