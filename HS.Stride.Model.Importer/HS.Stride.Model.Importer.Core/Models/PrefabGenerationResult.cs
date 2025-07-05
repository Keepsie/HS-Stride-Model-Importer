// HS Stride Model Importer (c) 2025 Happenstance Games LLC - MIT License

namespace HS.Stride.Model.Importer.Core.Models
{
    public class PrefabGenerationResult
    {
        public required string PrefabFilePath { get; set; }
        public required string PrefabName { get; set; }
        public List<string> ImportedAssets { get; set; } = new();
        public bool Success { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }
}