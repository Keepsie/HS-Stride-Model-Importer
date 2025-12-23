// HS Stride Model Importer (c) 2025 Happenstance Games LLC - MIT License

using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using HS.Stride.Model.Importer.Core.Core;

namespace HS.Stride.Model.Importer.UI
{
    public partial class MainWindow : Window
    {
        private readonly StrideModelImporter _modelImporter;
        private readonly FbxProcessor _fbxProcessor;

        public MainWindow()
        {
            InitializeComponent();
            _modelImporter = new StrideModelImporter();
            _fbxProcessor = new FbxProcessor();

            var version = Assembly.GetExecutingAssembly().GetName().Version;
            Title = $"HS Stride Model Importer v{version?.Major}.{version?.Minor}.{version?.Build} - © 2025 Happenstance Games";
        }

        #region Multi-Mesh Tab Event Handlers

        private void BrowseModelButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Multi-Mesh 3D Model File",
                Filter = "3D Model Files|*.fbx;*.obj;*.dae;*.gltf;*.glb|All Files|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                ModelFileBox.Text = dialog.FileName;
            }
        }

        private void BrowseProjectButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Stride Project Solution",
                Filter = "Solution Files|*.sln|All Files|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                // Store the directory containing the .sln file
                StrideProjectBox.Text = Path.GetDirectoryName(dialog.FileName) ?? dialog.FileName;
            }
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ModelFileBox.Text) || string.IsNullOrWhiteSpace(StrideProjectBox.Text))
            {
                System.Windows.MessageBox.Show("Please select both a model file and Stride project directory.", "Missing Information", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Validate file extensions
            var extension = Path.GetExtension(ModelFileBox.Text).ToLowerInvariant();
            var validExtensions = new[] { ".fbx", ".obj", ".dae", ".gltf", ".glb" };
            if (!validExtensions.Contains(extension))
            {
                System.Windows.MessageBox.Show($"Unsupported file format: {extension}\nSupported formats: .fbx, .obj, .dae, .gltf, .glb", 
                    "Invalid File Format", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            ImportButton.IsEnabled = false;
            ImportProgressBar.Visibility = Visibility.Visible;
            ImportProgressBar.IsIndeterminate = true;
            ImportStatusText.Text = "Processing multi-mesh model...";

            try
            {
                var outputDirectory = Path.GetDirectoryName(ModelFileBox.Text);
                var prefabName = Path.GetFileNameWithoutExtension(ModelFileBox.Text);

                var validation = _modelImporter.ValidateInput(ModelFileBox.Text, outputDirectory!, StrideProjectBox.Text);
                if (!validation.IsValid)
                {
                    System.Windows.MessageBox.Show($"Validation failed:\n{string.Join("\n", validation.Errors)}", 
                        "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var progress = new Progress<string>(status => 
                {
                    ImportStatusText.Text = status;
                });
                
                var result = await _modelImporter.ImportModelAsync(ModelFileBox.Text, outputDirectory!, StrideProjectBox.Text, prefabName, progress);

                if (result.Success)
                {
                    ImportStatusText.Text = $"Successfully imported! Generated {result.GeneratedFiles.Count} files.";
                    System.Windows.MessageBox.Show($"Multi-mesh model imported successfully!\n\nGenerated Files: {result.GeneratedFiles.Count}\n" +
                        $"Prefab: {result.PrefabResult?.PrefabFilePath ?? "None"}\n\n" +
                        "Open Stride GameStudio and refresh the Asset View to see your imported assets.", 
                        "Import Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    var errorMessage = string.Join("\n", result.Errors);
                    ImportStatusText.Text = "Import failed";
                    System.Windows.MessageBox.Show($"Import failed:\n{errorMessage}", "Import Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }

                if (result.Warnings.Any())
                {
                    var warningMessage = string.Join("\n", result.Warnings);
                    System.Windows.MessageBox.Show($"Warnings:\n{warningMessage}", "Import Warnings", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                ImportStatusText.Text = "Import failed";
                System.Windows.MessageBox.Show($"An error occurred during import:\n{ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ImportButton.IsEnabled = true;
                ImportProgressBar.Visibility = Visibility.Collapsed;
                ImportProgressBar.IsIndeterminate = false;
            }
        }

        #endregion


        protected override void OnClosed(EventArgs e)
        {
            _modelImporter?.Dispose();
            _fbxProcessor?.Dispose();
            base.OnClosed(e);
        }
    }
}