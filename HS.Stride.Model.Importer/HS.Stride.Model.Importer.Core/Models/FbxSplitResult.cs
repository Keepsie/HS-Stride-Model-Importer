// HS Stride Model Importer (c) 2025 Happenstance Games LLC - MIT License

namespace HS.Stride.Model.Importer.Core.Models
{
    public class FbxSplitResult
    {
        public required string OriginalFilePath { get; set; }
        public required string OutputDirectory { get; set; }
        public List<FbxMeshInfo> MeshInfos { get; set; } = new();
        public List<string> GeneratedFiles { get; set; } = new();
        public List<string> MaterialNames { get; set; } = new();
        public bool Success { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }
}