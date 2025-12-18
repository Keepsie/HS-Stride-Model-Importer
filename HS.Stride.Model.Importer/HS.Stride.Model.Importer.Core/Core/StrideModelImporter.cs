// HS Stride Model Importer (c) 2025 Happenstance Games LLC - MIT License

using HS.Stride.Model.Importer.Core.Models;
using HS.Stride.Model.Importer.Core.Utilities;

namespace HS.Stride.Model.Importer.Core.Core
{
    public class StrideModelImporter : IDisposable
    {
        private readonly FbxProcessor _fbxProcessor;
        private readonly PrefabGenerator _prefabGenerator;
        private readonly AssetGenerator _assetGenerator;

        public StrideModelImporter()
        {
            _fbxProcessor = new FbxProcessor();
            _prefabGenerator = new PrefabGenerator();
            _assetGenerator = new AssetGenerator();
        }

        public async Task<ModelImportResult> ImportModelAsync(string fbxFilePath, string outputDirectory, string strideProjectPath, string? prefabName = null, IProgress<string>? progress = null)
        {
            var result = new ModelImportResult
            {
                OriginalFilePath = fbxFilePath,
                OutputDirectory = outputDirectory
            };

            try
            {
                var baseName = Path.GetFileNameWithoutExtension(fbxFilePath);
                var actualPrefabName = prefabName ?? baseName;
                var splitOutputDir = Path.Combine(outputDirectory, "Split");

                progress?.Report("Initializing FBX processing...");
                var splitResult = await _fbxProcessor.ProcessFbxFileAsync(fbxFilePath, splitOutputDir, progress);
                result.SplitResult = splitResult;

                if (!splitResult.Success)
                {
                    result.Errors.AddRange(splitResult.Errors);
                    return result;
                }

                if (splitResult.MeshInfos.Count == 0)
                {
                    result.Warnings.Add("No meshes found in FBX file");
                    result.Success = true;
                    return result;
                }

                if (splitResult.MeshInfos.Count == 1)
                {
                    result.Warnings.Add("SINGLE MESH DETECTED - This tool is only for MULTI-MESH models!");
                    result.Warnings.Add("Your model contains only one mesh - use regular Stride import instead");
                    result.Warnings.Add("Multi-Mesh Model Importer requires models with 2+ separate mesh objects");
                    result.Success = false;
                    return result;
                }

                progress?.Report("Creating Stride assets...");
                var assetReferences = await CreateStrideAssetsAsync(splitResult, baseName, strideProjectPath, outputDirectory, progress);
                
                // Place prefab in the assets directory, not output directory
                var projectStructure = ProjectStructureDetector.DetectTargetProjectStructure(strideProjectPath);
                var prefabOutputDirectory = Path.Combine(strideProjectPath, projectStructure.AssetsPath, baseName);
                
                progress?.Report("Generating prefab...");
                var isFbx = Path.GetExtension(fbxFilePath).Equals(".fbx", StringComparison.OrdinalIgnoreCase);
                var prefabResult = _prefabGenerator.GeneratePrefab(splitResult, actualPrefabName, prefabOutputDirectory, assetReferences, applyFbxFixes: isFbx);
                result.PrefabResult = prefabResult;

                if (!prefabResult.Success)
                {
                    result.Errors.AddRange(prefabResult.Errors);
                    return result;
                }

                result.Success = true;
                result.GeneratedFiles.AddRange(splitResult.GeneratedFiles);
                result.GeneratedFiles.Add(prefabResult.PrefabFilePath);

                progress?.Report("Cleaning up temporary files...");
                // Clean up temporary split files and folder
                CleanupTempFiles(splitResult.GeneratedFiles, splitResult.OutputDirectory);
                progress?.Report("Import completed successfully!");
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Unexpected error during import: {ex.Message}");
                result.Success = false;
            }

            return result;
        }

        private async Task<Dictionary<string, string>> CreateStrideAssetsAsync(FbxSplitResult splitResult, string baseName, string strideProjectPath, string outputDirectory, IProgress<string>? progress = null)
        {
            var references = new Dictionary<string, string>();
            var packageName = baseName;
            
            // Detect project structure like Mass Importer does
            var projectStructure = ProjectStructureDetector.DetectTargetProjectStructure(strideProjectPath);
            
            var targetAssets = Path.Combine(strideProjectPath, projectStructure.AssetsPath, packageName);
            var targetResources = Path.Combine(strideProjectPath, projectStructure.ResourcesPath, packageName);
            
            FileHelper.EnsureDirectoryExists(targetAssets);
            FileHelper.EnsureDirectoryExists(targetResources);

            for (int i = 0; i < splitResult.GeneratedFiles.Count; i++)
            {
                var generatedFile = splitResult.GeneratedFiles[i];
                progress?.Report($"Creating asset {i + 1}/{splitResult.GeneratedFiles.Count}");
                
                try
                {
                    var fileName = Path.GetFileNameWithoutExtension(generatedFile);
                    var meshInfo = splitResult.MeshInfos.FirstOrDefault(m => generatedFile.Contains(m.Name));
                    if (meshInfo == null) continue;

                    var resourceFileName = Path.GetFileName(generatedFile);
                    var targetResourcePath = Path.Combine(targetResources, resourceFileName);
                    await Task.Run(() => FileHelper.CopyFile(generatedFile, targetResourcePath));

                    var assetContent = await Task.Run(() => _assetGenerator.GenerateModelAsset(generatedFile, packageName, meshInfo.Name));
                    var updatedAssetContent = UpdateAssetResourcePaths(assetContent, targetAssets, targetResourcePath);
                    
                    var assetFilePath = Path.Combine(targetAssets, $"{meshInfo.Name}.sdm3d");
                    await Task.Run(() => FileHelper.SaveFile(updatedAssetContent, assetFilePath));

                    var guid = _assetGenerator.ExtractGuidFromAsset(updatedAssetContent);
                    references[meshInfo.Name] = $"{guid}:{meshInfo.Name}";
                    
                    await Task.Delay(10); // Allow UI to update
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not create asset for {Path.GetFileName(generatedFile)}: {ex.Message}");
                }
            }

            return references;
        }

        private string UpdateAssetResourcePaths(string assetContent, string currentAssetFolder, string actualResourcePath)
        {
            var relativePath = Path.GetRelativePath(currentAssetFolder, actualResourcePath);
            relativePath = relativePath.Replace('\\', '/');
            
            var resourceFileName = Path.GetFileName(actualResourcePath);
            var packageName = Path.GetFileName(Path.GetDirectoryName(actualResourcePath) ?? "");
            var oldPattern = $"../../Resources/{packageName}/{resourceFileName}";
            var newPattern = relativePath;
            
            var updatedContent = assetContent.Replace($"Source: !file {oldPattern}", $"Source: !file {newPattern}");
            updatedContent = updatedContent.Replace($"!file {oldPattern}", $"!file {newPattern}");
            
            return updatedContent;
        }

        public ValidationResult ValidateInput(string modelFilePath, string outputDirectory, string strideProjectPath)
        {
            var result = new ValidationResult();

            if (string.IsNullOrWhiteSpace(modelFilePath))
            {
                result.Errors.Add("3D model file path cannot be empty");
                return result;
            }

            if (!File.Exists(modelFilePath))
            {
                result.Errors.Add($"3D model file not found: {modelFilePath}");
                return result;
            }

            var extension = Path.GetExtension(modelFilePath).ToLowerInvariant();
            var validExtensions = new[] { ".fbx", ".obj", ".dae", ".gltf", ".glb" };
            if (!validExtensions.Contains(extension))
            {
                result.Errors.Add($"File must be a supported 3D model format (.fbx, .obj, .dae, .gltf, .glb). Got: {extension}");
                return result;
            }

            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                result.Errors.Add("Output directory cannot be empty");
                return result;
            }

            // Validate Stride project using Mass Importer logic
            var projectValidation = PathHelper.ValidateStrideProject(strideProjectPath);
            if (!projectValidation.IsValid)
            {
                result.Errors.Add(projectValidation.ErrorMessage);
                result.Errors.AddRange(projectValidation.Suggestions);
                return result;
            }

            try
            {
                var fileInfo = new FileInfo(modelFilePath);
                if (fileInfo.Length == 0)
                {
                    result.Errors.Add("3D model file is empty");
                    return result;
                }

                if (fileInfo.Length > 500 * 1024 * 1024) // 500MB
                {
                    result.Warnings.Add("3D model file is very large (>500MB) - processing may take a while");
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Could not access 3D model file: {ex.Message}");
                return result;
            }

            return result;
        }

        private void CleanupTempFiles(List<string> tempFiles, string splitDirectory)
        {
            // Delete individual files first
            foreach (var file in tempFiles)
            {
                try
                {
                    if (File.Exists(file))
                    {
                        File.Delete(file);
                    }
                }
                catch
                {
                    // Ignore cleanup errors - not critical
                }
            }

            // Delete the Split folder if it exists and is empty
            try
            {
                if (Directory.Exists(splitDirectory))
                {
                    // Only delete if empty (safety check)
                    if (!Directory.GetFiles(splitDirectory, "*", SearchOption.AllDirectories).Any())
                    {
                        Directory.Delete(splitDirectory, true);
                    }
                }
            }
            catch
            {
                // Ignore cleanup errors - not critical
            }
        }

        public void Dispose()
        {
            _fbxProcessor?.Dispose();
        }
    }

    public class ModelImportResult
    {
        public required string OriginalFilePath { get; set; }
        public required string OutputDirectory { get; set; }
        public FbxSplitResult? SplitResult { get; set; }
        public PrefabGenerationResult? PrefabResult { get; set; }
        public List<string> GeneratedFiles { get; set; } = new();
        public bool Success { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    public class ValidationResult
    {
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public bool IsValid => !Errors.Any();
    }
}