using System.Threading.Tasks;
using System.Windows;

namespace CsBe_Browser_2._0
{
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Initialize theme manager
            ThemeManager.Initialize();

            // Check for AI model and offer to download if needed
            await Task.Run(async () => {
                await ModelDownloader.EnsureModelExists();
            });
        }
    }
}