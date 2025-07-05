// HS Stride Model Importer (c) 2025 Happenstance Games LLC - Apache License 2.0

namespace HS.Stride.Model.Importer.Core.Utilities
{
    public static class FileHelper
    {
        public static bool SaveFile(string content, string filePath)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? ".");
                File.WriteAllText(filePath, content);
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        public static bool EnsureDirectoryExists(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool CopyFile(string sourcePath, string destinationPath, bool overwrite = true)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? ".");
                File.Copy(sourcePath, destinationPath, overwrite);
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        public static string GetFileHash(string filePath)
        {
            try
            {
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                using var stream = File.OpenRead(filePath);
                var hashBytes = sha256.ComputeHash(stream);
                return Convert.ToHexString(hashBytes);
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}