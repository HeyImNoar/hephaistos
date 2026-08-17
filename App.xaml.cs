using Hephaistos.Services;
using System.Diagnostics;
using System.Windows;

namespace Hephaistos;

public partial class App : Application
{
    protected override async void OnStartup(
        StartupEventArgs e
    )
    {
        base.OnStartup(e);

        // L'application contrôle explicitement sa séquence de lancement :
        // splash -> vérification IA -> éventuelle configuration -> fenêtre principale.
        ShutdownMode =
            ShutdownMode.OnExplicitShutdown;

        var splashWindow =
            new SplashWindow();

        splashWindow.Show();
        splashWindow.SetStatus(
            "Préparation de votre espace…",
            "Vérification de l’IA locale"
        );

        // Un temps d'affichage minimal évite que le splash ne clignote
        // sur les machines rapides, tout en laissant les vraies opérations
        // de démarrage se faire en parallèle.
        var splashStopwatch =
            Stopwatch.StartNew();

        var aiReady = false;

        try
        {
            using var setupService =
                new OllamaSetupService();

            var status =
                await setupService.GetStatusAsync();

            aiReady =
                status.IsReady;
        }
        catch
        {
            // En cas d'échec de la vérification, l'assistant IA donnera
            // un diagnostic plus utile et permettra de réessayer.
            aiReady = false;
        }

        if (!aiReady)
        {
            await EnsureMinimumSplashDurationAsync(
                splashStopwatch,
                minimumMilliseconds: 850
            );

            splashWindow.Close();

            var setupWindow =
                new AiSetupWindow(
                    isRequiredAtStartup: true
                );

            var configured =
                setupWindow.ShowDialog() == true &&
                setupWindow.IsConfigured;

            if (!configured)
            {
                Shutdown();
                return;
            }

            // Après une première installation de l'IA, on remet brièvement
            // le splash pendant la création de l'espace principal.
            splashWindow =
                new SplashWindow();

            splashWindow.SetStatus(
                "Tout est prêt",
                "Ouverture d’Héphaïstos"
            );

            splashWindow.Show();
            splashStopwatch =
                Stopwatch.StartNew();
        }
        else
        {
            splashWindow.SetStatus(
                "Tout est prêt",
                "Ouverture d’Héphaïstos"
            );
        }

        // La construction de MainWindow peut initialiser plusieurs services :
        // on garde le splash visible pendant cette étape.
        var mainWindow =
            new MainWindow();

        MainWindow =
            mainWindow;

        await EnsureMinimumSplashDurationAsync(
            splashStopwatch,
            minimumMilliseconds: 1100
        );

        splashWindow.Close();

        ShutdownMode =
            ShutdownMode.OnMainWindowClose;

        mainWindow.Show();
        mainWindow.Activate();
    }

    private static async Task EnsureMinimumSplashDurationAsync(
        Stopwatch stopwatch,
        int minimumMilliseconds
    )
    {
        var remaining =
            minimumMilliseconds -
            (int)stopwatch.ElapsedMilliseconds;

        if (remaining > 0)
        {
            await Task.Delay(remaining);
        }
    }
}
