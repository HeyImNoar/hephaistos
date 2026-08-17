using Hephaistos.Models;
using Hephaistos.Services;
using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Net.Http;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Reflection;

namespace Hephaistos;

public partial class MainWindow : Window
{
    private readonly HttpClient _http;

    private readonly OcrService _ocrService;
    private readonly PdfService _pdfService;
    private readonly ChunkingService _chunkingService;
    private readonly EmbeddingService _embeddingService;
    private readonly SemanticSearchService _searchService;
    private readonly LlmService _llmService;
    private readonly SummaryService _summaryService;
    private readonly TimelineService _timelineService;
    private readonly StructuredExtractionService _structuredExtractionService;
    private readonly ComparisonService _comparisonService;
    private readonly ContradictionService _contradictionService;
    private readonly ValidationService _validationService;
    private readonly IndexStorageService _indexStorageService;
    

    private List<DocumentChunk> _chunks = [];
    private readonly ObservableCollection<ChatMessage> _chatMessages = [];
    private readonly ObservableCollection<AnalysisHistoryItem> _summaryHistory = [];
    private readonly ObservableCollection<AnalysisHistoryItem> _timelineHistory = [];
    private readonly ObservableCollection<AnalysisHistoryItem> _extractionHistory = [];
    private readonly ObservableCollection<AnalysisHistoryItem> _comparisonHistory = [];
    private readonly ObservableCollection<AnalysisHistoryItem> _contradictionHistory = [];
    private readonly ObservableCollection<AnalysisHistoryItem> _diagnosticHistory = [];
    private readonly List<string> _loadedPdfPaths = [];
    private readonly Dictionary<string, string> _documentPathsByName =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (long FileSize, DateTime LastWriteTimeUtc)> _loadedPdfStates =
        new(StringComparer.OrdinalIgnoreCase);
    private string _currentFolderPath = "";
    private CancellationTokenSource? _importCancellation;
    private CancellationTokenSource? _summaryCancellation;
    private CancellationTokenSource? _timelineCancellation;
    private CancellationTokenSource? _extractionCancellation;
    private CancellationTokenSource? _comparisonCancellation;
    private CancellationTokenSource? _contradictionCancellation;
    private bool _isBusy;
    private bool _aiSetupChecked;
    private bool _isDarkMode;

    public MainWindow()
    {
        InitializeComponent();

        VersionRun.Text = GetDisplayVersion();
        LoadThemePreference();

        ChatMessagesItemsControl.ItemsSource =
            _chatMessages;

        SummaryHistoryComboBox.ItemsSource = _summaryHistory;
        TimelineHistoryComboBox.ItemsSource = _timelineHistory;
        ExtractionHistoryComboBox.ItemsSource = _extractionHistory;
        ComparisonHistoryComboBox.ItemsSource = _comparisonHistory;
        ContradictionHistoryComboBox.ItemsSource = _contradictionHistory;
        DiagnosticHistoryComboBox.ItemsSource = _diagnosticHistory;

        var httpHandler =
    new HttpClientHandler
    {
        UseProxy =
            false,

        AllowAutoRedirect =
            false
    };

_http =
    new HttpClient(
        httpHandler
    );

        // --------------------------------------------------
        // OCR
        // --------------------------------------------------

        var tessDataPath =
            Path.Combine(
                AppContext.BaseDirectory,
                "tessdata"
            );

        _ocrService =
            new OcrService(
                tessDataPath
            );

        _pdfService =
            new PdfService(
                _ocrService
            );

        // --------------------------------------------------
        // RAG
        // --------------------------------------------------

        _chunkingService =
            new ChunkingService(
                HephaistosSettings.ChunkSize,
                HephaistosSettings.ChunkOverlap
            );

        _embeddingService =
            new EmbeddingService(
                _http
            );

        _searchService =
            new SemanticSearchService(
                _embeddingService
            );

        _llmService =
            new LlmService(
                _http
            );
        _summaryService =
    new SummaryService(
        _llmService
    );
        _timelineService =
    new TimelineService(
        _llmService
    );
        _structuredExtractionService =
    new StructuredExtractionService(
        _llmService
    );
    _comparisonService =
    new ComparisonService(
        _llmService
    );

    _contradictionService =
        new ContradictionService(
            _llmService,
            _embeddingService
        );

        _validationService =
    new ValidationService(
        _searchService
    );

        // --------------------------------------------------
        // Index chiffrés
        // --------------------------------------------------

        var localAppData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData
            );

        var indexDirectory =
            Path.Combine(
                localAppData,
                "Hephaistos",
                "indexes"
            );

        _indexStorageService =
            new IndexStorageService(
                indexDirectory
            );

        Closed += MainWindow_Closed;
    }
    private async void MainWindow_Loaded(
        object sender,
        RoutedEventArgs e
    )
    {
        if (_aiSetupChecked)
            return;

        _aiSetupChecked = true;

        try
        {
            using var setupService =
                new OllamaSetupService();

            var status =
                await setupService.GetStatusAsync();

            if (status.IsReady)
                return;

            var setupWindow =
                new AiSetupWindow
                {
                    Owner = this
                };

            var configured =
                setupWindow.ShowDialog() == true &&
                setupWindow.IsConfigured;

            if (!configured)
            {
                StatusTextBlock.Text =
                    "IA locale à configurer — cliquez sur « IA locale » en haut à droite.";
            }
            else
            {
                StatusTextBlock.Text =
                    "IA locale prête.";
            }
        }
        catch
        {
            StatusTextBlock.Text =
                "Impossible de vérifier l’IA locale — cliquez sur « IA locale » pour réessayer.";
        }
    }

    private static string GetDisplayVersion()
    {
        return HephaistosSettings.DisplayVersion;
    }

    private void ThemeToggleButton_Click(
        object sender,
        RoutedEventArgs e
    )
    {
        ApplyTheme(!_isDarkMode);
        SaveThemePreference();
    }

    private void LoadThemePreference()
    {
        try
        {
            var settingsDirectory =
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData
                    ),
                    "Hephaistos"
                );

            var themeFile =
                Path.Combine(
                    settingsDirectory,
                    "ui-theme.txt"
                );

            var useDarkMode =
                File.Exists(themeFile) &&
                string.Equals(
                    File.ReadAllText(themeFile).Trim(),
                    "dark",
                    StringComparison.OrdinalIgnoreCase
                );

            ApplyTheme(useDarkMode);
        }
        catch
        {
            ApplyTheme(false);
        }
    }

    private void SaveThemePreference()
    {
        try
        {
            var settingsDirectory =
                Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData
                    ),
                    "Hephaistos"
                );

            Directory.CreateDirectory(settingsDirectory);

            File.WriteAllText(
                Path.Combine(
                    settingsDirectory,
                    "ui-theme.txt"
                ),
                _isDarkMode
                    ? "dark"
                    : "light"
            );
        }
        catch
        {
            // Le thème reste fonctionnel pour la session même si
            // Windows refuse exceptionnellement d'enregistrer la préférence.
        }
    }

    private void ApplyTheme(
        bool darkMode
    )
    {
        _isDarkMode = darkMode;

        var colors =
            darkMode
                ? new Dictionary<string, string>
                {
                    ["AppBackgroundBrush"] = "#171D1B",
                    ["AccentBrush"] = "#D28A62",
                    ["AccentHoverBrush"] = "#E19A71",
                    ["AccentSoftBrush"] = "#3A2C27",
                    ["InkBrush"] = "#E8EDEA",
                    ["MutedBrush"] = "#A5B0AC",
                    ["LineBrush"] = "#36413D",
                    ["SurfaceBrush"] = "#202725",
                    ["SoftSurfaceBrush"] = "#1B2220",
                    ["HealthSoftBrush"] = "#25302C",
                    ["ButtonSurfaceBrush"] = "#252D2A",
                    ["ButtonHoverBrush"] = "#303A36",
                    ["ButtonHoverBorderBrush"] = "#4A5751",
                    ["ButtonPressedBrush"] = "#1D2422",
                    ["DangerBrush"] = "#E0A097",
                    ["TabHoverBrush"] = "#2B3431",
                    ["TabSelectedBorderBrush"] = "#47534E",
                    ["TabStripBrush"] = "#151A18",
                    ["SplitterBrush"] = "#58665F",
                    ["SourceHoverBrush"] = "#28312E",
                    ["SourceHoverBorderBrush"] = "#47544E",
                    ["SourceSelectedBorderBrush"] = "#8E624F",
                    ["HealthBorderBrush"] = "#394A43",
                    ["HealthDotBrush"] = "#86AA9B",
                    ["HealthTextBrush"] = "#BCD0C7",
                    ["ProgressTrackBrush"] = "#3A4641",
                    ["EmptyBorderBrush"] = "#303A36",
                    ["ChatBubbleBrush"] = "#252D2A",
                    ["TypingDotBrush"] = "#8E9A95",
                    ["EmptyIconTextBrush"] = "#A9B8B1"
                }
                : new Dictionary<string, string>
                {
                    ["AppBackgroundBrush"] = "#F6F7F5",
                    ["AccentBrush"] = "#B56F4A",
                    ["AccentHoverBrush"] = "#A96240",
                    ["AccentSoftBrush"] = "#F5EAE3",
                    ["InkBrush"] = "#30403D",
                    ["MutedBrush"] = "#6F7B78",
                    ["LineBrush"] = "#DDE2DF",
                    ["SurfaceBrush"] = "#FFFFFF",
                    ["SoftSurfaceBrush"] = "#FAFBFA",
                    ["HealthSoftBrush"] = "#EDF2EF",
                    ["ButtonSurfaceBrush"] = "#FFFFFF",
                    ["ButtonHoverBrush"] = "#F2F4F2",
                    ["ButtonHoverBorderBrush"] = "#CBD2CE",
                    ["ButtonPressedBrush"] = "#E9ECEA",
                    ["DangerBrush"] = "#8A5A51",
                    ["TabHoverBrush"] = "#EEF1EF",
                    ["TabSelectedBorderBrush"] = "#D8DEDA",
                    ["TabStripBrush"] = "#E9EDEA",
                    ["SplitterBrush"] = "#CAD2CE",
                    ["SourceHoverBrush"] = "#F6F8F6",
                    ["SourceHoverBorderBrush"] = "#CDD5D1",
                    ["SourceSelectedBorderBrush"] = "#DEB9A5",
                    ["HealthBorderBrush"] = "#E1E7E3",
                    ["HealthDotBrush"] = "#6F8E82",
                    ["HealthTextBrush"] = "#536460",
                    ["ProgressTrackBrush"] = "#DDE4E0",
                    ["EmptyBorderBrush"] = "#E5E9E6",
                    ["ChatBubbleBrush"] = "#F0F3F1",
                    ["TypingDotBrush"] = "#7E8986",
                    ["EmptyIconTextBrush"] = "#657671"
                };

        foreach (var pair in colors)
        {
            if (
                ColorConverter.ConvertFromString(pair.Value) is Color color
            )
            {
                // Les pinceaux XAML peuvent être gelés (Freezable) par WPF.
                // On remplace donc la ressource au lieu de modifier son Color
                // en place. Les DynamicResource du XAML se mettent à jour.
                Resources[pair.Key] =
                    new SolidColorBrush(color);
            }
        }

        ThemeToggleIcon.Text =
            darkMode
                ? "☀"
                : "☾";

        ThemeToggleText.Text =
            darkMode
                ? "Clair"
                : "Sombre";

        ThemeToggleButton.ToolTip =
            darkMode
                ? "Passer en mode clair"
                : "Passer en mode sombre";
    }

    private void AiSetupButton_Click(
        object sender,
        RoutedEventArgs e
    )
    {
        var setupWindow =
            new AiSetupWindow
            {
                Owner = this
            };

        var configured =
            setupWindow.ShowDialog() == true &&
            setupWindow.IsConfigured;

        StatusTextBlock.Text =
            configured
                ? "IA locale prête."
                : "Configuration de l’IA locale fermée.";
    }

    private void UpdateDocumentSummaries(
    IReadOnlyList<string>? _ = null
)
{
    var summaries =
        _chunks
            .GroupBy(
                chunk => chunk.DocumentName,
                StringComparer.OrdinalIgnoreCase
            )
            .Select(
                group =>
                {
                    _documentPathsByName.TryGetValue(
                        group.Key,
                        out var filePath
                    );

                    return new DocumentSummary
                    {
                        DocumentName = group.Key,
                        FilePath = filePath ?? "",
                        IndexedPages =
                            group
                                .Select(chunk => chunk.PageNumber)
                                .Distinct()
                                .Count(),
                        OcrPages =
                            group
                                .Where(chunk => chunk.WasOcr)
                                .Select(chunk => chunk.PageNumber)
                                .Distinct()
                                .Count(),
                        ChunkCount = group.Count()
                    };
                }
            )
            .OrderBy(summary => summary.DocumentName)
            .ToList();

    DocumentsListBox.ItemsSource = summaries;

    EmptyDocumentsPanel.Visibility =
        summaries.Count > 0
            ? Visibility.Collapsed
            : Visibility.Visible;

    UpdateCorpusLocationUi();
    UpdateDocumentSelectionUi();

    var totalPages = summaries.Sum(summary => summary.IndexedPages);
    var totalOcrPages = summaries.Sum(summary => summary.OcrPages);

    FolderStatsTextBlock.Text =
        summaries.Count == 0
            ? "Aucun document chargé."
            : $"{summaries.Count} PDF — " +
              $"{totalPages} pages — " +
              $"{totalOcrPages} pages OCR — " +
              $"{_chunks.Count} chunks";
}


    // ======================================================
    // SÉLECTION DU DOSSIER
    // ======================================================

    private async void SelectFolderButton_Click(
        object sender,
        RoutedEventArgs e
    )
    {
        var dialog =
            new OpenFileDialog
            {
                Title = "Ajouter des PDF à l'espace documentaire",
                Filter = "Documents PDF (*.pdf)|*.pdf",
                Multiselect = true,
                CheckFileExists = true
            };

        var result = dialog.ShowDialog();

        if (result != true || dialog.FileNames.Length == 0)
        {
            return;
        }

        await AddPdfFilesAsync(dialog.FileNames);
    }


    // ======================================================
    // CHARGEMENT / INDEXATION
    // ======================================================

    private async Task LoadFolderAsync(
        string folderPath
    )
    {
        var pdfPaths =
            Directory
                .GetFiles(
                    folderPath,
                    "*.pdf",
                    SearchOption.TopDirectoryOnly
                )
                .OrderBy(path => path)
                .ToList();

        if (pdfPaths.Count == 0)
        {
            MessageBox.Show(
                "Ce dossier ne contient aucun fichier PDF.",
                "Héphaïstos",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );

            StatusTextBlock.Text = "Aucun PDF trouvé.";
            return;
        }

        await AddPdfFilesAsync(pdfPaths);
    }

    private async Task AddPdfFilesAsync(
        IEnumerable<string> pdfPaths
    )
    {
        var requestedPaths =
            pdfPaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(Path.GetFullPath)
                .Where(File.Exists)
                .Where(
                    path =>
                        string.Equals(
                            Path.GetExtension(path),
                            ".pdf",
                            StringComparison.OrdinalIgnoreCase
                        )
                )
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        if (requestedPaths.Count == 0)
        {
            return;
        }

        var loadedPaths =
            _loadedPdfPaths.ToHashSet(
                StringComparer.OrdinalIgnoreCase
            );

        var pathsToProcess =
            new List<(string Path, bool IsReplacement, string ExistingDocumentName)>();

        var unchangedLoadedPaths =
            new List<string>();

        foreach (var path in requestedPaths)
        {
            if (!loadedPaths.Contains(path))
            {
                pathsToProcess.Add((path, false, ""));
                continue;
            }

            var currentState = GetFileState(path);

            if (
                _loadedPdfStates.TryGetValue(path, out var loadedState) &&
                loadedState == currentState
            )
            {
                unchangedLoadedPaths.Add(path);
                continue;
            }

            var existingDocumentName =
                FindDocumentNameForPath(path);

            pathsToProcess.Add(
                (
                    path,
                    !string.IsNullOrWhiteSpace(existingDocumentName),
                    existingDocumentName
                )
            );
        }

        if (pathsToProcess.Count == 0)
        {
            SelectDocumentsByPath(requestedPaths);
            StatusTextBlock.Text =
                requestedPaths.Count == 1
                    ? "Ce PDF est déjà chargé et n'a pas changé — aucune réindexation."
                    : "Ces PDF sont déjà chargés et n'ont pas changé — aucune réindexation.";
            return;
        }

        _importCancellation?.Dispose();
        _importCancellation =
            new CancellationTokenSource();

        var cancellationToken =
            _importCancellation.Token;

        try
        {
            var newCount =
                pathsToProcess.Count(item => !item.IsReplacement);

            var changedCount =
                pathsToProcess.Count(item => item.IsReplacement);

            SetBusy(
                true,
                pathsToProcess.Count == 1
                    ? changedCount == 1
                        ? "Mise à jour du document modifié..."
                        : "Ajout du document..."
                    : $"Mise à jour du corpus : {pathsToProcess.Count} document(s) à traiter..."
            );

            CancelButton.IsEnabled = true;

            SourcesListBox.ItemsSource = null;
            SourcePreviewTextBox.Clear();
            _chatMessages.Clear();

            var usedNames =
                _documentPathsByName.Keys.ToHashSet(
                    StringComparer.OrdinalIgnoreCase
                );

            var pendingDocuments =
                new List<(
                    string Path,
                    string Name,
                    List<DocumentChunk> Chunks,
                    bool IsReplacement,
                    long FileSize,
                    DateTime LastWriteTimeUtc
                )>();

            var chunksToEmbed =
                new List<DocumentChunk>();

            var indexCache =
                new Dictionary<string, DocumentIndex?>(
                    StringComparer.OrdinalIgnoreCase
                );

            var reusedDocumentCount = 0;

            for (int i = 0; i < pathsToProcess.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var workItem = pathsToProcess[i];
                var pdfPath = workItem.Path;

                var documentName =
                    workItem.IsReplacement
                        ? workItem.ExistingDocumentName
                        : CreateUniqueDocumentName(
                            pdfPath,
                            usedNames
                        );

                if (!workItem.IsReplacement)
                {
                    usedNames.Add(documentName);
                }

                var folderPath =
                    Path.GetDirectoryName(pdfPath) ?? "";

                if (!indexCache.TryGetValue(folderPath, out var savedIndex))
                {
                    savedIndex =
                        string.IsNullOrWhiteSpace(folderPath)
                            ? null
                            : await _indexStorageService.LoadAsync(folderPath);

                    indexCache[folderPath] = savedIndex;
                }

                cancellationToken.ThrowIfCancellationRequested();

                var info =
                    new FileInfo(pdfPath);

                List<DocumentChunk>? documentChunks = null;

                if (
                    savedIndex != null &&
                    _indexStorageService.IsBuildCompatible(savedIndex)
                )
                {
                    var savedDocument =
                        savedIndex.Documents.FirstOrDefault(
                            document =>
                                string.Equals(
                                    document.FileName,
                                    info.Name,
                                    StringComparison.OrdinalIgnoreCase
                                )
                        );

                    if (
                        savedDocument != null &&
                        savedDocument.FileSize == info.Length &&
                        savedDocument.LastWriteTimeUtc == info.LastWriteTimeUtc
                    )
                    {
                        var reusableChunks =
                            savedIndex.Chunks
                                .Where(
                                    chunk =>
                                        string.Equals(
                                            chunk.DocumentName,
                                            info.Name,
                                            StringComparison.OrdinalIgnoreCase
                                        )
                                )
                                .ToList();

                        if (reusableChunks.Count > 0)
                        {
                            documentChunks =
                                reusableChunks
                                    .Select(
                                        chunk =>
                                            CloneChunkWithDocumentName(
                                                chunk,
                                                documentName
                                            )
                                    )
                                    .ToList();

                            reusedDocumentCount++;
                        }
                    }
                }

                if (documentChunks == null)
                {
                    StatusTextBlock.Text =
                        $"Lecture du document {i + 1}/{pathsToProcess.Count} : " +
                        Path.GetFileName(pdfPath);

                    var pages =
                        await Task.Run(
                            () =>
                                _pdfService.ExtractPages(
                                    pdfPath,
                                    cancellationToken
                                ),
                            cancellationToken
                        );

                    cancellationToken.ThrowIfCancellationRequested();

                    StatusTextBlock.Text =
                        $"Découpage de {Path.GetFileName(pdfPath)}...";

                    documentChunks =
                        await Task.Run(
                            () =>
                                _chunkingService.CreateChunks(
                                    pages,
                                    documentName
                                ),
                            cancellationToken
                        );

                    cancellationToken.ThrowIfCancellationRequested();

                    chunksToEmbed.AddRange(documentChunks);
                }

                pendingDocuments.Add(
                    (
                        pdfPath,
                        documentName,
                        documentChunks,
                        workItem.IsReplacement,
                        info.Length,
                        info.LastWriteTimeUtc
                    )
                );
            }

            if (chunksToEmbed.Count > 0)
            {
                WorkProgressBar.Value = 0;

                var progress =
                    new Progress<int>(
                        percent =>
                        {
                            WorkProgressBar.Value = percent;
                            StatusTextBlock.Text =
                                $"Indexation des nouveaux contenus : {percent}% " +
                                $"({chunksToEmbed.Count} chunks à calculer)";
                        }
                    );

                await _searchService.IndexChunksAsync(
                    chunksToEmbed,
                    progress: progress,
                    cancellationToken: cancellationToken
                );
            }

            cancellationToken.ThrowIfCancellationRequested();

            foreach (var document in pendingDocuments)
            {
                if (document.IsReplacement)
                {
                    _chunks =
                        _chunks
                            .Where(
                                chunk =>
                                    !string.Equals(
                                        chunk.DocumentName,
                                        document.Name,
                                        StringComparison.OrdinalIgnoreCase
                                    )
                            )
                            .ToList();
                }

                if (
                    !_loadedPdfPaths.Contains(
                        document.Path,
                        StringComparer.OrdinalIgnoreCase
                    )
                )
                {
                    _loadedPdfPaths.Add(document.Path);
                }

                _documentPathsByName[document.Name] = document.Path;
                _loadedPdfStates[document.Path] =
                    (document.FileSize, document.LastWriteTimeUtc);
                _chunks.AddRange(document.Chunks);
            }

            var affectedFolders =
                pendingDocuments
                    .Select(document => Path.GetDirectoryName(document.Path))
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Select(path => path!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

            await TrySaveIndexesForFoldersAsync(affectedFolders);

            UpdateDocumentSummaries();
            SelectDocumentsByPath(requestedPaths);
            EnableQuestions();

            var statusParts =
                new List<string>();

            if (newCount > 0)
            {
                statusParts.Add(
                    newCount == 1
                        ? "1 PDF ajouté"
                        : $"{newCount} PDF ajoutés"
                );
            }

            if (changedCount > 0)
            {
                statusParts.Add(
                    changedCount == 1
                        ? "1 PDF modifié réindexé"
                        : $"{changedCount} PDF modifiés réindexés"
                );
            }

            if (unchangedLoadedPaths.Count > 0)
            {
                statusParts.Add(
                    unchangedLoadedPaths.Count == 1
                        ? "1 PDF inchangé ignoré"
                        : $"{unchangedLoadedPaths.Count} PDF inchangés ignorés"
                );
            }

            if (reusedDocumentCount > 0)
            {
                statusParts.Add(
                    reusedDocumentCount == 1
                        ? "1 index local réutilisé"
                        : $"{reusedDocumentCount} index locaux réutilisés"
                );
            }

            StatusTextBlock.Text =
                "Prêt — " + string.Join(" · ", statusParts) + ".";
        }
        catch (OperationCanceledException)
        {
            StatusTextBlock.Text =
                "Import annulé — aucun document en cours de traitement n'a été ajouté.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Erreur Héphaïstos",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );

            StatusTextBlock.Text = "Une erreur est survenue pendant l'ajout.";
        }
        finally
        {
            _importCancellation?.Dispose();
            _importCancellation = null;
            CancelButton.IsEnabled = false;
            SetBusy(false);
        }
    }

    private static (long FileSize, DateTime LastWriteTimeUtc) GetFileState(
        string pdfPath
    )
    {
        var info =
            new FileInfo(pdfPath);

        return (
            info.Length,
            info.LastWriteTimeUtc
        );
    }

    private string FindDocumentNameForPath(
        string pdfPath
    )
    {
        foreach (var pair in _documentPathsByName)
        {
            if (
                string.Equals(
                    pair.Value,
                    pdfPath,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return pair.Key;
            }
        }

        return "";
    }

    private async Task TrySaveIndexesForFoldersAsync(
        IEnumerable<string> folderPaths
    )
    {
        foreach (
            var folderPath in folderPaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
        )
        {
            try
            {
                var pdfPaths =
                    _loadedPdfPaths
                        .Where(
                            path =>
                                string.Equals(
                                    Path.GetDirectoryName(path),
                                    folderPath,
                                    StringComparison.OrdinalIgnoreCase
                                )
                        )
                        .OrderBy(path => path)
                        .ToList();

                if (pdfPaths.Count == 0)
                {
                    continue;
                }

                var chunksForFolder =
                    new List<DocumentChunk>();

                foreach (var pdfPath in pdfPaths)
                {
                    var documentName =
                        FindDocumentNameForPath(pdfPath);

                    if (string.IsNullOrWhiteSpace(documentName))
                    {
                        continue;
                    }

                    var storedDocumentName =
                        Path.GetFileName(pdfPath);

                    chunksForFolder.AddRange(
                        _chunks
                            .Where(
                                chunk =>
                                    string.Equals(
                                        chunk.DocumentName,
                                        documentName,
                                        StringComparison.OrdinalIgnoreCase
                                    )
                            )
                            .Select(
                                chunk =>
                                    CloneChunkWithDocumentName(
                                        chunk,
                                        storedDocumentName
                                    )
                            )
                    );
                }

                await _indexStorageService.SaveAsync(
                    folderPath,
                    pdfPaths,
                    chunksForFolder
                );
            }
            catch
            {
                // Le cache local accélère les prochains imports mais ne doit
                // jamais faire échouer l'ajout de documents en mémoire.
            }
        }
    }

    private static DocumentChunk CloneChunkWithDocumentName(
        DocumentChunk source,
        string documentName
    )
    {
        return new DocumentChunk
        {
            DocumentName = documentName,
            PageNumber = source.PageNumber,
            ChunkIndex = source.ChunkIndex,
            Text = source.Text,
            WasOcr = source.WasOcr,
            Embedding = source.Embedding
        };
    }

    private static string CreateUniqueDocumentName(
        string pdfPath,
        ISet<string> usedNames
    )
    {
        var fileName = Path.GetFileName(pdfPath);

        if (!usedNames.Contains(fileName))
        {
            return fileName;
        }

        var folderName =
            new DirectoryInfo(
                Path.GetDirectoryName(pdfPath) ?? "."
            ).Name;

        var candidate = $"{fileName} — {folderName}";
        var suffix = 2;

        while (usedNames.Contains(candidate))
        {
            candidate = $"{fileName} — {folderName} ({suffix})";
            suffix++;
        }

        return candidate;
    }

    private void UpdateCorpusLocationUi()
    {
        var folders =
            _loadedPdfPaths
                .Select(Path.GetDirectoryName)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => path!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        _currentFolderPath =
            folders.Count == 1
                ? folders[0]
                : "";

        if (_loadedPdfPaths.Count == 0)
        {
            FolderPathTextBox.Text = "Aucun document chargé";
            FolderPathTextBox.ToolTip = null;
        }
        else if (folders.Count == 1)
        {
            FolderPathTextBox.Text = folders[0];
            FolderPathTextBox.ToolTip = folders[0];
        }
        else
        {
            FolderPathTextBox.Text =
                $"Documents provenant de {folders.Count} dossiers";

            FolderPathTextBox.ToolTip =
                string.Join(Environment.NewLine, folders);
        }
    }


    // ======================================================
    // QUESTION
    // ======================================================

    private void QuestionTextBox_KeyDown(
        object sender,
        System.Windows.Input.KeyEventArgs e
    )
    {
        if (
            e.Key != System.Windows.Input.Key.Enter &&
            e.Key != System.Windows.Input.Key.Return
        )
        {
            return;
        }

        // Maj + Entrée : laisser le TextBox insérer un retour à la ligne.
        if (
            (System.Windows.Input.Keyboard.Modifiers &
             System.Windows.Input.ModifierKeys.Shift) != 0
        )
        {
            return;
        }

        // Entrée seule : envoyer sans insérer de retour à la ligne.
        e.Handled = true;

        if (AskButton.IsEnabled)
        {
            AskButton_Click(AskButton, e);
        }
    }

    private async void AskButton_Click(
        object sender,
        RoutedEventArgs e
    )
    {
        var question =
            QuestionTextBox.Text.Trim();

        if (
            string.IsNullOrWhiteSpace(
                question
            )
        )
        {
            return;
        }

        if (_chunks.Count == 0)
        {
            return;
        }

        var history =
            _chatMessages.ToList();

        AddChatMessage(
            "user",
            question
        );

        QuestionTextBox.Clear();
        ChatTab.IsSelected = true;

        ThinkingIndicator.Visibility =
            Visibility.Visible;

        ChatScrollViewer.ScrollToEnd();

        try
        {
            SetBusy(
                true,
                "Recherche des sources..."
            );

            SourcesListBox.ItemsSource =
                null;

            SourcePreviewTextBox.Clear();

            var results =
                await _searchService.SearchAsync(
                    _chunks,
                    question,
                    topK: 5
                );

            // Pour une question de suivi courte ou pronominale,
            // une seconde recherche enrichie par la question précédente
            // aide le RAG à retrouver le bon contexte documentaire.
            var previousUserQuestion =
                history
                    .LastOrDefault(
                        message => message.IsUser
                    )
                    ?.Text;

            if (
                !string.IsNullOrWhiteSpace(
                    previousUserQuestion
                )
            )
            {
                var contextualQuestion =
                    previousUserQuestion +
                    "\n" +
                    question;

                var contextualResults =
                    await _searchService.SearchAsync(
                        _chunks,
                        contextualQuestion,
                        topK: 5
                    );

                results =
                    MergeSearchResults(
                        results,
                        contextualResults,
                        topK: 5
                    );
            }

            var sourceItems =
                results
                    .Select(
                        result =>
                            new SourceItem
                            {
                                DocumentName =
                                    result.Chunk.DocumentName,

                                FilePath =
                                    ResolveDocumentPath(
                                        result.Chunk.DocumentName
                                    ),

                                PageNumber =
                                    result.Chunk.PageNumber,

                                ChunkIndex =
                                    result.Chunk.ChunkIndex,

                                Score =
                                    result.Score,

                                Text =
                                    result.Chunk.Text
                            }
                    )
                    .ToList();

            SourcesListBox.ItemsSource =
                sourceItems;

            StatusTextBlock.Text =
                "Rédaction de la réponse locale...";

            var answer =
                await _llmService.AnswerQuestionAsync(
                    question,
                    results,
                    history
                );

            AddChatMessage(
                "assistant",
                answer
            );

            StatusTextBlock.Text =
                "Prêt.";
        }
        catch (Exception ex)
        {
            AddChatMessage(
                "assistant",
                "Une erreur est survenue pendant cette réponse."
            );

            MessageBox.Show(
                ex.Message,
                "Erreur Hephaïstos",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );

            StatusTextBlock.Text =
                "Erreur lors de la question.";
        }
        finally
        {
            ThinkingIndicator.Visibility =
                Visibility.Collapsed;

            SetBusy(false);

            QuestionTextBox.Focus();
            System.Windows.Input.Keyboard.Focus(
                QuestionTextBox
            );
        }
    }

    private static List<(DocumentChunk Chunk, double Score)>
        MergeSearchResults(
            IReadOnlyList<(DocumentChunk Chunk, double Score)> first,
            IReadOnlyList<(DocumentChunk Chunk, double Score)> second,
            int topK
        )
    {
        return first
            .Concat(second)
            .GroupBy(
                result =>
                    (
                        result.Chunk.DocumentName,
                        result.Chunk.PageNumber,
                        result.Chunk.ChunkIndex
                    )
            )
            .Select(
                group =>
                    group
                        .OrderByDescending(
                            result => result.Score
                        )
                        .First()
            )
            .OrderByDescending(
                result => result.Score
            )
            .Take(topK)
            .ToList();
    }

    private void AddChatMessage(
        string role,
        string text
    )
    {
        _chatMessages.Add(
            new ChatMessage
            {
                Role = role,
                Text = text
            }
        );

        Dispatcher.BeginInvoke(
            new Action(
                () =>
                    ChatScrollViewer.ScrollToEnd()
            )
        );
    }

private void ChatMessageTextBlock_Loaded(
    object sender,
    RoutedEventArgs e
)
{
    if (
        sender is not System.Windows.Controls.TextBlock textBlock ||
        textBlock.DataContext is not ChatMessage message
    )
    {
        return;
    }

    RenderChatMarkdown(
        textBlock,
        message.Text
    );
}

private static void RenderChatMarkdown(
    System.Windows.Controls.TextBlock textBlock,
    string text
)
{
    textBlock.Inlines.Clear();

    var lines =
        (text ?? "")
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n');

    for (int i = 0; i < lines.Length; i++)
    {
        var line = lines[i];
        var fontSize = 14.0;
        var fontWeight = FontWeights.Normal;

        if (line.StartsWith("### ", StringComparison.Ordinal))
        {
            line = line[4..];
            fontSize = 14.5;
            fontWeight = FontWeights.SemiBold;
        }
        else if (line.StartsWith("## ", StringComparison.Ordinal))
        {
            line = line[3..];
            fontSize = 15.5;
            fontWeight = FontWeights.SemiBold;
        }
        else if (line.StartsWith("# ", StringComparison.Ordinal))
        {
            line = line[2..];
            fontSize = 16.5;
            fontWeight = FontWeights.SemiBold;
        }
        else if (
            line.StartsWith("- ", StringComparison.Ordinal) ||
            line.StartsWith("* ", StringComparison.Ordinal)
        )
        {
            line = "• " + line[2..];
        }

        AppendInlineMarkdown(
            textBlock,
            line,
            fontSize,
            fontWeight
        );

        if (i < lines.Length - 1)
        {
            textBlock.Inlines.Add(
                new System.Windows.Documents.LineBreak()
            );
        }
    }
}

private static void AppendInlineMarkdown(
    System.Windows.Controls.TextBlock textBlock,
    string line,
    double fontSize,
    FontWeight defaultFontWeight
)
{
    var index = 0;

    while (index < line.Length)
    {
        var boldStart = line.IndexOf("**", index, StringComparison.Ordinal);
        var codeStart = line.IndexOf('`', index);

        var nextStart = -1;
        var kind = "";

        if (boldStart >= 0 && (codeStart < 0 || boldStart < codeStart))
        {
            nextStart = boldStart;
            kind = "bold";
        }
        else if (codeStart >= 0)
        {
            nextStart = codeStart;
            kind = "code";
        }

        if (nextStart < 0)
        {
            AddChatRun(
                textBlock,
                line[index..],
                fontSize,
                defaultFontWeight,
                false
            );
            break;
        }

        if (nextStart > index)
        {
            AddChatRun(
                textBlock,
                line[index..nextStart],
                fontSize,
                defaultFontWeight,
                false
            );
        }

        if (kind == "bold")
        {
            var end =
                line.IndexOf(
                    "**",
                    nextStart + 2,
                    StringComparison.Ordinal
                );

            if (end < 0)
            {
                AddChatRun(
                    textBlock,
                    line[nextStart..],
                    fontSize,
                    defaultFontWeight,
                    false
                );
                break;
            }

            AddChatRun(
                textBlock,
                line[(nextStart + 2)..end],
                fontSize,
                FontWeights.SemiBold,
                false
            );

            index = end + 2;
            continue;
        }

        var codeEnd =
            line.IndexOf('`', nextStart + 1);

        if (codeEnd < 0)
        {
            AddChatRun(
                textBlock,
                line[nextStart..],
                fontSize,
                defaultFontWeight,
                false
            );
            break;
        }

        AddChatRun(
            textBlock,
            line[(nextStart + 1)..codeEnd],
            fontSize,
            defaultFontWeight,
            true
        );

        index = codeEnd + 1;
    }
}

private static void AddChatRun(
    System.Windows.Controls.TextBlock textBlock,
    string text,
    double fontSize,
    FontWeight fontWeight,
    bool isCode
)
{
    if (text.Length == 0)
    {
        return;
    }

    var run =
        new System.Windows.Documents.Run(text)
        {
            FontSize = fontSize,
            FontWeight = fontWeight
        };

    if (isCode)
    {
        run.FontFamily =
            new System.Windows.Media.FontFamily("Consolas");
        run.Background =
            new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(
                    239,
                    242,
                    240
                )
            );
    }

    textBlock.Inlines.Add(run);
}

private void OutputTextBox_PreviewMouseLeftButtonDown(
    object sender,
    System.Windows.Input.MouseButtonEventArgs e
)
{
    if (
        IsInsideScrollBar(
            e.OriginalSource as DependencyObject
        )
    )
    {
        return;
    }

    if (sender is System.Windows.Controls.TextBox textBox)
    {
        textBox.SelectionLength = 0;
    }

    System.Windows.Input.Keyboard.ClearFocus();
    e.Handled = true;
}

private static bool IsInsideScrollBar(
    DependencyObject? source
)
{
    while (source != null)
    {
        if (source is System.Windows.Controls.Primitives.ScrollBar)
        {
            return true;
        }

        source =
            System.Windows.Media.VisualTreeHelper.GetParent(source);
    }

    return false;
}

private List<DocumentSummary> GetSelectedDocuments()
{
    return
        DocumentsListBox.SelectedItems
            .OfType<DocumentSummary>()
            .ToList();
}

private List<DocumentChunk> GetChunksForDocuments(
    IReadOnlyCollection<DocumentSummary> documents
)
{
    var documentNames =
        documents
            .Select(
                document => document.DocumentName
            )
            .ToHashSet(
                StringComparer.OrdinalIgnoreCase
            );

    return
        _chunks
            .Where(
                chunk =>
                    documentNames.Contains(
                        chunk.DocumentName
                    )
            )
            .OrderBy(
                chunk => chunk.DocumentName
            )
            .ThenBy(
                chunk => chunk.PageNumber
            )
            .ThenBy(
                chunk => chunk.ChunkIndex
            )
            .ToList();
}

private void UpdateDocumentSelectionUi()
{
    var selectedDocuments =
        GetSelectedDocuments();

    var selectedCount =
        selectedDocuments.Count;

    var totalCount =
        DocumentsListBox.Items.Count;

    if (_chunks.Count == 0)
    {
        DocumentsSelectionTextBlock.Text =
            "Aucun corpus chargé.";
    }
    else if (selectedCount == 0)
    {
        DocumentsSelectionTextBlock.Text =
            "Aucun document sélectionné — le chat interroge tout le corpus.";
    }
    else if (selectedCount == 1)
    {
        DocumentsSelectionTextBlock.Text =
            $"1 document sélectionné sur {totalCount}.";
    }
    else
    {
        DocumentsSelectionTextBlock.Text =
            $"{selectedCount} documents sélectionnés sur {totalCount}.";
    }

    SummaryButton.Content =
        selectedCount > 1
            ? "Synthétiser la sélection"
            : "Synthétiser le PDF";

    SummaryButton.IsEnabled =
        !_isBusy &&
        _chunks.Count > 0 &&
        selectedCount >= 1;

    TimelineButton.IsEnabled =
        !_isBusy &&
        _chunks.Count > 0 &&
        selectedCount == 1;

    ExtractionButton.IsEnabled =
        !_isBusy &&
        _chunks.Count > 0 &&
        selectedCount == 1;

    CompareButton.IsEnabled =
        !_isBusy &&
        _chunks.Count > 0 &&
        selectedCount == 2;

    ContradictionButton.IsEnabled =
        !_isBusy &&
        _chunks.Count > 0 &&
        selectedCount >= 2;

    SelectAllDocumentsButton.IsEnabled =
        !_isBusy &&
        totalCount > 0 &&
        selectedCount < totalCount;

    ClearDocumentSelectionButton.IsEnabled =
        !_isBusy &&
        selectedCount > 0;

    RemoveDocumentsButton.IsEnabled =
        !_isBusy &&
        selectedCount > 0;
}

private void DocumentsListBox_SelectionChanged(
    object sender,
    System.Windows.Controls.SelectionChangedEventArgs e
)
{
    UpdateDocumentSelectionUi();
}

private void SelectAllDocumentsButton_Click(
    object sender,
    RoutedEventArgs e
)
{
    if (_isBusy)
    {
        return;
    }

    DocumentsListBox.SelectAll();
}

private void ClearDocumentSelectionButton_Click(
    object sender,
    RoutedEventArgs e
)
{
    if (_isBusy)
    {
        return;
    }

    DocumentsListBox.UnselectAll();
}

private void SelectDocumentsByPath(
    IReadOnlyCollection<string> pdfPaths
)
{
    if (pdfPaths.Count == 0)
    {
        return;
    }

    var paths =
        pdfPaths
            .Select(Path.GetFullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    DocumentsListBox.UnselectAll();

    foreach (var item in DocumentsListBox.Items)
    {
        if (
            item is DocumentSummary document &&
            !string.IsNullOrWhiteSpace(document.FilePath) &&
            paths.Contains(Path.GetFullPath(document.FilePath))
        )
        {
            DocumentsListBox.SelectedItems.Add(item);
        }
    }

    if (DocumentsListBox.SelectedItem != null)
    {
        DocumentsListBox.ScrollIntoView(
            DocumentsListBox.SelectedItem
        );
    }
}

private string ResolveDocumentPath(
    string documentName
)
{
    return
        _documentPathsByName.TryGetValue(
            documentName,
            out var path
        )
            ? path
            : "";
}


private void DocumentsListBox_MouseDoubleClick(
    object sender,
    System.Windows.Input.MouseButtonEventArgs e
)
{
    if (
        DocumentsListBox.SelectedItem
        is not DocumentSummary document ||
        string.IsNullOrWhiteSpace(document.FilePath)
    )
    {
        return;
    }

    var filePath = document.FilePath;

    if (!File.Exists(filePath))
    {
        MessageBox.Show(
            $"Le fichier est introuvable :\n{filePath}",
            "Héphaïstos",
            MessageBoxButton.OK,
            MessageBoxImage.Warning
        );

        return;
    }

    try
    {
        Process.Start(
            new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            }
        );
    }
    catch (Exception ex)
    {
        MessageBox.Show(
            $"Impossible d'ouvrir le document.\n\n{ex.Message}",
            "Héphaïstos",
            MessageBoxButton.OK,
            MessageBoxImage.Error
        );
    }
}


private static bool TryResolveDocumentDrop(
    IDataObject data,
    out List<string> pdfPaths,
    out string? errorMessage
)
{
    pdfPaths = [];
    errorMessage = null;

    if (
        !data.GetDataPresent(DataFormats.FileDrop) ||
        data.GetData(DataFormats.FileDrop) is not string[] paths ||
        paths.Length == 0
    )
    {
        return false;
    }

    foreach (var path in paths)
    {
        if (Directory.Exists(path))
        {
            pdfPaths.AddRange(
                Directory.GetFiles(
                    path,
                    "*.pdf",
                    SearchOption.TopDirectoryOnly
                )
            );

            continue;
        }

        if (
            File.Exists(path) &&
            string.Equals(
                Path.GetExtension(path),
                ".pdf",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            pdfPaths.Add(path);
            continue;
        }

        errorMessage =
            "Héphaïstos accepte ici uniquement des fichiers PDF ou des dossiers.";
        return false;
    }

    pdfPaths =
        pdfPaths
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    if (pdfPaths.Count == 0)
    {
        errorMessage =
            "Aucun fichier PDF n'a été trouvé dans ce dépôt.";
        return false;
    }

    return true;
}


private void DocumentDropZone_DragOver(
    object sender,
    DragEventArgs e
)
{
    e.Effects =
        !_isBusy &&
        TryResolveDocumentDrop(
            e.Data,
            out _,
            out _
        )
            ? DragDropEffects.Copy
            : DragDropEffects.None;

    e.Handled = true;
}


private async void DocumentDropZone_Drop(
    object sender,
    DragEventArgs e
)
{
    if (_isBusy)
    {
        return;
    }

    if (
        !TryResolveDocumentDrop(
            e.Data,
            out var pdfPaths,
            out var errorMessage
        )
    )
    {
        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            MessageBox.Show(
                errorMessage,
                "Héphaïstos",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
        }

        return;
    }

    await AddPdfFilesAsync(pdfPaths);
}



private void SourcesListBox_SelectionChanged(
    object sender,
    System.Windows.Controls.SelectionChangedEventArgs e
)
{
    if (
        SourcesListBox.SelectedItem
        is not SourceItem source
    )
    {
        SourcePreviewTextBox.Clear();
        return;
    }

    SourcePreviewTextBox.Text =
    $"Document : {source.DocumentName}\n" +
    $"Page : {source.PageNumber}\n\n" +
    source.Text;
}

    private void SourcesListBox_MouseDoubleClick(
    object sender,
    System.Windows.Input.MouseButtonEventArgs e
)
{
    if (
        SourcesListBox.SelectedItem
        is not SourceItem source
    )
    {
        return;
    }

    if (!File.Exists(source.FilePath))
    {
        MessageBox.Show(
            $"Le fichier source est introuvable :\n{source.FilePath}",
            "Hephaïstos",
            MessageBoxButton.OK,
            MessageBoxImage.Warning
        );

        return;
    }

    try
    {
        Process.Start(
            new ProcessStartInfo
            {
                FileName =
                    source.FilePath,

                UseShellExecute =
                    true
            }
        );
    }
    catch (Exception ex)
    {
        MessageBox.Show(
            $"Impossible d'ouvrir le document.\n\n{ex.Message}",
            "Hephaïstos",
            MessageBoxButton.OK,
            MessageBoxImage.Error
        );
    }
}
private async void FolderSummaryButton_Click(
    object sender,
    RoutedEventArgs e
)
{
    AnalysisTab.IsSelected = true;
    SummaryAnalysisTab.IsSelected = true;
    if (_chunks.Count == 0)
    {
        MessageBox.Show(
            "Ajoute d'abord un ou plusieurs documents.",
            "Hephaïstos",
            MessageBoxButton.OK,
            MessageBoxImage.Information
        );

        return;
    }

    _summaryCancellation?.Dispose();

    _summaryCancellation =
        new CancellationTokenSource();

    try
    {
        SetBusy(
            true,
            "Préparation de la synthèse du corpus..."
        );

        CancelButton.IsEnabled =
            true;

        SourcesListBox.ItemsSource =
            null;

        SourcePreviewTextBox.Clear();

        WorkProgressBar.Value =
            0;

        var progress =
            new Progress<int>(
                percent =>
                {
                    WorkProgressBar.Value =
                        percent;

                    StatusTextBlock.Text =
                        $"Synthèse du corpus : {percent}%";
                }
            );

        var summary =
            await _summaryService
                .SummarizeFolderAsync(
                    _chunks,
                    progress,
                    _summaryCancellation.Token
                );

        AddAnalysisHistory(
            _summaryHistory,
            SummaryHistoryComboBox,
            SummaryTextBox,
            "Corpus complet",
            summary
        );

        StatusTextBlock.Text =
            "Synthèse du corpus terminée.";
    }
    catch (OperationCanceledException)
    {
        StatusTextBlock.Text =
            "Synthèse du corpus annulée.";

    }
    catch (Exception ex)
    {
        MessageBox.Show(
            ex.Message,
            "Erreur Hephaïstos",
            MessageBoxButton.OK,
            MessageBoxImage.Error
        );

        StatusTextBlock.Text =
            "Erreur pendant la synthèse du corpus.";
    }
    finally
    {
        _summaryCancellation?.Dispose();

        _summaryCancellation =
            null;

        CancelButton.IsEnabled =
            false;

        SetBusy(false);
    }
}

private async void SummaryButton_Click(
    object sender,
    RoutedEventArgs e
)
{
    AnalysisTab.IsSelected = true;
    SummaryAnalysisTab.IsSelected = true;

    var selectedDocuments =
        GetSelectedDocuments();

    if (selectedDocuments.Count == 0)
    {
        MessageBox.Show(
            "Sélectionne d'abord un ou plusieurs PDF dans l'espace Documents.",
            "Héphaïstos",
            MessageBoxButton.OK,
            MessageBoxImage.Information
        );

        return;
    }

    var documentChunks =
        GetChunksForDocuments(
            selectedDocuments
        );

    if (documentChunks.Count == 0)
    {
        MessageBox.Show(
            "Aucun contenu exploitable n'a été trouvé pour la sélection.",
            "Héphaïstos",
            MessageBoxButton.OK,
            MessageBoxImage.Warning
        );

        return;
    }

    var scopeLabel =
        selectedDocuments.Count == 1
            ? selectedDocuments[0].DocumentName
            : $"{selectedDocuments.Count} documents sélectionnés";

    _summaryCancellation?.Dispose();

    _summaryCancellation =
        new CancellationTokenSource();

    try
    {
        SetBusy(
            true,
            $"Préparation de la synthèse — {scopeLabel}..."
        );

        CancelButton.IsEnabled =
            true;

        SourcesListBox.ItemsSource =
            null;

        SourcePreviewTextBox.Clear();

        WorkProgressBar.Value =
            0;

        var progress =
            new Progress<int>(
                percent =>
                {
                    WorkProgressBar.Value =
                        percent;

                    StatusTextBlock.Text =
                        $"Synthèse — {scopeLabel} : {percent}%";
                }
            );

        var summary =
            await _summaryService
                .SummarizeFolderAsync(
                    documentChunks,
                    progress,
                    _summaryCancellation.Token
                );

        AddAnalysisHistory(
            _summaryHistory,
            SummaryHistoryComboBox,
            SummaryTextBox,
            scopeLabel,
            summary
        );

        StatusTextBlock.Text =
            $"Synthèse terminée — {scopeLabel}.";
    }
    catch (OperationCanceledException)
    {
        StatusTextBlock.Text =
            "Synthèse annulée.";
    }
    catch (Exception ex)
    {
        MessageBox.Show(
            ex.Message,
            "Erreur Héphaïstos",
            MessageBoxButton.OK,
            MessageBoxImage.Error
        );

        StatusTextBlock.Text =
            "Erreur pendant la synthèse.";
    }
    finally
    {
        _summaryCancellation?.Dispose();

        _summaryCancellation =
            null;

        CancelButton.IsEnabled =
            false;

        SetBusy(false);
    }
}
private async void TimelineButton_Click(
    object sender,
    RoutedEventArgs e
)
{
    AnalysisTab.IsSelected = true;
    TimelineAnalysisTab.IsSelected = true;

    var selectedDocuments =
        GetSelectedDocuments();

    if (selectedDocuments.Count != 1)
    {
        MessageBox.Show(
            "Sélectionne un seul PDF pour créer une chronologie.",
            "Héphaïstos",
            MessageBoxButton.OK,
            MessageBoxImage.Information
        );

        return;
    }

    var selectedDocument =
        selectedDocuments[0];

    var documentChunks =
        GetChunksForDocuments(
            selectedDocuments
        );

    if (documentChunks.Count == 0)
    {
        MessageBox.Show(
            "Aucun contenu exploitable n'a été trouvé pour ce document.",
            "Héphaïstos",
            MessageBoxButton.OK,
            MessageBoxImage.Warning
        );

        return;
    }

    _timelineCancellation?.Dispose();

    _timelineCancellation =
        new CancellationTokenSource();

    try
    {
        SetBusy(
            true,
            $"Création de la chronologie de {selectedDocument.DocumentName}..."
        );

        CancelButton.IsEnabled =
            true;

        SourcesListBox.ItemsSource =
            null;

        SourcePreviewTextBox.Clear();

        WorkProgressBar.Value =
            0;

        var progress =
            new Progress<int>(
                percent =>
                {
                    WorkProgressBar.Value =
                        percent;

                    StatusTextBlock.Text =
                        $"Chronologie de {selectedDocument.DocumentName} : {percent}%";
                }
            );

        var timeline =
            await _timelineService
                .BuildTimelineAsync(
                    documentChunks,
                    progress,
                    _timelineCancellation.Token
                );

        AddAnalysisHistory(
            _timelineHistory,
            TimelineHistoryComboBox,
            TimelineTextBox,
            selectedDocument.DocumentName,
            timeline
        );

        StatusTextBlock.Text =
            $"Chronologie terminée — {selectedDocument.DocumentName}";
    }
    catch (OperationCanceledException)
    {
        StatusTextBlock.Text =
            "Chronologie annulée.";
    }
    catch (Exception ex)
    {
        MessageBox.Show(
            ex.Message,
            "Erreur Héphaïstos",
            MessageBoxButton.OK,
            MessageBoxImage.Error
        );

        StatusTextBlock.Text =
            "Erreur pendant la création de la chronologie.";
    }
    finally
    {
        _timelineCancellation?.Dispose();

        _timelineCancellation =
            null;

        CancelButton.IsEnabled =
            false;

        SetBusy(false);
    }
}
private async void ExtractionButton_Click(
    object sender,
    RoutedEventArgs e
)
{
    AnalysisTab.IsSelected = true;
    ExtractionAnalysisTab.IsSelected = true;

    var selectedDocuments =
        GetSelectedDocuments();

    if (selectedDocuments.Count != 1)
    {
        MessageBox.Show(
            "Sélectionne un seul PDF pour créer une fiche structurée.",
            "Héphaïstos",
            MessageBoxButton.OK,
            MessageBoxImage.Information
        );

        return;
    }

    var selectedDocument =
        selectedDocuments[0];

    var documentChunks =
        GetChunksForDocuments(
            selectedDocuments
        );

    if (documentChunks.Count == 0)
    {
        MessageBox.Show(
            "Aucun contenu exploitable n'a été trouvé pour ce document.",
            "Héphaïstos",
            MessageBoxButton.OK,
            MessageBoxImage.Warning
        );

        return;
    }

    _extractionCancellation?.Dispose();

    _extractionCancellation =
        new CancellationTokenSource();

    try
    {
        SetBusy(
            true,
            $"Fiche structurée de {selectedDocument.DocumentName}..."
        );

        CancelButton.IsEnabled =
            true;

        SourcesListBox.ItemsSource =
            null;

        SourcePreviewTextBox.Clear();

        WorkProgressBar.Value =
            0;

        var progress =
            new Progress<int>(
                percent =>
                {
                    WorkProgressBar.Value =
                        percent;

                    StatusTextBlock.Text =
                        $"Fiche structurée de {selectedDocument.DocumentName} : {percent}%";
                }
            );

        var extraction =
            await _structuredExtractionService
                .ExtractAsync(
                    documentChunks,
                    progress,
                    _extractionCancellation.Token
                );

        AddAnalysisHistory(
            _extractionHistory,
            ExtractionHistoryComboBox,
            ExtractionTextBox,
            selectedDocument.DocumentName,
            extraction
        );

        StatusTextBlock.Text =
            $"Fiche structurée terminée — {selectedDocument.DocumentName}";
    }
    catch (OperationCanceledException)
    {
        StatusTextBlock.Text =
            "Fiche structurée annulée.";
    }
    catch (Exception ex)
    {
        MessageBox.Show(
            ex.Message,
            "Erreur Héphaïstos",
            MessageBoxButton.OK,
            MessageBoxImage.Error
        );

        StatusTextBlock.Text =
            "Erreur pendant la création de la fiche structurée.";
    }
    finally
    {
        _extractionCancellation?.Dispose();

        _extractionCancellation =
            null;

        CancelButton.IsEnabled =
            false;

        SetBusy(false);
    }
}
private async void ContradictionButton_Click(
    object sender,
    RoutedEventArgs e
)
{
    AnalysisTab.IsSelected = true;
    ContradictionAnalysisTab.IsSelected = true;

    var selectedDocuments =
        GetSelectedDocuments();

    if (selectedDocuments.Count < 2)
    {
        MessageBox.Show(
            "Sélectionne au moins deux PDF pour rechercher des contradictions.",
            "Héphaïstos",
            MessageBoxButton.OK,
            MessageBoxImage.Information
        );

        return;
    }

    var selectedChunks =
        GetChunksForDocuments(
            selectedDocuments
        );

    if (selectedChunks.Count == 0)
    {
        MessageBox.Show(
            "Aucun contenu exploitable n'a été trouvé pour la sélection.",
            "Héphaïstos",
            MessageBoxButton.OK,
            MessageBoxImage.Warning
        );

        return;
    }

    _contradictionCancellation?.Dispose();

    _contradictionCancellation =
        new CancellationTokenSource();

    try
    {
        SetBusy(
            true,
            $"Recherche de contradictions dans {selectedDocuments.Count} documents..."
        );

        CancelButton.IsEnabled =
            true;

        SourcesListBox.ItemsSource =
            null;

        SourcePreviewTextBox.Clear();

        WorkProgressBar.Value =
            0;

        var progress =
            new Progress<int>(
                percent =>
                {
                    WorkProgressBar.Value =
                        percent;

                    StatusTextBlock.Text =
                        $"Contradictions : {percent}%";
                }
            );

        var result =
            await _contradictionService.DetectAsync(
                selectedChunks,
                progress,
                _contradictionCancellation.Token
            );

        var contradictionScope =
            string.Join(
                ", ",
                selectedDocuments.Select(document => document.DocumentName)
            );

        AddAnalysisHistory(
            _contradictionHistory,
            ContradictionHistoryComboBox,
            ContradictionTextBox,
            contradictionScope,
            result
        );

        StatusTextBlock.Text =
            $"Détection de contradictions terminée — {selectedDocuments.Count} documents.";
    }
    catch (OperationCanceledException)
    {
        StatusTextBlock.Text =
            "Détection de contradictions annulée.";
    }
    catch (Exception ex)
    {
        MessageBox.Show(
            ex.Message,
            "Erreur Héphaïstos",
            MessageBoxButton.OK,
            MessageBoxImage.Error
        );

        StatusTextBlock.Text =
            "Erreur pendant la détection de contradictions.";
    }
    finally
    {
        _contradictionCancellation?.Dispose();

        _contradictionCancellation =
            null;

        CancelButton.IsEnabled =
            false;

        SetBusy(false);
    }
}

private async void CompareButton_Click(
    object sender,
    RoutedEventArgs e
)
{
    AnalysisTab.IsSelected = true;
    ComparisonAnalysisTab.IsSelected = true;

    var selectedDocuments =
        GetSelectedDocuments();

    if (selectedDocuments.Count != 2)
    {
        MessageBox.Show(
            "Sélectionne exactement deux PDF dans l'espace Documents pour les comparer.",
            "Héphaïstos",
            MessageBoxButton.OK,
            MessageBoxImage.Information
        );

        return;
    }

    var documentA =
        selectedDocuments[0];

    var documentB =
        selectedDocuments[1];

    var chunksA =
        GetChunksForDocuments(
            [documentA]
        );

    var chunksB =
        GetChunksForDocuments(
            [documentB]
        );

    if (
        chunksA.Count == 0 ||
        chunksB.Count == 0
    )
    {
        MessageBox.Show(
            "L'un des deux documents ne contient aucun contenu exploitable.",
            "Héphaïstos",
            MessageBoxButton.OK,
            MessageBoxImage.Warning
        );

        return;
    }

    _comparisonCancellation?.Dispose();

    _comparisonCancellation =
        new CancellationTokenSource();

    try
    {
        SetBusy(
            true,
            $"Comparaison de {documentA.DocumentName} et {documentB.DocumentName}..."
        );

        CancelButton.IsEnabled =
            true;

        SourcesListBox.ItemsSource =
            null;

        SourcePreviewTextBox.Clear();

        WorkProgressBar.Value =
            0;

        var progress =
            new Progress<int>(
                percent =>
                {
                    WorkProgressBar.Value =
                        percent;

                    StatusTextBlock.Text =
                        $"Comparaison : {percent}%";
                }
            );

        var comparison =
            await _comparisonService.CompareAsync(
                chunksA,
                chunksB,
                progress,
                _comparisonCancellation.Token
            );

        var comparisonLength =
            comparison?.Length ?? 0;

        var comparisonText =
            string.IsNullOrWhiteSpace(comparison)
                ? "DIAGNOSTIC : le service de comparaison n'a renvoyé aucun texte."
                : comparison;

        AddAnalysisHistory(
            _comparisonHistory,
            ComparisonHistoryComboBox,
            ComparisonTextBox,
            $"{documentA.DocumentName} ↔ {documentB.DocumentName}",
            comparisonText
        );

        StatusTextBlock.Text =
            $"Comparaison terminée — " +
            $"{documentA.DocumentName} / {documentB.DocumentName} — " +
            $"{comparisonLength} caractères";
    }
    catch (OperationCanceledException)
    {
        StatusTextBlock.Text =
            "Comparaison annulée.";
    }
    catch (Exception ex)
    {
        MessageBox.Show(
            ex.Message,
            "Erreur Héphaïstos",
            MessageBoxButton.OK,
            MessageBoxImage.Error
        );

        StatusTextBlock.Text =
            "Erreur pendant la comparaison.";
    }
    finally
    {
        _comparisonCancellation?.Dispose();

        _comparisonCancellation =
            null;

        CancelButton.IsEnabled =
            false;

        SetBusy(false);
    }
}


private void CancelButton_Click(
    object sender,
    RoutedEventArgs e
)
{
    if (
        _importCancellation == null &&
        _summaryCancellation == null &&
        _timelineCancellation == null &&
        _extractionCancellation == null &&
        _comparisonCancellation == null &&
        _contradictionCancellation == null
    )
    {
        return;
    }

    StatusTextBlock.Text =
        "Annulation en cours...";

    CancelButton.IsEnabled =
        false;

    _importCancellation?.Cancel();

    _summaryCancellation?.Cancel();

    _timelineCancellation?.Cancel();

    _extractionCancellation?.Cancel();

    _comparisonCancellation?.Cancel();

    _contradictionCancellation?.Cancel();
}

private async void ValidationButton_Click(
    object sender,
    RoutedEventArgs e
)
{
    AnalysisTab.IsSelected = true;
    DiagnosticAnalysisTab.IsSelected = true;
    if (_chunks.Count == 0)
    {
        return;
    }

    if (string.IsNullOrWhiteSpace(_currentFolderPath))
    {
        return;
    }

    try
    {
        SetBusy(
            true,
            "Validation de la recherche..."
        );

        SourcesListBox.ItemsSource =
            null;

        SourcePreviewTextBox.Clear();

        var validationCases =
            await _validationService
                .LoadValidationCasesAsync(
                    _currentFolderPath
                );

        if (validationCases.Count == 0)
        {
            MessageBox.Show(
                "Le fichier de validation ne contient aucun test.",
                "Hephaïstos",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );

            return;
        }

        var results =
            await _validationService.RunAsync(
                _chunks,
                validationCases,
                topK: 5
            );

        var total =
            results.Count;

        var top1 =
            results.Count(
                result =>
                    result.Rank == 1
            );

        var top3 =
            results.Count(
                result =>
                    result.Rank.HasValue &&
                    result.Rank.Value <= 3
            );

        var top5 =
            results.Count(
                result =>
                    result.Success
            );

        var report =
            new System.Text.StringBuilder();

        report.AppendLine(
            "VALIDATION DE LA RECHERCHE"
        );

        report.AppendLine(
            "============================"
        );

        report.AppendLine();

        report.AppendLine(
            $"Tests : {total}"
        );

        report.AppendLine(
            $"Bonne page en position 1 : {top1}/{total}"
        );

        report.AppendLine(
            $"Bonne page dans le Top 3 : {top3}/{total}"
        );

        report.AppendLine(
            $"Bonne page dans le Top 5 : {top5}/{total}"
        );

        report.AppendLine();

        foreach (var result in results)
        {
            if (result.Success)
            {
                report.AppendLine(
                    $"✓ {result.Question}"
                );

                report.AppendLine(
                    $"  Attendu : " +
                    $"{result.ExpectedDocument}, " +
                    $"p. {result.ExpectedPage}"
                );

                report.AppendLine(
                    $"  Trouvé en position {result.Rank}"
                );
            }
            else
            {
                report.AppendLine(
                    $"✗ {result.Question}"
                );

                report.AppendLine(
                    $"  Attendu : " +
                    $"{result.ExpectedDocument}, " +
                    $"p. {result.ExpectedPage}"
                );

                report.AppendLine(
                    "  Résultats obtenus :"
                );

                foreach (
                    var source in result.RetrievedSources
                )
                {
                    report.AppendLine(
                        $"  - {source}"
                    );
                }
            }

            report.AppendLine();
        }

        AddAnalysisHistory(
            _diagnosticHistory,
            DiagnosticHistoryComboBox,
            DiagnosticTextBox,
            "Test recherche",
            report.ToString()
        );

        StatusTextBlock.Text =
            $"Validation terminée : {top5}/{total} réussis.";
    }
    catch (FileNotFoundException)
    {
        MessageBox.Show(
            "Le fichier hephaistos.validation.json " +
            "n'a pas été trouvé dans le dossier sélectionné.",
            "Hephaïstos",
            MessageBoxButton.OK,
            MessageBoxImage.Warning
        );

        StatusTextBlock.Text =
            "Fichier de validation introuvable.";
    }
    catch (Exception ex)
    {
        MessageBox.Show(
            ex.Message,
            "Erreur Hephaïstos",
            MessageBoxButton.OK,
            MessageBoxImage.Error
        );

        StatusTextBlock.Text =
            "Erreur pendant la validation.";
    }
    finally
    {
        SetBusy(false);
    }
}
private void RemoveDocumentsButton_Click(
    object sender,
    RoutedEventArgs e
)
{
    if (_isBusy)
    {
        return;
    }

    var selectedDocuments = GetSelectedDocuments();

    if (selectedDocuments.Count == 0)
    {
        return;
    }

    var names =
        selectedDocuments
            .Select(document => document.DocumentName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    var paths =
        selectedDocuments
            .Select(document => document.FilePath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    _chunks =
        _chunks
            .Where(chunk => !names.Contains(chunk.DocumentName))
            .ToList();

    _loadedPdfPaths.RemoveAll(
        path => paths.Contains(path)
    );

    foreach (var path in paths)
    {
        _loadedPdfStates.Remove(path);
    }

    foreach (var name in names)
    {
        _documentPathsByName.Remove(name);
    }

    _chatMessages.Clear();
    SourcesListBox.ItemsSource = null;
    SourcePreviewTextBox.Clear();
    QuestionTextBox.Clear();

    UpdateDocumentSummaries();
    SetBusy(false);

    StatusTextBlock.Text =
        selectedDocuments.Count == 1
            ? "Document retiré de l'espace. Le fichier original n'a pas été supprimé."
            : $"{selectedDocuments.Count} documents retirés de l'espace. Les fichiers originaux n'ont pas été supprimés.";
}


    // ======================================================
    // UI
    // ======================================================

private void AddAnalysisHistory(
    ObservableCollection<AnalysisHistoryItem> history,
    ComboBox historyComboBox,
    TextBox outputTextBox,
    string title,
    string content
)
{
    var item =
        new AnalysisHistoryItem
        {
            Title = title,
            Content = content,
            CreatedAt = DateTime.Now
        };

    history.Add(item);
    historyComboBox.SelectedItem = item;
    ShowSelectedAnalysis(historyComboBox, outputTextBox);
}

private static void ShowSelectedAnalysis(
    ComboBox historyComboBox,
    TextBox outputTextBox
)
{
    if (historyComboBox.SelectedItem is not AnalysisHistoryItem item)
    {
        return;
    }

    outputTextBox.Text = item.Content;
    outputTextBox.CaretIndex = 0;
    outputTextBox.ScrollToHome();
}

private void SummaryHistoryComboBox_SelectionChanged(
    object sender,
    SelectionChangedEventArgs e
) => ShowSelectedAnalysis(SummaryHistoryComboBox, SummaryTextBox);

private void TimelineHistoryComboBox_SelectionChanged(
    object sender,
    SelectionChangedEventArgs e
) => ShowSelectedAnalysis(TimelineHistoryComboBox, TimelineTextBox);

private void ExtractionHistoryComboBox_SelectionChanged(
    object sender,
    SelectionChangedEventArgs e
) => ShowSelectedAnalysis(ExtractionHistoryComboBox, ExtractionTextBox);

private void ComparisonHistoryComboBox_SelectionChanged(
    object sender,
    SelectionChangedEventArgs e
) => ShowSelectedAnalysis(ComparisonHistoryComboBox, ComparisonTextBox);

private void ContradictionHistoryComboBox_SelectionChanged(
    object sender,
    SelectionChangedEventArgs e
) => ShowSelectedAnalysis(ContradictionHistoryComboBox, ContradictionTextBox);

private void DiagnosticHistoryComboBox_SelectionChanged(
    object sender,
    SelectionChangedEventArgs e
) => ShowSelectedAnalysis(DiagnosticHistoryComboBox, DiagnosticTextBox);

private void ClearAnalysisOutputs()
{
    _summaryHistory.Clear();
    _timelineHistory.Clear();
    _extractionHistory.Clear();
    _comparisonHistory.Clear();
    _contradictionHistory.Clear();
    _diagnosticHistory.Clear();

    SummaryTextBox.Clear();
    TimelineTextBox.Clear();
    ExtractionTextBox.Clear();
    ComparisonTextBox.Clear();
    ContradictionTextBox.Clear();
    DiagnosticTextBox.Clear();
}

private bool HasAtLeastTwoDocuments()
{
    return
        _chunks
            .Select(
                chunk => chunk.DocumentName
            )
            .Distinct(
                StringComparer.OrdinalIgnoreCase
            )
            .Take(2)
            .Count() >= 2;
}

    private void EnableQuestions()
    {
        var hasDocuments = _chunks.Count > 0;

        QuestionTextBox.IsEnabled = hasDocuments;
        AskButton.IsEnabled = hasDocuments;
        FolderSummaryButton.IsEnabled = hasDocuments;

        ValidationButton.IsEnabled =
            hasDocuments &&
            !string.IsNullOrWhiteSpace(_currentFolderPath);

        UpdateDocumentSelectionUi();

        if (hasDocuments)
        {
            QuestionTextBox.Focus();
        }
    }



    private void SetBusy(
        bool busy,
        string? message = null
    )
    {
        _isBusy = busy;

        var hasDocuments = _chunks.Count > 0;

        SelectFolderButton.IsEnabled = !busy;
        DocumentsListBox.IsEnabled = !busy;

        AskButton.IsEnabled =
            !busy && hasDocuments;

        FolderSummaryButton.IsEnabled =
            !busy && hasDocuments;

        ValidationButton.IsEnabled =
            !busy &&
            hasDocuments &&
            !string.IsNullOrWhiteSpace(_currentFolderPath);

        QuestionTextBox.IsEnabled = hasDocuments;
        QuestionTextBox.IsReadOnly = busy;

        UpdateDocumentSelectionUi();

        WorkProgressBar.Visibility =
            busy
                ? Visibility.Visible
                : Visibility.Collapsed;

        if (!busy)
        {
            WorkProgressBar.Value = 0;

            EmptyDocumentsPanel.Visibility =
                DocumentsListBox.Items.Count == 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        if (message != null)
        {
            StatusTextBlock.Text = message;
        }
    }


    private void MainWindow_Closed(
        object? sender,
        EventArgs e
    )
    {
        _importCancellation?.Cancel();
        _summaryCancellation?.Cancel();
        _timelineCancellation?.Cancel();
        _extractionCancellation?.Cancel();
        _comparisonCancellation?.Cancel();
        _contradictionCancellation?.Cancel();

        _ocrService.Dispose();

        _http.Dispose();
    }
}


