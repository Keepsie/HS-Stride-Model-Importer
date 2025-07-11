// HS Stride Model Importer (c) 2025 Happenstance Games LLC - MIT License

using Assimp;
using HS.Stride.Model.Importer.Core.Models;
using System.Numerics;

namespace HS.Stride.Model.Importer.Core.Core
{
    public class FbxProcessor
    {
        private readonly AssimpContext _context;

        public FbxProcessor()
        {
            _context = new AssimpContext();
        }

        public async Task<FbxSplitResult> ProcessFbxFileAsync(string fbxFilePath, string outputDirectory, IProgress<string>? progress = null)
        {
            var result = new FbxSplitResult
            {
                OriginalFilePath = fbxFilePath,
                OutputDirectory = outputDirectory
            };

            try
            {
                progress?.Report("Validating input file...");
                if (!File.Exists(fbxFilePath))
                {
                    result.Errors.Add($"FBX file not found: {fbxFilePath}");
                    return result;
                }

                if (!Directory.Exists(outputDirectory))
                {
                    Directory.CreateDirectory(outputDirectory);
                }

                progress?.Report("Loading 3D model file...");
                await Task.Delay(10); // Allow UI to update
                
                // Load without any post-processing to preserve exact data
                var scene = await Task.Run(() => _context.ImportFile(fbxFilePath, PostProcessSteps.None));
                    
                if (scene == null)
                {
                    result.Errors.Add("Failed to load FBX file");
                    return result;
                }

                progress?.Report("Extracting mesh information...");
                await Task.Delay(10);
                var meshInfos = await Task.Run(() => ExtractAllMeshes(scene));
                result.MeshInfos = meshInfos;

                progress?.Report("Extracting material information...");
                await Task.Delay(10);
                var materialNames = await Task.Run(() => ExtractMaterialNames(scene));
                result.MaterialNames = materialNames;

                // Report detailed mesh finding info
                progress?.Report($"Found {scene.MeshCount} total meshes in scene");
                progress?.Report($"Processing {meshInfos.Count} mesh nodes...");
                
                await Task.Delay(10);
                var generatedFiles = await SplitMeshesToSeparateFilesAsync(scene, fbxFilePath, outputDirectory, meshInfos, progress);
                result.GeneratedFiles = generatedFiles;

                progress?.Report("Processing complete!");
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Error processing FBX file: {ex.Message}");
                result.Success = false;
            }

            return result;
        }

        private List<FbxMeshInfo> ExtractAllMeshes(Scene scene)
        {
            var meshInfos = new List<FbxMeshInfo>();
            var meshCounter = 0;
            
            // First, let's log what we find
            Console.WriteLine($"Scene has {scene.MeshCount} meshes total");
            Console.WriteLine("Scene hierarchy:");
            PrintNodeHierarchy(scene.RootNode, 0);
            
            // Process the entire hierarchy, including nodes without meshes
            ProcessCompleteHierarchy(scene.RootNode, meshInfos, scene, "", ref meshCounter);
            
            // Verify we found all meshes
            var totalMeshesFound = meshInfos.Sum(m => m.MeshIndices.Count);
            Console.WriteLine($"Extracted {meshInfos.Count} mesh nodes containing {totalMeshesFound} meshes");
            
            return meshInfos;
        }

        private void PrintNodeHierarchy(Node node, int depth)
        {
            var indent = new string(' ', depth * 2);
            var meshInfo = node.HasMeshes ? $" (Meshes: {string.Join(",", node.MeshIndices)})" : "";
            Console.WriteLine($"{indent}{node.Name}{meshInfo}");
            
            foreach (var child in node.Children)
            {
                PrintNodeHierarchy(child, depth + 1);
            }
        }

        private void ProcessCompleteHierarchy(Node node, List<FbxMeshInfo> meshInfos, Scene scene, 
            string parentPath, ref int nodeCounter, Node? parentNode = null)
        {
            if (node == null) return;

            var nodePath = string.IsNullOrEmpty(parentPath) ? node.Name : $"{parentPath}/{node.Name}";
            
            // Process this node if it has meshes
            if (node.HasMeshes)
            {
                // Get the local transform (this is relative to parent)
                var localTransform = node.Transform;
                
                // Create a unique name for this mesh group
                var cleanName = CleanNodeName(node.Name);
                var uniqueName = $"{cleanName}_{nodeCounter++:D3}";
                
                var meshInfo = new FbxMeshInfo
                {
                    Name = uniqueName,
                    OriginalName = node.Name,
                    Position = ConvertVector3(localTransform.A4, localTransform.B4, localTransform.C4),
                    Rotation = ExtractRotationFromMatrix(localTransform),
                    Scale = ExtractScaleFromMatrix(localTransform),
                    MeshIndices = node.MeshIndices.ToList(),
                    MaterialNames = new List<string>(),
                    ParentName = parentNode?.Name,
                    NodePath = nodePath
                };

                // Extract material names for all meshes in this node
                var materials = new HashSet<string>();
                foreach (var meshIndex in node.MeshIndices)
                {
                    if (meshIndex < scene.MeshCount)
                    {
                        var mesh = scene.Meshes[meshIndex];
                        materials.UnionWith(ExtractMeshMaterialNames(mesh, scene));
                        
                        // Update vertex and face counts
                        meshInfo.VertexCount += mesh.VertexCount;
                        meshInfo.FaceCount += mesh.FaceCount;
                    }
                }
                meshInfo.MaterialNames.AddRange(materials);
                meshInfos.Add(meshInfo);
                
                Console.WriteLine($"Added mesh node: {meshInfo.Name} with {meshInfo.MeshIndices.Count} meshes");
            }

            // Always process all children, even if parent had no meshes
            foreach (var child in node.Children)
            {
                ProcessCompleteHierarchy(child, meshInfos, scene, nodePath, ref nodeCounter, node);
            }
        }

        private string CleanNodeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Mesh";
                
            // Remove invalid characters for file names
            var invalidChars = Path.GetInvalidFileNameChars();
            var cleanName = name;
            
            foreach (var c in invalidChars)
            {
                cleanName = cleanName.Replace(c, '_');
            }
            
            // Also replace other problematic characters
            cleanName = cleanName.Replace('.', '_')
                                 .Replace(' ', '_')
                                 .Replace('-', '_');
                                 
            return cleanName;
        }

        private Vector3 ConvertVector3(float x, float y, float z) => new(x, y, z);

        private System.Numerics.Quaternion ExtractRotationFromMatrix(Assimp.Matrix4x4 matrix)
        {
            var scale = ExtractScaleFromMatrix(matrix);
            
            // Handle potential zero scale
            if (scale.X == 0) scale.X = 1;
            if (scale.Y == 0) scale.Y = 1;
            if (scale.Z == 0) scale.Z = 1;
            
            // Normalize the rotation matrix by removing scale
            var normalizedMatrix = new System.Numerics.Matrix4x4(
                matrix.A1 / scale.X, matrix.A2 / scale.Y, matrix.A3 / scale.Z, matrix.A4,
                matrix.B1 / scale.X, matrix.B2 / scale.Y, matrix.B3 / scale.Z, matrix.B4,
                matrix.C1 / scale.X, matrix.C2 / scale.Y, matrix.C3 / scale.Z, matrix.C4,
                matrix.D1, matrix.D2, matrix.D3, matrix.D4
            );
            
            return System.Numerics.Quaternion.CreateFromRotationMatrix(normalizedMatrix);
        }

        private Vector3 ExtractScaleFromMatrix(Assimp.Matrix4x4 matrix)
        {
            var scaleX = MathF.Sqrt(matrix.A1 * matrix.A1 + matrix.B1 * matrix.B1 + matrix.C1 * matrix.C1);
            var scaleY = MathF.Sqrt(matrix.A2 * matrix.A2 + matrix.B2 * matrix.B2 + matrix.C2 * matrix.C2);
            var scaleZ = MathF.Sqrt(matrix.A3 * matrix.A3 + matrix.B3 * matrix.B3 + matrix.C3 * matrix.C3);
            
            // Prevent zero scale
            if (scaleX == 0) scaleX = 1;
            if (scaleY == 0) scaleY = 1;
            if (scaleZ == 0) scaleZ = 1;
            
            return new Vector3(scaleX, scaleY, scaleZ);
        }

        private List<string> ExtractMeshMaterialNames(Mesh mesh, Scene scene)
        {
            var names = new List<string>();
            if (mesh.MaterialIndex >= 0 && mesh.MaterialIndex < scene.MaterialCount)
            {
                names.Add(scene.Materials[mesh.MaterialIndex].Name ?? $"Material_{mesh.MaterialIndex}");
            }
            return names;
        }

        private List<string> ExtractMaterialNames(Scene scene)
        {
            return scene.Materials.Select((m, i) => m.Name ?? $"Material_{i}").ToList();
        }

        private async Task<List<string>> SplitMeshesToSeparateFilesAsync(Scene scene, string filePath, 
            string outputDir, List<FbxMeshInfo> meshInfos, IProgress<string>? progress = null)
        {
            var generatedFiles = new List<string>();
            var baseName = Path.GetFileNameWithoutExtension(filePath);

            for (int i = 0; i < meshInfos.Count; i++)
            {
                var meshInfo = meshInfos[i];
                progress?.Report($"Exporting mesh {i + 1}/{meshInfos.Count}: {meshInfo.Name}");
                
                try
                {
                    var outputPath = Path.Combine(outputDir, $"{baseName}_{meshInfo.Name}.fbx");
                    
                    await Task.Run(() =>
                    {
                        var newScene = CreateSceneForMeshNode(scene, meshInfo);
                        
                        if (newScene != null)
                        {
                            // Export with minimal processing to preserve data
                            _context.ExportFile(newScene, outputPath, "fbx", PostProcessSteps.None);
                        }
                    });
                    
                    if (File.Exists(outputPath))
                    {
                        generatedFiles.Add(outputPath);
                        Console.WriteLine($"Exported: {Path.GetFileName(outputPath)}");
                    }
                    else
                    {
                        Console.WriteLine($"Failed to export: {meshInfo.Name}");
                    }
                    
                    await Task.Delay(10); // Allow UI to update
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error exporting {meshInfo.Name}: {ex.Message}");
                }
            }
            
            Console.WriteLine($"Exported {generatedFiles.Count} files");
            return generatedFiles;
        }

        private Scene? CreateSceneForMeshNode(Scene originalScene, FbxMeshInfo meshInfo)
        {
            if (!meshInfo.MeshIndices.Any()) return null;

            var newScene = new Scene { RootNode = new Node("RootNode") };
            
            // Find the original node that contains these mesh indices
            var originalNode = FindNodeByMeshIndices(originalScene.RootNode, meshInfo.MeshIndices);
            if (originalNode == null)
            {
                Console.WriteLine($"Could not find node for mesh indices: {string.Join(",", meshInfo.MeshIndices)}");
                return null;
            }

            // Create the mesh node preserving the original transform
            var meshNode = new Node(meshInfo.OriginalName)
            {
                Transform = originalNode.Transform
            };
            
            // Add all meshes from this node
            foreach (var meshIndex in meshInfo.MeshIndices)
            {
                if (meshIndex >= originalScene.MeshCount) 
                {
                    Console.WriteLine($"Mesh index {meshIndex} out of range (max: {originalScene.MeshCount - 1})");
                    continue;
                }
                
                var mesh = originalScene.Meshes[meshIndex];
                var newMeshIndex = newScene.Meshes.Count;
                meshNode.MeshIndices.Add(newMeshIndex);
                newScene.Meshes.Add(mesh);

                // Preserve material references
                if (mesh.MaterialIndex >= 0 && mesh.MaterialIndex < originalScene.MaterialCount)
                {
                    var material = originalScene.Materials[mesh.MaterialIndex];
                    
                    // Check if we already added this material
                    var existingIndex = -1;
                    for (int i = 0; i < newScene.Materials.Count; i++)
                    {
                        if (newScene.Materials[i] == material)
                        {
                            existingIndex = i;
                            break;
                        }
                    }
                    
                    if (existingIndex == -1)
                    {
                        newScene.Materials.Add(material);
                        mesh.MaterialIndex = newScene.Materials.Count - 1;
                    }
                    else
                    {
                        mesh.MaterialIndex = existingIndex;
                    }
                }
            }
            
            newScene.RootNode.Children.Add(meshNode);
            Console.WriteLine($"Created scene for {meshInfo.Name} with {meshNode.MeshIndices.Count} meshes");
            return newScene;
        }

        private Node? FindNodeByMeshIndices(Node node, List<int> targetIndices)
        {
            // Check if this node has the exact mesh indices we're looking for
            if (node.HasMeshes && node.MeshIndices.Count == targetIndices.Count)
            {
                var matches = true;
                for (int i = 0; i < targetIndices.Count; i++)
                {
                    if (!node.MeshIndices.Contains(targetIndices[i]))
                    {
                        matches = false;
                        break;
                    }
                }
                if (matches) return node;
            }

            // Recursively check children
            foreach (var child in node.Children)
            {
                var result = FindNodeByMeshIndices(child, targetIndices);
                if (result != null) return result;
            }
            
            return null;
        }

        public void Dispose() => _context.Dispose();
    }
}
