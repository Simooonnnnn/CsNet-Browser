using System;
using System.Windows;
using System.Windows.Media;

namespace CsBe_Browser_2._0
{
    public class ThemeManager
    {
        public enum Theme { Light, Dark }
        public static event EventHandler<Theme> ThemeChanged;

        private static Theme _currentTheme = Theme.Light;
        public static Theme CurrentTheme
        {
            get => _currentTheme;
            set
            {
                if (_currentTheme != value)
                {
                    _currentTheme = value;
                    ThemeChanged?.Invoke(null, value);
                    UpdateAppTheme(value);
                }
            }
        }

        public static void Initialize()
        {
            // Get Windows theme
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key?.GetValue("AppsUseLightTheme") is int value)
            {
                CurrentTheme = value == 0 ? Theme.Dark : Theme.Light;
            }

            // Listen for Windows theme changes
            Microsoft.Win32.SystemEvents.UserPreferenceChanged += (s, e) =>
            {
                if (e.Category == Microsoft.Win32.UserPreferenceCategory.General)
                {
                    using var themeKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                    if (themeKey?.GetValue("AppsUseLightTheme") is int themeValue)
                    {
                        CurrentTheme = themeValue == 0 ? Theme.Dark : Theme.Light;
                    }
                }
            };
        }

        private static void UpdateAppTheme(Theme theme)
        {
            var app = Application.Current;
            var resources = app.Resources;

            if (theme == Theme.Dark)
            {
                resources["BackgroundColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#202124"));
                resources["ForegroundColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e8eaed"));
                resources["BorderColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5f6368"));
                resources["TabBackgroundColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#292a2d"));
                resources["TabHoverColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3c4043"));
                resources["SearchBarBackgroundColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#202124"));
            }
            else
            {
                resources["BackgroundColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ffffff"));
                resources["ForegroundColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#202124"));
                resources["BorderColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e8eaed"));
                resources["TabBackgroundColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ffffff"));
                resources["TabHoverColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f5f5f5"));
                resources["SearchBarBackgroundColor"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ffffff"));
            }

            // Update UI elements that need immediate refresh
            if (Application.Current.MainWindow != null)
            {
                Application.Current.MainWindow.Background = theme == Theme.Dark
                    ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#202124"))
                    : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ffffff"));
            }

            foreach (Window window in Application.Current.Windows)
            {
                window.UpdateLayout();
            }
        }
    }
}