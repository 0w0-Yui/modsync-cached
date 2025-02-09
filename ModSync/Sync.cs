using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SPT.Common.Utils;
using SPT.Custom.Utils;

namespace ModSync
{
    public static class Sync
    {
        public static List<string> GetAddedFiles(Dictionary<string, ModFile> localModFiles, Dictionary<string, ModFile> remoteModFiles)
        {
            return remoteModFiles.Keys.Except(localModFiles.Keys.Where((file) => !localModFiles[file].nosync), StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static List<string> GetUpdatedFiles(
            Dictionary<string, ModFile> localModFiles,
            Dictionary<string, ModFile> remoteModFiles,
            Dictionary<string, ModFile> previousRemoteModFiles
        )
        {
            return remoteModFiles
                .Keys.Intersect(localModFiles.Keys, StringComparer.OrdinalIgnoreCase)
                .Where((key) => !localModFiles[key].nosync)
                .Where(
                    (key) =>
                        (!previousRemoteModFiles.ContainsKey(key) || remoteModFiles[key].crc != previousRemoteModFiles[key].crc)
                        && remoteModFiles[key].crc != localModFiles[key].crc
                )
                .ToList();
        }

        public static List<string> GetRemovedFiles(
            Dictionary<string, ModFile> localModFiles,
            Dictionary<string, ModFile> remoteModFiles,
            Dictionary<string, ModFile> previousRemoteModFiles
        )
        {
            return previousRemoteModFiles
                .Keys.Intersect(localModFiles.Keys, StringComparer.OrdinalIgnoreCase)
                .Except(remoteModFiles.Keys, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static Dictionary<string, ModFile> HashLocalFiles(string basePath, List<string> syncPaths, List<string> enabledSyncPaths)
        {
            return syncPaths
                .Where((syncPath) => VFS.Exists(Path.Combine(basePath, syncPath)))
                .Select((subDir) => Path.Combine(basePath, subDir))
                .SelectMany(
                    (path) =>
                        Utility
                            .GetFilesInDir(path)
                            .AsParallel()
                            .Where((file) => file != @"BepInEx\patchers\Corter-ModSync-Patcher.dll")
                            .Where((file) => !file.EndsWith(".nosync") && !file.EndsWith(".nosync.txt"))
                            .Select((file) => CreateModFile(basePath, file, enabledSyncPaths.Contains(path.Remove(0, basePath.Length + 1))))
                )
                .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
        }

        public static KeyValuePair<string, ModFile> CreateModFile(string basePath, string file, bool enabled)
        {
            var data = VFS.ReadFile(file);
            var relativePath = file.Replace($"{basePath}\\", "");

            return new KeyValuePair<string, ModFile>(relativePath, new ModFile(Crc32.Compute(data), !enabled || Utility.NoSyncInTree(basePath, relativePath)));
        }

        public static void CompareModFiles(
            Dictionary<string, ModFile> localModFiles,
            Dictionary<string, ModFile> remoteModFiles,
            Dictionary<string, ModFile> previousSync,
            out List<string> addedFiles,
            out List<string> updatedFiles,
            out List<string> removedFiles
        )
        {
            addedFiles = GetAddedFiles(localModFiles, remoteModFiles);
            updatedFiles = GetUpdatedFiles(localModFiles, remoteModFiles, previousSync);
            removedFiles = GetRemovedFiles(localModFiles, remoteModFiles, previousSync);
        }
    }
}
