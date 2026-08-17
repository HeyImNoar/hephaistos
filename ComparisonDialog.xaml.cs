using Hephaistos.Models;
using System.Windows;

namespace Hephaistos;

public partial class ComparisonDialog : Window
{
    public DocumentSummary? SelectedDocumentA
    {
        get;
        private set;
    }

    public DocumentSummary? SelectedDocumentB
    {
        get;
        private set;
    }

    public ComparisonDialog(
        IReadOnlyList<DocumentSummary> documents
    )
    {
        InitializeComponent();

        DocumentAComboBox.ItemsSource =
            documents;

        DocumentBComboBox.ItemsSource =
            documents;

        if (documents.Count > 0)
        {
            DocumentAComboBox.SelectedIndex =
                0;
        }

        if (documents.Count > 1)
        {
            DocumentBComboBox.SelectedIndex =
                1;
        }
    }

    private void CompareButton_Click(
        object sender,
        RoutedEventArgs e
    )
    {
        if (
            DocumentAComboBox.SelectedItem
                is not DocumentSummary documentA ||
            DocumentBComboBox.SelectedItem
                is not DocumentSummary documentB
        )
        {
            MessageBox.Show(
                "Sélectionne deux documents.",
                "Héphaïstos",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );

            return;
        }

        if (
            string.Equals(
                documentA.DocumentName,
                documentB.DocumentName,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            MessageBox.Show(
                "Les documents A et B doivent être différents.",
                "Héphaïstos",
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );

            return;
        }

        SelectedDocumentA =
            documentA;

        SelectedDocumentB =
            documentB;

        DialogResult =
            true;
    }
}