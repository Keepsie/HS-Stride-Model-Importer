// HS Stride Model Importer (c) 2025 Happenstance Games LLC - Apache License 2.0

namespace HS.Stride.Model.Importer.Core.Utilities
{
    public class TargetProjectStructure
    {
        public ProjectStructureType Type { get; set; }
        public string AssetsPath { get; set; } = string.Empty;
        public string ResourcesPath { get; set; } = string.Empty;
        public string CodePath { get; set; } = string.Empty;
    }

    public enum ProjectStructureType
    {
        Unknown,
        Fresh,    // ProjectName/Assets/ structure
        Template  // Assets/ at root structure
    }

    public static class ProjectStructureDetector
    {
        public static TargetProjectStructure DetectTargetProjectStructure(string projectPath)
        {
            var projectName = Path.GetFileName(projectPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            // Check for Fresh structure: ProjectName/ProjectName/Assets/
            var nestedAssetsPath = Path.Combine(projectPath, projectName, "Assets");
            if (Directory.Exists(nestedAssetsPath))
            {
                return new TargetProjectStructure
                {
                    Type = ProjectStructureType.Fresh,
                    AssetsPath = Path.Combine(projectName, "Assets"),
                    ResourcesPath = Path.Combine(projectName, "Resources"),
                    CodePath = projectName  // Code goes in ProjectName/ folder
                };
            }

            // Check for Template structure: Assets/ at root level
            var rootAssetsPath = Path.Combine(projectPath, "Assets");
            if (Directory.Exists(rootAssetsPath))
            {
                return new TargetProjectStructure
                {
                    Type = ProjectStructureType.Template,
                    AssetsPath = "Assets",
                    ResourcesPath = "Resources",
                    CodePath = DetermineTemplateCodePath(projectPath)
                };
            }

            // Default to Template structure if unclear
            return new TargetProjectStructure
            {
                Type = ProjectStructureType.Template,
                AssetsPath = "Assets",
                ResourcesPath = "Resources",
                CodePath = DetermineTemplateCodePath(projectPath)
            };
        }

        private static string DetermineTemplateCodePath(string projectPath)
        {
            // Template structure: Look for existing .Game folder, otherwise use empty string (root level)
            var gameFolder = Directory.GetDirectories(projectPath, "*.Game", SearchOption.TopDirectoryOnly)
                                    .FirstOrDefault();
            
            if (gameFolder != null)
            {
                return Path.GetFileName(gameFolder);
            }
            
            // No .Game folder exists, code will go at root level
            return "";
        }
    }
}