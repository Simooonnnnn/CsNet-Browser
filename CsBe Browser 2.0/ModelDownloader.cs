using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;

namespace CsBe_Browser_2._0
{
    public class ModelDownloader
    {
        // You would replace this with the actual URL for the Gemma model
        private const string ModelDownloadUrl = "https://huggingface.co/TheBloke/gemma-3-1b-it-GGUF/resolve/main/gemma-3-1b-it-Q5_K_M.gguf";
        private const string ModelFileName = "gemma-3-1b-it-Q5_K_M.gguf";

        public static async Task EnsureModelExists()
        {
            string modelsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Models");
            string modelPath = Path.Combine(modelsDir, ModelFileName);

            // If the model already exists, no need to download
            if (File.Exists(modelPath))
                return;

            // Create models directory if it doesn't exist
            if (!Directory.Exists(modelsDir))
                Directory.CreateDirectory(modelsDir);

            // Ask user if they want to download the model
            var result = MessageBox.Show(
                $"The required AI model file ({ModelFileName}) was not found. Would you like to download it now? (Size: ~1.2 GB)",
                "Model Download Required",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                MessageBox.Show(
                    "AI search will not be available without the model file. You can manually place the file in the Models folder or try again later.",
                    "Download Cancelled",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            try
            {
                // Create progress window
                var progressWindow = new Window
                {
                    Title = "Downloading AI Model",
                    Width = 400,
                    Height = 150,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    ResizeMode = ResizeMode.NoResize
                };

                var grid = new System.Windows.Controls.Grid();
                progressWindow.Content = grid;

                grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
                grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });

                var textBlock = new System.Windows.Controls.TextBlock
                {
                    Text = "Downloading AI model...",
                    Margin = new Thickness(10),
                    TextAlignment = TextAlignment.Center
                };
                System.Windows.Controls.Grid.SetRow(textBlock, 0);
                grid.Children.Add(textBlock);

                var progressBar = new System.Windows.Controls.ProgressBar
                {
                    Margin = new Thickness(10),
                    Height = 20,
                    IsIndeterminate = true
                };
                System.Windows.Controls.Grid.SetRow(progressBar, 1);
                grid.Children.Add(progressBar);

                var statusText = new System.Windows.Controls.TextBlock
                {
                    Text = "Starting download...",
                    Margin = new Thickness(10),
                    TextAlignment = TextAlignment.Center
                };
                System.Windows.Controls.Grid.SetRow(statusText, 2);
                grid.Children.Add(statusText);

                progressWindow.Show();

                // Start download
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(30); // Set a long timeout for large files

                    // Get the file size first to show progress
                    var response = await client.GetAsync(ModelDownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    var totalBytes = response.Content.Headers.ContentLength ?? 0;
                    progressBar.IsIndeterminate = false;
                    progressBar.Maximum = 100;

                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(modelPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        var buffer = new byte[8192];
                        long totalBytesRead = 0;
                        int bytesRead;

                        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead);

                            totalBytesRead += bytesRead;
                            double percentage = (double)totalBytesRead / totalBytes * 100;

                            // Update UI on the UI thread
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                progressBar.Value = percentage;
                                statusText.Text = $"Downloaded {FormatFileSize(totalBytesRead)} of {FormatFileSize(totalBytes)} ({percentage:F1}%)";
                            });
                        }
                    }
                }

                progressWindow.Close();

                MessageBox.Show(
                    "Model download completed successfully!",
                    "Download Complete",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Failed to download model: {ex.Message}",
                    "Download Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                // Clean up partial file if it exists
                if (File.Exists(modelPath))
                {
                    try { File.Delete(modelPath); } catch { /* Ignore cleanup errors */ }
                }
            }
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