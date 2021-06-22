using System;
using System.IO;
using System.IO.Compression;

namespace TeamsMigrate.Utils
{
    public class Files
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(Files));
        public static string DecompressSlackArchiveFile(string zipFilePath, string tempPath)
        {
            log.Debug("Decompress "+ zipFilePath);

            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            if (Directory.Exists(tempPath))
            {
                Directory.Delete(tempPath, true);
                log.Debug("Deleting pre-existing temp directory");
            }

            Directory.CreateDirectory(tempPath);
            log.Debug("Creating temp directory for Slack archive decompression");
            log.Debug("Temp path is " + tempPath);
            ZipFile.ExtractToDirectory(zipFilePath, tempPath);
            log.Debug("Slack archive decompression done");

            return tempPath;
        }

        public static void CleanUpTempDirectoriesAndFiles(string tempPath)
        {
            if (Program.SkipCleanup) return;
            log.Info("Cleaning up Slack archive temp directories and files");
            Directory.Delete(tempPath, true);
            File.Delete(tempPath);
            log.Debug("Deleted " + tempPath + " and subdirectories");
        }
    }
}
