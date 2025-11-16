// HS Stride Model Importer (c) 2025 Happenstance Games LLC - Apache License 2.0

using HS.Stride.Model.Importer.Core.Utilities;

namespace HS.Stride.Model.Importer.Core.Utilities
{
    public class AssetGenerator
    {
        public string GenerateModelAsset(string fbxPath, string packageName, string modelName, Dictionary<string, string>? materialReferences = null, string? skeletonReference = null)
        {
            var modelGuid = Guid.NewGuid().ToString();
            var resourcePath = $"../../Resources/{packageName}/{Path.GetFileName(fbxPath)}";

            var materialsSection = "";
            if (materialReferences?.Any() == true)
            {
                var materialEntries = materialReferences.Select(m =>
                    $"    {GenerateMaterialHash(m.Key)}:\n        Name: {m.Key}\n        MaterialInstance:\n            Material: {m.Value}");
                materialsSection = "\n" + string.Join("\n", materialEntries);
            }
            else
            {
                materialsSection = " {}";
            }

            var skeletonRef = string.IsNullOrEmpty(skeletonReference) ? "null" : skeletonReference;
            var sourceHash = FileHelper.GetFileHash(fbxPath);
            var hashKey = GenerateHashKey(resourcePath);

            return $@"!Model
Id: {modelGuid}
SerializedVersion: {{Stride: 2.0.0.0}}
Tags: []
Source: !file {resourcePath}
Skeleton: {skeletonRef}
PivotPosition: {{X: 0.0, Y: 0.0, Z: 0.0}}
Materials:{materialsSection}
Modifiers: {{}}
~SourceHashes:
    {hashKey}~{resourcePath}: {sourceHash}
";
        }

        private string GenerateMaterialHash(string materialName)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            var inputBytes = System.Text.Encoding.UTF8.GetBytes(materialName);
            var hashBytes = md5.ComputeHash(inputBytes);
            return Convert.ToHexString(hashBytes).ToLower();
        }

        private string GenerateHashKey(string resourcePath)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            var inputBytes = System.Text.Encoding.UTF8.GetBytes(resourcePath);
            var hashBytes = md5.ComputeHash(inputBytes);
            return Convert.ToHexString(hashBytes).ToLower();
        }

        public string ExtractGuidFromAsset(string assetContent)
        {
            var lines = assetContent.Split('\n');
            var idLine = lines.FirstOrDefault(l => l.StartsWith("Id: "));
            return idLine?.Substring(4).Trim() ?? Guid.NewGuid().ToString();
        }
    }
}