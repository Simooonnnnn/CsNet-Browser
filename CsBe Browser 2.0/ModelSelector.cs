using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using System.Configuration;
using System.Diagnostics;

namespace CsBe_Browser_2._0
{
    public class ModelSelector
    {
        // Add this new method to allow changing the model after initial setup
        public static async Task ChangeModelFile()
        {
            bool modelConfigured = false;
            while (!modelConfigured)
            {
                string selectedFilePath = null;
                bool fileSelected = false;

                await Application.Current.Dispatcher.InvokeAsync(() => {
                    var openFileDialog = new OpenFileDialog
                    {
                        Title = "Select GGUF Model File",
                        Filter = "GGUF Files (*.gguf)|*.gguf|All files (*.*)|*.*",
                        CheckFileExists = true,
                        Multiselect = false
                    };

                    if (openFileDialog.ShowDialog() == true)
                    {
                        selectedFilePath = openFileDialog.FileName;
                        fileSelected = true;
                    }
                });

                if (!fileSelected || string.IsNullOrEmpty(selectedFilePath))
                {
                    await Application.Current.Dispatcher.InvokeAsync(() => {
                        MessageBox.Show(
                            "No model file was selected. Current model will remain unchanged.",
                            "Model Selection Cancelled",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    });
                    return;
                }

                // Verify the selected file is valid
                try
                {
                    if (!File.Exists(selectedFilePath))
                    {
                        throw new FileNotFoundException("The selected file does not exist.");
                    }

                    // Check file size - warn if too large (might cause memory issues)
                    var fileInfo = new FileInfo(selectedFilePath);
                    if (fileInfo.Length > 2_500_000_000) // ~2.5GB
                    {
                        MessageBoxResult sizeWarningResult = MessageBoxResult.No;
                        await Application.Current.Dispatcher.InvokeAsync(() => {
                            sizeWarningResult = MessageBox.Show(
                                $"The selected model is very large ({FormatFileSize(fileInfo.Length)}).\n\n" +
                                "Large models may cause memory issues or slow performance.\n" +
                                "It's recommended to use smaller models (1-2GB) or quantized versions (Q4_0).\n\n" +
                                "Do you want to continue with this model anyway?",
                                "Large Model Warning",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Warning);
                        });

                        if (sizeWarningResult == MessageBoxResult.No)
                        {
                            continue; // Let the user select a different model
                        }
                    }
                    else if (fileInfo.Length < 10_000_000) // ~10MB
                    {
                        MessageBoxResult smallWarningResult = MessageBoxResult.No;
                        await Application.Current.Dispatcher.InvokeAsync(() => {
                            smallWarningResult = MessageBox.Show(
                                $"The selected file is very small ({FormatFileSize(fileInfo.Length)}).\n\n" +
                                "This might not be a valid GGUF model file.\n\n" +
                                "Do you want to continue with this file anyway?",
                                "Small File Warning",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Warning);
                        });

                        if (smallWarningResult == MessageBoxResult.No)
                        {
                            continue; // Let the user select a different model
                        }
                    }

                    // Check GGUF magic number (simple validation)
                    bool isValidGguf = false;
                    using (var fileStream = new FileStream(selectedFilePath, FileMode.Open, FileAccess.Read))
                    {
                        if (fileStream.Length >= 4)
                        {
                            byte[] header = new byte[4];
                            fileStream.Read(header, 0, 4);
                            // GGUF files start with "GGUF" in ASCII (71 71 85 70)
                            isValidGguf = (header[0] == 71 && header[1] == 71 && header[2] == 85 && header[3] == 70);
                        }
                    }

                    if (!isValidGguf)
                    {
                        MessageBoxResult formatWarningResult = MessageBoxResult.No;
                        await Application.Current.Dispatcher.InvokeAsync(() => {
                            formatWarningResult = MessageBox.Show(
                                "The selected file doesn't appear to be a valid GGUF format model.\n\n" +
                                "Do you want to try a different file?",
                                "Invalid Model Format",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Warning);
                        });

                        if (formatWarningResult == MessageBoxResult.Yes)
                        {
                            continue; // Let the user select a different model
                        }
                    }

                    // Save the selected model path
                    StoreModelPath(selectedFilePath);
                    modelConfigured = true;

                    await Application.Current.Dispatcher.InvokeAsync(() => {
                        MessageBox.Show(
                            $"Model successfully changed to:\n{Path.GetFileName(selectedFilePath)}\n" +
                            $"Size: {FormatFileSize(fileInfo.Length)}",
                            "Model Changed",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                    });
                }
                catch (Exception ex)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() => {
                        MessageBox.Show(
                            $"Error validating the selected model file: {ex.Message}\n\n" +
                            "Please try selecting a different GGUF model file.",
                            "Invalid Model File",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    });
                }
            }
        }

        // The rest of your ModelSelector.cs code remains the same
        // Config key to store the selected model path
        private const string ModelPathKey = "AIModelPath";

        public static async Task EnsureModelExists()
        {
            string modelsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models");

            // Create models directory if it doesn't exist
            if (!Directory.Exists(modelsDir))
                Directory.CreateDirectory(modelsDir);

            // Check if we already have a stored model path
            string storedModelPath = GetStoredModelPath();
            if (!string.IsNullOrEmpty(storedModelPath) && File.Exists(storedModelPath))
            {
                // Model already exists and is valid
                return;
            }

            // Ask user to select a model file
            MessageBoxResult result = MessageBoxResult.No;
            await Application.Current.Dispatcher.InvokeAsync(() => {
                result = MessageBox.Show(
                    "An AI model file (*.gguf) is required for CsNet Search functionality.\n\n" +
                    "Would you like to select a GGUF model file now?\n\n" +
                    "Recommended models (1-2GB in size):\n" +
                    "• phi-2-q4_0.gguf\n" +
                    "• tinyllama-1.1b-chat-v1.0.Q4_K_M.gguf\n" +
                    "• gemma-3-1b-it-Q4_0.gguf (smallest quantized version)\n\n" +
                    "You can download suitable models from:\n" +
                    "• https://huggingface.co/models?search=gguf\n" +
                    "• https://gpt4all.io/index.html",
                    "AI Model Required",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
            });

            if (result != MessageBoxResult.Yes)
            {
                await Application.Current.Dispatcher.InvokeAsync(() => {
                    MessageBox.Show(
                        "AI search will not be available without a model file.\n\n" +
                        "You can configure a model later by restarting the application.",
                        "AI Search Unavailable",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                });
                return;
            }

            // Reuse the ChangeModelFile method for the initial setup
            await ChangeModelFile();
        }

        // Store the model path in app settings
        private static void StoreModelPath(string modelPath)
        {
            try
            {
                // Create a simple settings file in the application directory
                string settingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_settings.txt");
                File.WriteAllText(settingsFile, modelPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not save model path: {ex.Message}\n\nYou may need to select the model again next time you start the application.",
                    "Settings Save Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        // Get the stored model path from app settings
        public static string GetStoredModelPath()
        {
            try
            {
                // Read from the simple settings file
                string settingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_settings.txt");
                if (File.Exists(settingsFile))
                {
                    string path = File.ReadAllText(settingsFile).Trim();
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        return path;
                    }
                }
            }
            catch
            {
                // If there's any error, just return null
            }
            return null;
        }

        private static string FormatFileSize(long bytes)
        {
            string[] suffix = { "B", "KB", "MB", "GB", "TB" };
            int i;
            double dblSByte = bytes;

            for (i = 0; i < suffix.Length && bytes >= 1024; i++, bytes /= 1024)
            {
                dblSByte = bytes / 1024.0;
            }

            return $"{dblSByte:0.##} {suffix[i]}";
        }
    }
}