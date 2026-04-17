using System;
using System.IO;
using BugAuditScript.Helpers;

public static class PathUtility
{
    private static readonly string LogFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");

    public static string EnsureFile(string filePath)
    {
        try
        {
            string fullPath = Path.GetFullPath(filePath);

            string? directory = Path.GetDirectoryName(fullPath);

            if (string.IsNullOrWhiteSpace(directory))
                throw new Exception("Invalid directory path");

            // Create directory (nested supported)
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                Helper.Log($"Directory created: {directory}");
            }

            // Create file if missing
            if (!File.Exists(fullPath))
            {
                File.WriteAllText(fullPath, "");
                Helper.Log($"File created: {fullPath}");
            }

            return fullPath;
        }
        catch (Exception ex)
        {
            Helper.Log($"EnsureFile FAILED: {ex.Message}");
            return string.Empty; 
        }
    }

    public static bool FileExistsStrict(string filePath)
    {
        try
        {
            string fullPath = Path.GetFullPath(filePath);

            if (!File.Exists(fullPath))
            {
                Helper.Log($"File not found: {fullPath}");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Helper.Log($"FileExistsStrict FAILED: {ex.Message}");
            return false;
        }
    }

   
    public static string GetDirectoryPath(string path)
    {
        try
        {
            string fullPath = Path.GetFullPath(path);

            // If it's a file path → extract directory
            string? directory = Path.GetDirectoryName(fullPath);

            if (string.IsNullOrWhiteSpace(directory))
                throw new Exception("Unable to extract directory");

            if (!Directory.Exists(directory))
            {
                Helper.Log($"Directory does not exist: {directory}");
                return string.Empty;
            }

            return directory;
        }
        catch (Exception ex)
        {
           Helper.Log($"GetDirectoryPath FAILED: {ex.Message}");
            return string.Empty;
        }
    }

  
    public static bool DirectoryExistsStrict(string path)
    {
        try
        {
            string fullPath = Path.GetFullPath(path);

            if (!Directory.Exists(fullPath))
            {
               Helper.Log($"Directory not found: {fullPath}");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Helper.Log($"DirectoryExistsStrict FAILED: {ex.Message}");
            return false;
        }
    }

    
}