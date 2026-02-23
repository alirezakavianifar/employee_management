using System;
using System.IO;
using System.Linq;

namespace Shared.Utils
{
    public static class DataCleanupHelper
    {
        /// <summary>
        /// Cleans up infinite nested folder structures and consolidates data to a single level
        /// </summary>
        /// <param name="dataDirectory">The root data directory to clean up</param>
        /// <returns>True if cleanup was successful</returns>
        public static bool CleanupNestedStructure(string dataDirectory)
        {
            try
            {
                Console.WriteLine($"Starting cleanup of nested structure in: {dataDirectory}");
                
                if (!Directory.Exists(dataDirectory))
                {
                    Console.WriteLine("Data directory does not exist");
                    return false;
                }

                // Find the deepest level of the nested structure
                var deepestLevel = FindDeepestNestedLevel(dataDirectory);
                if (deepestLevel == 0)
                {
                    Console.WriteLine("No nested structure found");
                    return true;
                }

                Console.WriteLine($"Found nested structure with {deepestLevel} levels");

                // Create a temporary directory for consolidation
                var tempDir = Path.Combine(Path.GetTempPath(), "DataCleanup_" + Guid.NewGuid().ToString("N")[..8]);
                Directory.CreateDirectory(tempDir);

                try
                {
                    // Copy all actual data files (not empty nested folders) to temp directory
                    ConsolidateDataFiles(dataDirectory, tempDir);

                    // Remove the old nested structure
                    RemoveNestedStructure(dataDirectory);

                    // Move consolidated data back
                    MoveConsolidatedData(tempDir, dataDirectory);

                    Console.WriteLine("Cleanup completed successfully");
                    return true;
                }
                finally
                {
                    // Clean up temp directory
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during cleanup: {ex.Message}");
                return false;
            }
        }

        private static int FindDeepestNestedLevel(string directory)
        {
            var maxDepth = 0;
            var currentDepth = 0;
            
            FindDeepestNestedLevelRecursive(directory, currentDepth, ref maxDepth);
            return maxDepth;
        }

        private static void FindDeepestNestedLevelRecursive(string directory, int currentDepth, ref int maxDepth)
        {
            if (currentDepth > maxDepth)
            {
                maxDepth = currentDepth;
            }

            if (currentDepth > 20) // Prevent infinite recursion
            {
                return;
            }

            try
            {
                var subDirs = Directory.GetDirectories(directory);
                foreach (var subDir in subDirs)
                {
                    var dirName = Path.GetFileName(subDir);
                    if (dirName.Equals("SharedData", StringComparison.OrdinalIgnoreCase) || 
                        dirName.Equals("New folder", StringComparison.OrdinalIgnoreCase))
                    {
                        FindDeepestNestedLevelRecursive(subDir, currentDepth + 1, ref maxDepth);
                    }
                }
            }
            catch
            {
                // Ignore errors when traversing
            }
        }

        private static void ConsolidateDataFiles(string sourceDir, string targetDir)
        {
            try
            {
                var sourceDirInfo = new DirectoryInfo(sourceDir);
                
                // Copy files from current directory
                foreach (var file in sourceDirInfo.GetFiles())
                {
                    var targetFile = Path.Combine(targetDir, file.Name);
                    if (!File.Exists(targetFile))
                    {
                        file.CopyTo(targetFile, true);
                        Console.WriteLine($"Copied file: {file.Name}");
                    }
                }

                // Process subdirectories
                foreach (var subDir in sourceDirInfo.GetDirectories())
                {
                    var dirName = Path.GetFileName(subDir.FullName);
                    
                    // Skip empty nested folders
                    if (dirName.Equals("SharedData", StringComparison.OrdinalIgnoreCase) || 
                        dirName.Equals("New folder", StringComparison.OrdinalIgnoreCase))
                    {
                        // Recursively process nested structure
                        ConsolidateDataFiles(subDir.FullName, targetDir);
                    }
                    else
                    {
                        // This is a real data directory (Reports, Images, Logs, etc.)
                        var targetSubDir = Path.Combine(targetDir, dirName);
                        if (!Directory.Exists(targetSubDir))
                        {
                            Directory.CreateDirectory(targetSubDir);
                        }
                        
                        // Copy all files from this directory
                        foreach (var file in subDir.GetFiles("*", SearchOption.AllDirectories))
                        {
                            var relativePath = Path.GetRelativePath(subDir.FullName, file.FullName);
                            var targetFile = Path.Combine(targetSubDir, relativePath);
                            var targetFileDir = Path.GetDirectoryName(targetFile);
                            
                            if (!Directory.Exists(targetFileDir))
                            {
                                Directory.CreateDirectory(targetFileDir);
                            }
                            
                            file.CopyTo(targetFile, true);
                        }
                        
                        Console.WriteLine($"Copied directory: {dirName}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error consolidating data files: {ex.Message}");
                throw;
            }
        }

        private static void RemoveNestedStructure(string directory)
        {
            try
            {
                var dirInfo = new DirectoryInfo(directory);
                
                // Remove nested SharedData and New folder directories
                var subDirs = dirInfo.GetDirectories();
                foreach (var subDir in subDirs)
                {
                    var dirName = Path.GetFileName(subDir.FullName);
                    if (dirName.Equals("SharedData", StringComparison.OrdinalIgnoreCase) || 
                        dirName.Equals("New folder", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"Removing nested directory: {subDir.FullName}");
                        subDir.Delete(true);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing nested structure: {ex.Message}");
                throw;
            }
        }

        private static void MoveConsolidatedData(string sourceDir, string targetDir)
        {
            try
            {
                var sourceDirInfo = new DirectoryInfo(sourceDir);
                
                // Move files
                foreach (var file in sourceDirInfo.GetFiles())
                {
                    var targetFile = Path.Combine(targetDir, file.Name);
                    file.MoveTo(targetFile);
                    Console.WriteLine($"Moved file: {file.Name}");
                }

                // Move directories
                foreach (var subDir in sourceDirInfo.GetDirectories())
                {
                    var targetSubDir = Path.Combine(targetDir, subDir.Name);
                    if (Directory.Exists(targetSubDir))
                    {
                        // Merge directories
                        MergeDirectories(subDir.FullName, targetSubDir);
                        subDir.Delete(true);
                    }
                    else
                    {
                        subDir.MoveTo(targetSubDir);
                    }
                    Console.WriteLine($"Moved directory: {subDir.Name}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error moving consolidated data: {ex.Message}");
                throw;
            }
        }

        private static void MergeDirectories(string sourceDir, string targetDir)
        {
            try
            {
                var sourceDirInfo = new DirectoryInfo(sourceDir);
                
                // Copy files
                foreach (var file in sourceDirInfo.GetFiles())
                {
                    var targetFile = Path.Combine(targetDir, file.Name);
                    file.CopyTo(targetFile, true);
                }

                // Recursively merge subdirectories
                foreach (var subDir in sourceDirInfo.GetDirectories())
                {
                    var targetSubDir = Path.Combine(targetDir, subDir.Name);
                    if (!Directory.Exists(targetSubDir))
                    {
                        Directory.CreateDirectory(targetSubDir);
                    }
                    MergeDirectories(subDir.FullName, targetSubDir);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error merging directories: {ex.Message}");
                throw;
            }
        }
    }
}
