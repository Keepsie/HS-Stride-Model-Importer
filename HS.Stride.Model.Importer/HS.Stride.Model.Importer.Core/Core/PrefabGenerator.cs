// HS Stride Model Importer (c) 2025 Happenstance Games LLC - MIT License

using HS.Stride.Model.Importer.Core.Models;
using System.Globalization;
using System.Text;

namespace HS.Stride.Model.Importer.Core.Core
{
    public class PrefabGenerator
    {
        public PrefabGenerationResult GeneratePrefab(FbxSplitResult splitResult, string prefabName, string outputDirectory, Dictionary<string, string> assetReferences, bool applyFbxFixes = true)
        {
            var result = new PrefabGenerationResult
            {
                PrefabName = prefabName,
                PrefabFilePath = Path.Combine(outputDirectory, $"{prefabName}.sdprefab")
            };

            try
            {
                var prefabContent = GeneratePrefabContent(prefabName, splitResult.MeshInfos, assetReferences, applyFbxFixes);
                File.WriteAllText(result.PrefabFilePath, prefabContent);

                result.ImportedAssets.AddRange(assetReferences.Keys);
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Error generating prefab: {ex.Message}");
                result.Success = false;
            }

            return result;
        }

        private string GeneratePrefabContent(string prefabName, List<FbxMeshInfo> meshInfos, Dictionary<string, string> assetReferences, bool applyFbxFixes)
        {
            var rootGuid = Guid.NewGuid().ToString();
            var prefabGuid = Guid.NewGuid().ToString();

            var sb = new StringBuilder();
            sb.AppendLine("!PrefabAsset");
            sb.AppendLine($"Id: {prefabGuid}");
            sb.AppendLine("SerializedVersion: {Stride: 3.1.0.1}");
            sb.AppendLine("Tags: []");
            sb.AppendLine("Hierarchy:");
            sb.AppendLine("    RootParts:");
            sb.AppendLine($"        - ref!! {rootGuid}");
            sb.AppendLine("    Parts:");

            var entityParts = new List<string>();
            var childReferences = new List<string>();

            foreach (var meshInfo in meshInfos)
            {
                var entityGuid = Guid.NewGuid().ToString();
                var transformGuid = Guid.NewGuid().ToString();
                var modelGuid = Guid.NewGuid().ToString();

                if (assetReferences.TryGetValue(meshInfo.Name, out var assetReference))
                {
                    entityParts.Add(GenerateEntityPart(meshInfo, entityGuid, transformGuid, modelGuid, assetReference, applyFbxFixes));
                    childReferences.Add(GenerateChildReference(transformGuid)); // Use transformGuid, not entityGuid!
                }
            }

            var rootEntity = GenerateRootEntity(prefabName, rootGuid, childReferences);
            sb.AppendLine(rootEntity);

            foreach (var entityPart in entityParts)
            {
                sb.AppendLine(entityPart);
            }

            return sb.ToString();
        }

        private string GenerateRootEntity(string prefabName, string rootGuid, List<string> childReferences)
        {
            var transformGuid = Guid.NewGuid().ToString();
            var sb = new StringBuilder();

            sb.AppendLine("        -   Entity:");
            sb.AppendLine($"                Id: {rootGuid}");
            sb.AppendLine($"                Name: {prefabName}");
            sb.AppendLine("                Components:");
            sb.AppendLine("                    961ae8a7666cc3785b61a3ec150b76ed: !TransformComponent");
            sb.AppendLine($"                        Id: {transformGuid}");
            sb.AppendLine("                        Position: {X: 0.0, Y: 0.0, Z: 0.0}");
            sb.AppendLine("                        Rotation: {X: 0.0, Y: 0.0, Z: 0.0, W: 1.0}");
            sb.AppendLine("                        Scale: {X: 1.0, Y: 1.0, Z: 1.0}");

            // Write Children - either {} for empty or proper mapping with references
            if (childReferences.Count == 0)
            {
                sb.AppendLine("                        Children: {}");
            }
            else
            {
                sb.AppendLine("                        Children:");
                foreach (var childRef in childReferences)
                {
                    sb.AppendLine($"                            {childRef}");
                }
            }

            return sb.ToString();
        }

        private string GenerateChildReference(string entityGuid)
        {
            var refGuid = Guid.NewGuid().ToString("N");
            return $"{refGuid}: ref!! {entityGuid}";
        }

        private string GenerateEntityPart(FbxMeshInfo meshInfo, string entityGuid, string transformGuid, string modelGuid, string assetReference, bool applyFbxFixes)
        {
            var sb = new StringBuilder();
            // FBX uses centimeters, so divide by 100 to convert to meters. GLB/GLTF already use meters.
            var p = applyFbxFixes
                ? new System.Numerics.Vector3(meshInfo.Position.X / 100f, meshInfo.Position.Y / 100f, meshInfo.Position.Z / 100f)
                : meshInfo.Position;
            var r = meshInfo.Rotation;
            var s = applyFbxFixes ? meshInfo.Scale : System.Numerics.Vector3.One;

            sb.AppendLine("        -   Entity:");
            sb.AppendLine($"                Id: {entityGuid}");
            sb.AppendLine($"                Name: {meshInfo.Name}");
            sb.AppendLine("                Components:");
            sb.AppendLine("                    e9f67a8a0ea22804b0285279fe8c2c41: !TransformComponent");
            sb.AppendLine($"                        Id: {transformGuid}");
            sb.AppendLine($"                        Position: {{X: {p.X.ToString(CultureInfo.InvariantCulture)}, Y: {p.Y.ToString(CultureInfo.InvariantCulture)}, Z: {p.Z.ToString(CultureInfo.InvariantCulture)}}}");
            sb.AppendLine($"                        Rotation: {{X: {r.X.ToString(CultureInfo.InvariantCulture)}, Y: {r.Y.ToString(CultureInfo.InvariantCulture)}, Z: {r.Z.ToString(CultureInfo.InvariantCulture)}, W: {r.W.ToString(CultureInfo.InvariantCulture)}}}");
            sb.AppendLine($"                        Scale: {{X: {s.X.ToString(CultureInfo.InvariantCulture)}, Y: {s.Y.ToString(CultureInfo.InvariantCulture)}, Z: {s.Z.ToString(CultureInfo.InvariantCulture)}}}");
            sb.AppendLine("                        Children: {}");
            sb.AppendLine("                    9a5a0c793a9d36c3de89e0e51b089965: !ModelComponent");
            sb.AppendLine($"                        Id: {modelGuid}");
            sb.AppendLine($"                        Model: {assetReference}");
            sb.AppendLine("                        Materials: {}");

            return sb.ToString();
        }
    }
}