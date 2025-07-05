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
                var scene = await Task.Run(() => _context.ImportFile(fbxFilePath, PostProcessSteps.None));
                if (scene == null)
                {
                    result.Errors.Add("Failed to load FBX file");
                    return result;
                }

                progress?.Report("Extracting mesh information...");
                await Task.Delay(10);
                var meshInfos = await Task.Run(() => ExtractMeshInfoFromScene(scene));
                result.MeshInfos = meshInfos;

                progress?.Report("Extracting material information...");
                await Task.Delay(10);
                var materialNames = await Task.Run(() => ExtractMaterialNames(scene));
                result.MaterialNames = materialNames;

                progress?.Report($"Splitting into {meshInfos.Count} separate files...");
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

        private List<FbxMeshInfo> ExtractMeshInfoFromScene(Scene scene)
        {
            var meshInfos = new List<FbxMeshInfo>();
            var processedParents = new HashSet<string>();

            foreach (var rootChild in scene.RootNode.Children)
            {
                ExtractParentMeshInfo(rootChild, meshInfos, processedParents, scene);
            }

            return meshInfos;
        }

        private void ExtractParentMeshInfo(Node node, List<FbxMeshInfo> meshInfos, 
            HashSet<string> processedParents, Scene scene, string? parentName = null)
        {
            if (node == null) return;

            if (!node.HasMeshes && node.ChildCount == 0)
                return;

            var uniqueParentName = GenerateUniqueNodeName(node.Name, processedParents);
            if (processedParents.Contains(uniqueParentName))
                return;

            processedParents.Add(uniqueParentName);

            var meshIndices = new List<int>();
            CollectAllMeshIndices(node, scene, meshIndices);

            if (meshIndices.Count > 0)
            {
                var worldTransform = GetWorldTransform(node);
                var meshInfo = new FbxMeshInfo
                {
                    Name = uniqueParentName,
                    OriginalName = node.Name,
                    Position = ConvertVector3(worldTransform.A4, worldTransform.B4, worldTransform.C4),
                    Rotation = ExtractRotationFromMatrix(worldTransform),
                    Scale = ExtractScaleFromMatrix(worldTransform),
                    MeshIndices = meshIndices,
                    MaterialNames = new List<string>(),
                    ParentName = parentName
                };

                var materials = new HashSet<string>();
                foreach (var index in meshIndices)
                {
                    materials.UnionWith(ExtractMeshMaterialNames(scene.Meshes[index], scene));
                }
                meshInfo.MaterialNames.AddRange(materials);
                meshInfos.Add(meshInfo);
            }

            foreach (var childNode in node.Children)
            {
                ExtractParentMeshInfo(childNode, meshInfos, processedParents, scene, uniqueParentName);
            }
        }

        private void CollectAllMeshIndices(Node node, Scene scene, List<int> indices)
        {
            if (node.HasMeshes)
            {
                foreach (var meshIndex in node.MeshIndices)
                {
                    if (!indices.Contains(meshIndex))
                        indices.Add(meshIndex);
                }
            }

            foreach (var childNode in node.Children)
                CollectAllMeshIndices(childNode, scene, indices);
        }

        private string GenerateUniqueNodeName(string baseName, HashSet<string> usedNames)
        {
            var uniqueName = baseName;
            var counter = 1;
            while (usedNames.Contains(uniqueName))
                uniqueName = $"{baseName}_{counter++}";
            return uniqueName;
        }

        private Vector3 ConvertVector3(float x, float y, float z) => new(x, y, z);

        private System.Numerics.Quaternion ExtractRotationFromMatrix(Assimp.Matrix4x4 matrix)
        {
            var scale = ExtractScaleFromMatrix(matrix);
            var normalizedMatrix = new System.Numerics.Matrix4x4(
                matrix.A1 / scale.X, matrix.A2 / scale.Y, matrix.A3 / scale.Z, matrix.A4,
                matrix.B1 / scale.X, matrix.B2 / scale.Y, matrix.B3 / scale.Z, matrix.B4,
                matrix.C1 / scale.X, matrix.C2 / scale.Y, matrix.C3 / scale.Z, matrix.C4,
                matrix.D1, matrix.D2, matrix.D3, matrix.D4
            );
            return System.Numerics.Quaternion.CreateFromRotationMatrix(normalizedMatrix);
        }

        private Vector3 ExtractScaleFromMatrix(Assimp.Matrix4x4 matrix) => new(
            MathF.Sqrt(matrix.A1 * matrix.A1 + matrix.B1 * matrix.B1 + matrix.C1 * matrix.C1),
            MathF.Sqrt(matrix.A2 * matrix.A2 + matrix.B2 * matrix.B2 + matrix.C2 * matrix.C2),
            MathF.Sqrt(matrix.A3 * matrix.A3 + matrix.B3 * matrix.B3 + matrix.C3 * matrix.C3)
        );

        private List<string> ExtractMeshMaterialNames(Mesh mesh, Scene scene)
        {
            var names = new List<string>();
            if (mesh.MaterialIndex >= 0 && mesh.MaterialIndex < scene.MaterialCount)
                names.Add(scene.Materials[mesh.MaterialIndex].Name ?? $"Material_{mesh.MaterialIndex}");
            return names;
        }

        private List<string> ExtractMaterialNames(Scene scene) => 
            scene.Materials.Select((m, i) => m.Name ?? $"Material_{i}").ToList();

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
                        var newScene = CreateMultiMeshScene(scene, meshInfo);
                        
                        if (newScene != null)
                        {
                            _context.ExportFile(newScene, outputPath, "fbx");
                        }
                    });
                    
                    if (File.Exists(outputPath))
                    {
                        generatedFiles.Add(outputPath);
                    }
                    
                    await Task.Delay(10); // Allow UI to update
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error exporting {meshInfo.Name}: {ex.Message}");
                }
            }
            return generatedFiles;
        }

        private Scene? CreateMultiMeshScene(Scene originalScene, FbxMeshInfo meshInfo)
        {
            if (!meshInfo.MeshIndices.Any()) return null;

            var newScene = new Scene { RootNode = new Node("Root") };
            
            foreach (var index in meshInfo.MeshIndices)
            {
                if (index >= originalScene.MeshCount) continue;
                
                var mesh = originalScene.Meshes[index];
                var node = FindNodeContainingMesh(originalScene.RootNode, mesh, originalScene);
                if (node == null) continue;

                ApplyTransformationToMesh(mesh, GetWorldTransform(node));
                
                var meshNode = new Node(mesh.Name ?? $"Mesh_{index}")
                {
                    MeshIndices = { newScene.Meshes.Count }
                };
                newScene.Meshes.Add(mesh);
                newScene.RootNode.Children.Add(meshNode);

                if (mesh.MaterialIndex >= 0 && mesh.MaterialIndex < originalScene.MaterialCount)
                {
                    var material = originalScene.Materials[mesh.MaterialIndex];
                    if (!newScene.Materials.Contains(material))
                        newScene.Materials.Add(material);
                    mesh.MaterialIndex = newScene.Materials.IndexOf(material);
                }
            }
            return newScene;
        }

        private Node? FindNodeContainingMesh(Node node, Mesh targetMesh, Scene scene)
        {
            if (node.HasMeshes && node.MeshIndices.Any(i => scene.Meshes[i] == targetMesh))
                return node;

            foreach (var child in node.Children)
            {
                var result = FindNodeContainingMesh(child, targetMesh, scene);
                if (result != null) return result;
            }
            return null;
        }

        private Assimp.Matrix4x4 GetWorldTransform(Node node)
        {
            var world = node.Transform;
            var current = node.Parent;
            while (current != null)
            {
                world = current.Transform * world;
                current = current.Parent;
            }
            return world;
        }

        private void ApplyTransformationToMesh(Mesh mesh, Assimp.Matrix4x4 transform)
        {
            var matrix = new System.Numerics.Matrix4x4(
                transform.A1, transform.A2, transform.A3, transform.A4,
                transform.B1, transform.B2, transform.B3, transform.B4,
                transform.C1, transform.C2, transform.C3, transform.C4,
                transform.D1, transform.D2, transform.D3, transform.D4);

            // Transform vertices
            for (int i = 0; i < mesh.Vertices.Count; i++)
            {
                var vec = new System.Numerics.Vector3(
                    mesh.Vertices[i].X, 
                    mesh.Vertices[i].Y, 
                    mesh.Vertices[i].Z);
                vec = System.Numerics.Vector3.Transform(vec, matrix);
                mesh.Vertices[i] = new Vector3D(vec.X, vec.Y, vec.Z);
            }

            // Transform normals
            if (mesh.HasNormals)
            {
                for (int i = 0; i < mesh.Normals.Count; i++)
                {
                    var vec = new System.Numerics.Vector3(
                        mesh.Normals[i].X,
                        mesh.Normals[i].Y,
                        mesh.Normals[i].Z);
                    vec = System.Numerics.Vector3.TransformNormal(vec, matrix);
                    mesh.Normals[i] = new Vector3D(vec.X, vec.Y, vec.Z);
                }
            }
        }

        public void Dispose() => _context.Dispose();

    }
}
