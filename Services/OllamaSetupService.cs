using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Hephaistos.Services;

public sealed record OllamaSetupProgress(
    string Message,
    double? Percent = null
);

public sealed record OllamaSetupStatus(
    bool IsServerRunning,
    IReadOnlyList<string> MissingModels
)
{
    public bool IsReady =>
        IsServerRunning && MissingModels.Count == 0;
}

public sealed class OllamaSetupService : IDisposable
{
    private const string OllamaBaseUrl =
        "http://localhost:11434";

    private const string WindowsInstallerUrl =
        "https://ollama.com/download/OllamaSetup.exe";

    private readonly HttpClient _apiHttp;
    private readonly HttpClient _downloadHttp;

    public OllamaSetupService()
    {
        _apiHttp = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(3)
        };

        _downloadHttp = new HttpClient
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    public async Task<OllamaSetupStatus> GetStatusAsync(
        CancellationToken cancellationToken = default
    )
    {
        var serverRunning =
            await IsServerRunningAsync(
                cancellationToken
            );

        if (!serverRunning)
        {
            return new OllamaSetupStatus(
                false,
                new[]
                {
                    HephaistosSettings.ChatModel,
                    HephaistosSettings.EmbeddingModel
                }
            );
        }

        var installedModels =
            await GetInstalledModelsAsync(
                cancellationToken
            );

        var requiredModels =
            new[]
            {
                HephaistosSettings.ChatModel,
                HephaistosSettings.EmbeddingModel
            };

        var missingModels =
            requiredModels
                .Where(
                    required =>
                        !installedModels.Contains(
                            required,
                            StringComparer.OrdinalIgnoreCase
                        )
                )
                .ToList();

        return new OllamaSetupStatus(
            true,
            missingModels
        );
    }

    public async Task ConfigureAsync(
        IProgress<OllamaSetupProgress>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        progress?.Report(
            new OllamaSetupProgress(
                "Vérification du moteur d’IA locale…"
            )
        );

        var serverRunning =
            await IsServerRunningAsync(
                cancellationToken
            );

        if (!serverRunning)
        {
            progress?.Report(
                new OllamaSetupProgress(
                    "Recherche d’une installation Ollama existante…"
                )
            );

            var startedExisting =
                TryStartInstalledOllama();

            if (startedExisting)
            {
                serverRunning =
                    await WaitForServerAsync(
                        TimeSpan.FromSeconds(12),
                        cancellationToken
                    );
            }
        }

        if (!serverRunning)
        {
            var installerPath =
                await DownloadInstallerAsync(
                    progress,
                    cancellationToken
                );

            progress?.Report(
                new OllamaSetupProgress(
                    "L’installateur Ollama va s’ouvrir. Suivez simplement ses étapes, puis revenez à Héphaïstos."
                )
            );

            await RunInstallerAsync(
                installerPath,
                cancellationToken
            );

            serverRunning =
                await WaitForServerAsync(
                    TimeSpan.FromSeconds(20),
                    cancellationToken
                );

            if (!serverRunning)
            {
                TryStartInstalledOllama();

                serverRunning =
                    await WaitForServerAsync(
                        TimeSpan.FromSeconds(20),
                        cancellationToken
                    );
            }
        }

        if (!serverRunning)
        {
            throw new InvalidOperationException(
                "Ollama semble installé, mais son moteur local ne répond pas encore. " +
                "Vous pouvez fermer puis relancer Héphaïstos, ou lancer Ollama depuis le menu Démarrer."
            );
        }

        var installedModels =
            await GetInstalledModelsAsync(
                cancellationToken
            );

        var requiredModels =
            new[]
            {
                HephaistosSettings.ChatModel,
                HephaistosSettings.EmbeddingModel
            };

        foreach (var model in requiredModels)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (
                installedModels.Contains(
                    model,
                    StringComparer.OrdinalIgnoreCase
                )
            )
            {
                progress?.Report(
                    new OllamaSetupProgress(
                        $"{model} est déjà présent."
                    )
                );

                continue;
            }

            await PullModelAsync(
                model,
                progress,
                cancellationToken
            );

            installedModels.Add(model);
        }

        progress?.Report(
            new OllamaSetupProgress(
                "Configuration terminée. L’IA locale est prête.",
                100
            )
        );
    }

    public async Task<bool> IsServerRunningAsync(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            using var response =
                await _apiHttp.GetAsync(
                    $"{OllamaBaseUrl}/api/version",
                    cancellationToken
                );

            return response.IsSuccessStatusCode;
        }
        catch (
            Exception ex
        ) when (
            ex is HttpRequestException or TaskCanceledException
        )
        {
            return false;
        }
    }

    private async Task<List<string>> GetInstalledModelsAsync(
        CancellationToken cancellationToken
    )
    {
        using var response =
            await _apiHttp.GetAsync(
                $"{OllamaBaseUrl}/api/tags",
                cancellationToken
            );

        response.EnsureSuccessStatusCode();

        var json =
            await response.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: cancellationToken
            );

        if (
            !json.TryGetProperty(
                "models",
                out var modelsElement
            ) ||
            modelsElement.ValueKind != JsonValueKind.Array
        )
        {
            return new List<string>();
        }

        var models =
            new List<string>();

        foreach (var modelElement in modelsElement.EnumerateArray())
        {
            var name =
                modelElement.TryGetProperty(
                    "name",
                    out var nameElement
                )
                    ? nameElement.GetString()
                    : null;

            name ??=
                modelElement.TryGetProperty(
                    "model",
                    out var modelNameElement
                )
                    ? modelNameElement.GetString()
                    : null;

            if (!string.IsNullOrWhiteSpace(name))
            {
                models.Add(name);
            }
        }

        return models;
    }

    private async Task<string> DownloadInstallerAsync(
        IProgress<OllamaSetupProgress>? progress,
        CancellationToken cancellationToken
    )
    {
        var tempDirectory =
            Path.Combine(
                Path.GetTempPath(),
                "Hephaistos"
            );

        Directory.CreateDirectory(
            tempDirectory
        );

        var installerPath =
            Path.Combine(
                tempDirectory,
                "OllamaSetup.exe"
            );

        progress?.Report(
            new OllamaSetupProgress(
                "Téléchargement d’Ollama…",
                0
            )
        );

        using var response =
            await _downloadHttp.GetAsync(
                WindowsInstallerUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken
            );

        response.EnsureSuccessStatusCode();

        var contentLength =
            response.Content.Headers.ContentLength;

        await using var input =
            await response.Content.ReadAsStreamAsync(
                cancellationToken
            );

        await using var output =
            new FileStream(
                installerPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 131072,
                useAsync: true
            );

        var buffer =
            new byte[131072];

        long totalRead = 0;

        while (true)
        {
            var read =
                await input.ReadAsync(
                    buffer.AsMemory(0, buffer.Length),
                    cancellationToken
                );

            if (read == 0)
                break;

            await output.WriteAsync(
                buffer.AsMemory(0, read),
                cancellationToken
            );

            totalRead += read;

            double? percent = null;

            if (contentLength is > 0)
            {
                percent =
                    Math.Clamp(
                        totalRead * 100d / contentLength.Value,
                        0,
                        100
                    );
            }

            progress?.Report(
                new OllamaSetupProgress(
                    contentLength is > 0
                        ? $"Téléchargement d’Ollama — {FormatBytes(totalRead)} / {FormatBytes(contentLength.Value)}"
                        : $"Téléchargement d’Ollama — {FormatBytes(totalRead)}",
                    percent
                )
            );
        }

        return installerPath;
    }

    private static async Task RunInstallerAsync(
        string installerPath,
        CancellationToken cancellationToken
    )
    {
        var process =
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = installerPath,
                    UseShellExecute = true
                }
            );

        if (process == null)
        {
            throw new InvalidOperationException(
                "Impossible de lancer l’installateur Ollama."
            );
        }

        await process.WaitForExitAsync(
            cancellationToken
        );

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "L’installation d’Ollama n’a pas été menée à son terme."
            );
        }
    }

    private async Task PullModelAsync(
        string model,
        IProgress<OllamaSetupProgress>? progress,
        CancellationToken cancellationToken
    )
    {
        progress?.Report(
            new OllamaSetupProgress(
                $"Téléchargement du modèle {model}…",
                0
            )
        );

        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                $"{OllamaBaseUrl}/api/pull"
            )
            {
                Content = JsonContent.Create(
                    new
                    {
                        model,
                        stream = true
                    }
                )
            };

        using var response =
            await _downloadHttp.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken
            );

        response.EnsureSuccessStatusCode();

        await using var stream =
            await response.Content.ReadAsStreamAsync(
                cancellationToken
            );

        using var reader =
            new StreamReader(
                stream,
                Encoding.UTF8
            );

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line =
                await reader.ReadLineAsync(
                    cancellationToken
                );

            if (line == null)
                break;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                using var document =
                    JsonDocument.Parse(line);

                var root =
                    document.RootElement;

                var status =
                    root.TryGetProperty(
                        "status",
                        out var statusElement
                    )
                        ? statusElement.GetString()
                        : null;

                long? total =
                    root.TryGetProperty(
                        "total",
                        out var totalElement
                    ) &&
                    totalElement.TryGetInt64(
                        out var totalValue
                    )
                        ? totalValue
                        : null;

                long? completed =
                    root.TryGetProperty(
                        "completed",
                        out var completedElement
                    ) &&
                    completedElement.TryGetInt64(
                        out var completedValue
                    )
                        ? completedValue
                        : null;

                double? percent = null;
                string details = "";

                if (
                    total is > 0 &&
                    completed is >= 0
                )
                {
                    percent =
                        Math.Clamp(
                            completed.Value * 100d / total.Value,
                            0,
                            100
                        );

                    details =
                        $" — {FormatBytes(completed.Value)} / {FormatBytes(total.Value)}";
                }

                progress?.Report(
                    new OllamaSetupProgress(
                        $"{model} — {status ?? "téléchargement"}{details}",
                        percent
                    )
                );
            }
            catch (JsonException)
            {
                // Une ligne de progression mal formée ne doit pas interrompre le téléchargement.
            }
        }

        progress?.Report(
            new OllamaSetupProgress(
                $"{model} est prêt.",
                100
            )
        );
    }

    private async Task<bool> WaitForServerAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken
    )
    {
        var deadline =
            DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (
                await IsServerRunningAsync(
                    cancellationToken
                )
            )
            {
                return true;
            }

            await Task.Delay(
                700,
                cancellationToken
            );
        }

        return false;
    }

    private static bool TryStartInstalledOllama()
    {
        var executablePath =
            FindInstalledOllamaExecutable();

        if (string.IsNullOrWhiteSpace(executablePath))
            return false;

        try
        {
            Process.Start(
                new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = "serve",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                }
            );

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? FindInstalledOllamaExecutable()
    {
        var localAppData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData
            );

        var commonPath =
            Path.Combine(
                localAppData,
                "Programs",
                "Ollama",
                "ollama.exe"
            );

        if (File.Exists(commonPath))
            return commonPath;

        var pathValue =
            Environment.GetEnvironmentVariable(
                "PATH"
            );

        if (string.IsNullOrWhiteSpace(pathValue))
            return null;

        foreach (
            var directory in pathValue.Split(
                Path.PathSeparator,
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries
            )
        )
        {
            try
            {
                var candidate =
                    Path.Combine(
                        directory,
                        "ollama.exe"
                    );

                if (File.Exists(candidate))
                    return candidate;
            }
            catch
            {
                // Ignorer un segment PATH invalide.
            }
        }

        return null;
    }

    private static string FormatBytes(
        long value
    )
    {
        string[] units =
            [
                "o",
                "Ko",
                "Mo",
                "Go"
            ];

        double size = value;
        var unitIndex = 0;

        while (
            size >= 1024 &&
            unitIndex < units.Length - 1
        )
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:0.#} {units[unitIndex]}";
    }

    public void Dispose()
    {
        _apiHttp.Dispose();
        _downloadHttp.Dispose();
    }
}
