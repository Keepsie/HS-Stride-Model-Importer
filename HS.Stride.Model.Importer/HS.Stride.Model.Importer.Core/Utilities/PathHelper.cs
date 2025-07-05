// HS Stride Model Importer (c) 2025 Happenstance Games LLC - Apache License 2.0

namespace HS.Stride.Model.Importer.Core.Utilities
{
    public static class PathHelper
    {
        public static bool IsStrideProject(string directoryPath)
        {
            var validation = ValidateStrideProject(directoryPath);
            return validation.IsValid;
        }

        public static ProjectValidationResult ValidateStrideProject(string directoryPath)
        {
            var result = new ProjectValidationResult();

            try
            {
                if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
                {
                    result.IsValid = false;
                    result.ErrorMessage = "Directory does not exist";
                    return result;
                }

                // Check for .sln files in the root directory
                var slnFiles = Directory.GetFiles(directoryPath, "*.sln", SearchOption.TopDirectoryOnly);
                result.HasSolutionFile = slnFiles.Any();

                // Check for .sdpkg files (can be in subdirectories)
                var sdpkgFiles = Directory.GetFiles(directoryPath, "*.sdpkg", SearchOption.AllDirectories);
                result.HasStridePackages = sdpkgFiles.Any();

                // Determine validity and create helpful messages
                if (result.HasSolutionFile && result.HasStridePackages)
                {
                    result.IsValid = true;
                    result.SuccessMessage = "✓ Valid Stride project (Visual Studio solution with Stride packages)";
                }
                else if (!result.HasSolutionFile && !result.HasStridePackages)
                {
                    result.IsValid = false;
                    result.ErrorMessage = "❌ Not a Stride project root. Please select the Visual Studio solution folder containing the .sln file";
                    result.Suggestions.Add("Look for a folder containing a .sln file (Visual Studio solution)");
                    result.Suggestions.Add("The mass importer will automatically find Stride packages in the project");
                }
                else if (!result.HasSolutionFile)
                {
                    result.IsValid = false;
                    result.ErrorMessage = "❌ Found Stride packages but no Visual Studio solution. Please select the folder containing the .sln file";
                    result.Suggestions.Add("Look for the directory containing the .sln file");
                }
                else // !result.HasStridePackages
                {
                    result.IsValid = false;
                    result.ErrorMessage = "❌ Found Visual Studio solution but no Stride packages. This may not be a Stride project";
                    result.Suggestions.Add("Ensure this is a Stride game project, not just any Visual Studio solution");
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.ErrorMessage = $"Error validating project: {ex.Message}";
            }

            return result;
        }
        
        public static bool IsImageFile(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLower();
            return new[] { ".png", ".jpg", ".jpeg", ".tga", ".dds", ".bmp" }.Contains(extension);
        }

        public static bool IsModelFile(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLower();
            return new[] { ".fbx", ".obj", ".dae", ".gltf", ".glb" }.Contains(extension);
        }

        public static bool IsAudioFile(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLower();
            return new[] { ".wav", ".ogg", ".mp3", ".flac" }.Contains(extension);
        }

        public static bool IsRawAssetFile(string filePath)
        {
            // Files that should be imported as raw assets (preserved as-is)
            var extension = Path.GetExtension(filePath).ToLower();
            var rawExtensions = new[] 
            { 
                ".txt", ".json", ".xml", ".csv" //Hmm we can add more but i think this is good enough for now.
            };
            return rawExtensions.Contains(extension);
        }

        public static bool ShouldIgnoreFile(string filePath)
        {
            // Files that should be completely ignored during import
            var fileName = Path.GetFileName(filePath).ToLower();
            var extension = Path.GetExtension(filePath).ToLower();
            
            var ignoredExtensions = new[] { ".meta", ".tmp", ".temp", ".log", ".bak", ".old" };
            var ignoredFiles = new[] { "thumbs.db", ".ds_store", "desktop.ini" };
            
            return ignoredExtensions.Contains(extension) || ignoredFiles.Contains(fileName);
        }

        public static string MakeValidAssetName(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "Asset";

            // Remove file extension
            var nameWithoutExt = Path.GetFileNameWithoutExtension(input);

            // Replace invalid characters with underscores
            var invalidChars = Path.GetInvalidFileNameChars().Concat(new[] { ' ', '-', '.' }).ToArray();
            var validName = new string(nameWithoutExt.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());

            // Ensure it starts with a letter or underscore
            if (!char.IsLetter(validName[0]) && validName[0] != '_')
                validName = "_" + validName;

            return validName;
        }
        
    }

    public class ProjectValidationResult
    {
        public bool IsValid { get; set; }
        public bool HasSolutionFile { get; set; }
        public bool HasStridePackages { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string SuccessMessage { get; set; } = string.Empty;
        public List<string> Suggestions { get; set; } = new();
    }
}