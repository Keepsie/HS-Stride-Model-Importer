// HS Stride Model Importer (c) 2025 Happenstance Games LLC - MIT License

using System.Numerics;

namespace HS.Stride.Model.Importer.Core.Models
{
    public class FbxMeshInfo
    {
        public required string Name { get; set; }
        public required string OriginalName { get; set; }
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }
        public Vector3 Scale { get; set; } = Vector3.One;
        public int VertexCount { get; set; }
        public int FaceCount { get; set; }
        public List<string> MaterialNames { get; set; } = new();
        public string? ParentName { get; set; }
        public string? NodePath { get; set; }
        public int MeshIndex { get; set; }
        public List<int> MeshIndices { get; set; } = new();
        public List<FbxMeshInfo> Children { get; set; } = new();
    }
}
