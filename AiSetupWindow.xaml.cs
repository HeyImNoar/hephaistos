using Hephaistos.Services;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace Hephaistos;

public partial class AiSetupWindow : Window
{
    private readonly OllamaSetupService _setupService =
        new();

    private readonly bool _isRequiredAtStartup;
    private CancellationTokenSource? _setupCancellation;
    private bool _isConfigured;
    private bool _isWorking;

    public bool IsConfigured =>
        _isConfigured;

    public AiSetupWindow(
        bool isRequiredAtStartup = false
    )
    {
        _isRequiredAtStartup =
            isRequiredAtStartup;

        InitializeComponent();

        if (_isRequiredAtStartup)
        {
            Title =
                "Première configuration — Héphaïstos";

            WindowStartupLocation =
                WindowStartupLocation.CenterScreen;

            SetupSubtitleTextBlock.Text =
                "Une étape nécessaire avant d’ouvrir Héphaïstos";

            SetupIntroTitleTextBlock.Text =
                "Héphaïstos a besoin de son IA locale pour fonctionner.";

            SetupIntroBodyTextBlock.Text =
                "Cette configuration n’est nécessaire qu’une fois. Héphaïstos installe le moteur local puis télécharge les modèles nécessaires. Vos documents restent traités sur cet ordinateur.";

            FooterHintTextBlock.Text =
                "Une connexion Internet est nécessaire uniquement pour cette installation initiale.";

            LaterButton.Content =
                "Quitter Héphaïstos";
        }

        Loaded +=
            AiSetupWindow_Loaded;

        Closing +=
            AiSetupWindow_Closing;

        Closed +=
            AiSetupWindow_Closed;
    }

    private async void AiSetupWindow_Loaded(
        object sender,
        RoutedEventArgs e
    )
    {
        await RefreshStatusAsync();
    }

    private async Task RefreshStatusAsync()
    {
        try
        {
            StatusTextBlock.Text =
                "Vérification de l’IA locale…";

            StatusDetailsTextBlock.Text = "";
            StatusDot.Fill =
                new SolidColorBrush(
                    Color.FromRgb(
                        154,
                        167,
                        162
                    )
                );

            var status =
                await _setupService.GetStatusAsync();

            if (status.IsReady)
            {
                SetReadyState();
                return;
            }

            _isConfigured = false;
            ConfigureButton.Content =
                _isRequiredAtStartup
                    ? "Installer l’IA locale"
                    : "Configurer maintenant";

            UpdateSecondaryButton();

            if (!status.IsServerRunning)
            {
                StatusTextBlock.Text =
                    "Le moteur d’IA locale doit être installé.";

                StatusDetailsTextBlock.Text =
                    "Héphaïstos peut télécharger et lancer automatiquement l’installateur officiel d’Ollama.";
            }
            else
            {
                StatusTextBlock.Text =
                    "Le moteur est prêt, mais certains modèles manquent.";

                StatusDetailsTextBlock.Text =
                    "À télécharger : " +
                    string.Join(
                        ", ",
                        status.MissingModels
                    );
            }
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text =
                "Impossible de vérifier la configuration.";

            StatusDetailsTextBlock.Text =
                ex.Message;

            ConfigureButton.Content =
                _isRequiredAtStartup
                    ? "Réessayer / installer"
                    : "Configurer maintenant";

            UpdateSecondaryButton();
        }
    }

    private async void ConfigureButton_Click(
        object sender,
        RoutedEventArgs e
    )
    {
        if (_isConfigured)
        {
            DialogResult = true;
            return;
        }

        if (_isWorking)
            return;

        _isWorking = true;
        _setupCancellation =
            new CancellationTokenSource();

        ConfigureButton.IsEnabled = false;
        LaterButton.Content = "Annuler";
        SetupProgressBar.Visibility =
            Visibility.Visible;
        SetupProgressBar.IsIndeterminate = true;

        var progress =
            new Progress<OllamaSetupProgress>(
                update =>
                {
                    ProgressTextBlock.Text =
                        update.Message;

                    if (update.Percent.HasValue)
                    {
                        SetupProgressBar.IsIndeterminate = false;
                        SetupProgressBar.Value =
                            update.Percent.Value;
                    }
                    else
                    {
                        SetupProgressBar.IsIndeterminate = true;
                    }
                }
            );

        try
        {
            await _setupService.ConfigureAsync(
                progress,
                _setupCancellation.Token
            );

            SetReadyState();
        }
        catch (OperationCanceledException)
        {
            ProgressTextBlock.Text =
                "Configuration interrompue.";

            await RefreshStatusAsync();
        }
        catch (Exception ex)
        {
            StatusDot.Fill =
                new SolidColorBrush(
                    Color.FromRgb(
                        181,
                        111,
                        74
                    )
                );

            StatusTextBlock.Text =
                "La configuration n’a pas pu être terminée.";

            StatusDetailsTextBlock.Text =
                ex.Message;

            ProgressTextBlock.Text =
                "Vous pouvez réessayer. Aucune donnée de vos documents n’a été envoyée pendant cette opération.";
        }
        finally
        {
            _isWorking = false;
            ConfigureButton.IsEnabled = true;

            _setupCancellation?.Dispose();
            _setupCancellation = null;

            if (!_isConfigured)
            {
                UpdateSecondaryButton();

                SetupProgressBar.IsIndeterminate = false;
                SetupProgressBar.Visibility =
                    Visibility.Collapsed;
            }
        }
    }

    private void SetReadyState()
    {
        _isConfigured = true;

        StatusDot.Fill =
            new SolidColorBrush(
                Color.FromRgb(
                    111,
                    142,
                    130
                )
            );

        StatusTextBlock.Text =
            "L’IA locale est prête.";

        StatusDetailsTextBlock.Text =
            $"{HephaistosSettings.ChatModel} et {HephaistosSettings.EmbeddingModel} sont disponibles.";

        ProgressTextBlock.Text =
            "Héphaïstos peut maintenant analyser et interroger vos documents.";

        SetupProgressBar.IsIndeterminate = false;
        SetupProgressBar.Value = 100;
        SetupProgressBar.Visibility =
            Visibility.Visible;

        ConfigureButton.Content =
            _isRequiredAtStartup
                ? "Ouvrir Héphaïstos"
                : "Fermer";

        LaterButton.Visibility =
            Visibility.Collapsed;
    }

    private void UpdateSecondaryButton()
    {
        LaterButton.Visibility =
            Visibility.Visible;

        LaterButton.Content =
            _isRequiredAtStartup
                ? "Quitter Héphaïstos"
                : "Plus tard";
    }

    private void LaterButton_Click(
        object sender,
        RoutedEventArgs e
    )
    {
        if (_isWorking)
        {
            _setupCancellation?.Cancel();
            return;
        }

        DialogResult = false;
    }

    private void AiSetupWindow_Closing(
        object? sender,
        CancelEventArgs e
    )
    {
        if (_isWorking)
        {
            _setupCancellation?.Cancel();
        }
    }

    private void AiSetupWindow_Closed(
        object? sender,
        EventArgs e
    )
    {
        _setupCancellation?.Cancel();
        _setupCancellation?.Dispose();
        _setupService.Dispose();
    }
}
