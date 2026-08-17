using System.IO;
using System.Windows;
using System.Windows.Media;

namespace Hephaistos;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        VersionTextBlock.Text = HephaistosSettings.DisplayVersion;
        ApplySavedTheme();
    }

    public void SetStatus(
        string status,
        string? detail = null
    )
    {
        StatusTextBlock.Text = status;

        if (detail is not null)
        {
            DetailTextBlock.Text = detail;
        }
    }

    private void ApplySavedTheme()
    {
        if (!IsDarkModeSaved())
        {
            return;
        }

        SetBrush("SplashAccentBrush", "#D28A62");
        SetBrush("SplashBackgroundBrush", "#171D1B");
        SetBrush("SplashSurfaceBrush", "#202725");
        SetBrush("SplashInkBrush", "#E8EDEA");
        SetBrush("SplashMutedBrush", "#A5B0AC");
        SetBrush("SplashLineBrush", "#36413D");
        SetBrush("SplashTrackBrush", "#303A36");
    }

    private static bool IsDarkModeSaved()
    {
        try
        {
            var themeFile =
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData
                    ),
                    "Hephaistos",
                    "ui-theme.txt"
                );

            return
                File.Exists(themeFile) &&
                string.Equals(
                    File.ReadAllText(themeFile).Trim(),
                    "dark",
                    StringComparison.OrdinalIgnoreCase
                );
        }
        catch
        {
            return false;
        }
    }

    private void SetBrush(
        string resourceKey,
        string color
    )
    {
        Resources[resourceKey] =
            new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(color)
            );
    }
}
