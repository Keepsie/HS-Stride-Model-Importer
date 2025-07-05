// HS Stride Model Importer (c) 2025 Happenstance Games LLC - MIT License

using HS.Stride.Model.Importer.Core.Core;

namespace HS.Stride.Model.Importer.Console
{
    internal class Program
    {
        private const string VERSION = "1.0.0";
        
        static async Task Main(string[] args)
        {
            ShowBanner();
            System.Console.WriteLine();

            string fbxFilePath, strideProjectPath;

            if (args.Length < 2)
            {
                ShowInfo("Welcome to the Multi-Mesh Model Importer Wizard!");
                ShowInfo("This tool splits multi-mesh 3D models into individual parts and creates Stride prefabs.");
                System.Console.WriteLine();

                if (!RunWizard(out fbxFilePath, out strideProjectPath))
                {
                    ShowWarning("Import cancelled.");
                    return;
                }
            }
            else
            {
                fbxFilePath = args[0];
                strideProjectPath = args[1];
            }

            // Use the FBX file's directory for temporary split files
            var outputDirectory = Path.GetDirectoryName(fbxFilePath) ?? throw new InvalidOperationException("Could not determine FBX file directory");

            try
            {
                ShowInfo("=== Import Configuration ===");
                System.Console.WriteLine();
                System.Console.WriteLine($"Model File: {fbxFilePath}");
                System.Console.WriteLine($"Temp Directory: {outputDirectory}");
                System.Console.WriteLine($"Stride Project: {strideProjectPath}");
                System.Console.WriteLine();

                using var importer = new StrideModelImporter();

                ShowProgress("Validating inputs...");
                var validation = importer.ValidateInput(fbxFilePath, outputDirectory, strideProjectPath);
                
                if (!validation.IsValid)
                {
                    ShowError("Validation failed:");
                    foreach (var error in validation.Errors)
                        System.Console.WriteLine($"  • {error}");
                    return;
                }

                if (validation.Warnings.Any())
                {
                    ShowWarning("Warnings:");
                    foreach (var warning in validation.Warnings)
                        System.Console.WriteLine($"  • {warning}");
                    System.Console.WriteLine();
                }

                ShowProgress("Processing multi-mesh 3D model file...");
                
                var progress = new Progress<string>(status => 
                {
                    ShowProgress(status);
                });
                
                var prefabName = Path.GetFileNameWithoutExtension(fbxFilePath);
                var result = await importer.ImportModelAsync(fbxFilePath, outputDirectory, strideProjectPath, prefabName, progress);

                System.Console.WriteLine();
                
                if (result.Success)
                {
                    ShowSuccess("✅ Model import completed successfully!");
                    System.Console.WriteLine();
                    
                    if (result.SplitResult != null)
                    {
                        ShowInfo($"📊 Split Results:");
                        System.Console.WriteLine($"  • Found {result.SplitResult.MeshInfos.Count} meshes");
                        System.Console.WriteLine($"  • Generated {result.SplitResult.GeneratedFiles.Count} individual model files");
                        System.Console.WriteLine($"  • Found {result.SplitResult.MaterialNames.Count} materials");
                        System.Console.WriteLine();

                        ShowInfo("🧩 Mesh Details:");
                        foreach (var mesh in result.SplitResult.MeshInfos)
                        {
                            System.Console.WriteLine($"  • {mesh.Name}: {mesh.VertexCount} vertices, {mesh.FaceCount} faces");
                        }
                        System.Console.WriteLine();
                    }

                    if (result.PrefabResult != null)
                    {
                        ShowInfo($"🏗️  Prefab Generated:");
                        System.Console.WriteLine($"  • Name: {result.PrefabResult.PrefabName}");
                        System.Console.WriteLine($"  • Path: {result.PrefabResult.PrefabFilePath}");
                        System.Console.WriteLine();
                    }

                    ShowInfo("📁 All Generated Files:");
                    foreach (var file in result.GeneratedFiles)
                    {
                        System.Console.WriteLine($"  • {Path.GetFileName(file)}");
                    }
                    System.Console.WriteLine();

                    ShowSuccess("Next Steps:");
                    System.Console.WriteLine("  1. Open Stride GameStudio");
                    System.Console.WriteLine("  2. Refresh the Asset View to see imported assets");
                    System.Console.WriteLine("  3. Use the prefab in your scenes!");
                }
                else
                {
                    ShowError("❌ Import failed:");
                    foreach (var error in result.Errors)
                        System.Console.WriteLine($"  • {error}");
                }

                if (result.Warnings.Any())
                {
                    System.Console.WriteLine();
                    ShowWarning("⚠️  Warnings:");
                    foreach (var warning in result.Warnings)
                        System.Console.WriteLine($"  • {warning}");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Fatal error: {ex.Message}");
                if (args.Contains("--debug"))
                {
                    System.Console.WriteLine();
                    System.Console.WriteLine("Stack trace:");
                    System.Console.WriteLine(ex.StackTrace);
                }
            }

            System.Console.WriteLine();
            System.Console.WriteLine("Press any key to exit...");
            System.Console.ReadKey();
        }

        private static bool RunWizard(out string fbxFilePath, out string strideProjectPath)
        {
            fbxFilePath = string.Empty;
            strideProjectPath = string.Empty;

            try
            {
                ShowInfo("=== Step 1/2: Multi-Mesh 3D Model File ===");
                System.Console.WriteLine("Enter the path to your MULTI-MESH 3D model file:");
                System.Console.WriteLine("(Must contain multiple meshes/objects - Supported: .fbx, .obj, .dae, .gltf, .glb)");
                System.Console.Write("> ");
                fbxFilePath = CleanPath(System.Console.ReadLine());

                if (string.IsNullOrEmpty(fbxFilePath))
                {
                    ShowError("Multi-mesh 3D model file path cannot be empty.");
                    return false;
                }

                if (!File.Exists(fbxFilePath))
                {
                    ShowError($"Multi-mesh 3D model file does not exist: {fbxFilePath}");
                    return false;
                }

                var extension = Path.GetExtension(fbxFilePath).ToLowerInvariant();
                var validExtensions = new[] { ".fbx", ".obj", ".dae", ".gltf", ".glb" };
                if (!validExtensions.Contains(extension))
                {
                    ShowError($"File must be a supported multi-mesh 3D model format (.fbx, .obj, .dae, .gltf, .glb). Got: {extension}");
                    return false;
                }

                ShowSuccess($"Found multi-mesh 3D model file: {fbxFilePath}");
                System.Console.WriteLine();

                ShowInfo("=== Step 2/2: Stride Project ===");
                System.Console.WriteLine("Enter the path to your Stride project folder:");
                System.Console.WriteLine("(This should contain your .sln file)");
                System.Console.Write("> ");
                strideProjectPath = CleanPath(System.Console.ReadLine());

                if (string.IsNullOrEmpty(strideProjectPath))
                {
                    ShowError("Stride project path cannot be empty.");
                    return false;
                }

                if (!Directory.Exists(strideProjectPath))
                {
                    ShowError($"Stride project folder does not exist: {strideProjectPath}");
                    return false;
                }

                ShowSuccess($"Using Stride project: {strideProjectPath}");
                System.Console.WriteLine();

                var tempOutputDirectory = Path.GetDirectoryName(fbxFilePath) ?? string.Empty;
                using var importer = new StrideModelImporter();
                var validation = importer.ValidateInput(fbxFilePath, tempOutputDirectory, strideProjectPath);
                
                if (!validation.IsValid)
                {
                    ShowError("Validation failed:");
                    foreach (var error in validation.Errors)
                        System.Console.WriteLine($"  • {error}");
                    return false;
                }

                ShowInfo("Summary:");
                System.Console.WriteLine($"  Model File: {fbxFilePath}");
                System.Console.WriteLine($"  Temp Dir: {tempOutputDirectory}");
                System.Console.WriteLine($"  Stride:   {strideProjectPath}");
                System.Console.WriteLine($"  Prefab:   prefab.sdprefab");
                System.Console.WriteLine();
                System.Console.Write("Proceed with import? (y/N): ");
                var confirm = System.Console.ReadLine()?.Trim().ToLower();
                
                return confirm == "y" || confirm == "yes";
            }
            catch (Exception ex)
            {
                ShowError($"Wizard error: {ex.Message}");
                return false;
            }
        }

        private static void ShowBanner()
        {
            System.Console.WriteLine(
@"
╔═══════════════════════════════════════════════════════════════════════════╗
║                                                                           ║
║  ██╗  ██╗███████╗    ███╗   ███╗ ██████╗ ██████╗ ███████╗██╗              ║
║  ██║  ██║██╔════╝    ████╗ ████║██╔═══██╗██╔══██╗██╔════╝██║              ║
║  ███████║███████╗    ██╔████╔██║██║   ██║██║  ██║█████╗  ██║              ║
║  ██╔══██║╚════██║    ██║╚██╔╝██║██║   ██║██║  ██║██╔══╝  ██║              ║
║  ██║  ██║███████║    ██║ ╚═╝ ██║╚██████╔╝██████╔╝███████╗███████╗         ║
║  ╚═╝  ╚═╝╚══════╝    ╚═╝     ╚═╝ ╚═════╝ ╚═════╝ ╚══════╝╚══════╝         ║
║                                                                           ║
║                   Multi-Mesh Model Importer v" + VERSION + @"                ║
║           © 2025 Happenstance Games LLC - All Rights Reserved             ║
║                                                                           ║
╚═══════════════════════════════════════════════════════════════════════════╝");
        }

        private static void ShowSuccess(string message)
        {
            var originalColor = System.Console.ForegroundColor;
            System.Console.ForegroundColor = ConsoleColor.Green;
            System.Console.WriteLine(message);
            System.Console.ForegroundColor = originalColor;
        }

        private static void ShowError(string message)
        {
            var originalColor = System.Console.ForegroundColor;
            System.Console.ForegroundColor = ConsoleColor.Red;
            System.Console.WriteLine(message);
            System.Console.ForegroundColor = originalColor;
        }

        private static void ShowWarning(string message)
        {
            var originalColor = System.Console.ForegroundColor;
            System.Console.ForegroundColor = ConsoleColor.Yellow;
            System.Console.WriteLine(message);
            System.Console.ForegroundColor = originalColor;
        }

        private static void ShowInfo(string message)
        {
            var originalColor = System.Console.ForegroundColor;
            System.Console.ForegroundColor = ConsoleColor.Cyan;
            System.Console.WriteLine(message);
            System.Console.ForegroundColor = originalColor;
        }

        private static void ShowProgress(string message)
        {
            var originalColor = System.Console.ForegroundColor;
            System.Console.ForegroundColor = ConsoleColor.White;
            System.Console.WriteLine(message);
            System.Console.ForegroundColor = originalColor;
        }

        private static string CleanPath(string? input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            var cleaned = input.Trim();
            
            // Remove surrounding quotes if present
            if ((cleaned.StartsWith("\"") && cleaned.EndsWith("\"")) ||
                (cleaned.StartsWith("'") && cleaned.EndsWith("'")))
            {
                cleaned = cleaned.Substring(1, cleaned.Length - 2);
            }

            return cleaned.Trim();
        }
    }
}