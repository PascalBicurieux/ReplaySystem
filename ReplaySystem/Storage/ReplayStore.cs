namespace ReplaySystem.Storage
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using Exiled.API.Features;

    public static class ReplayStore
    {
        public const int MaxAccessible = 5;

        public class Entry
        {
            public string FullPath;
            public string FileName;
            public DateTime LastWriteUtc;
            public long FileSize;
            public ReplayBundle Bundle;
        }

        public static List<Entry> ListAvailable()
        {
            var entries = new List<Entry>();
            string dir = ReplayPaths.ReplayDirectory;
            if (!Directory.Exists(dir)) return entries;

            var files = Directory.GetFiles(dir, "*.replay.gz")
                .Select(f => new FileInfo(f))
                .OrderByDescending(fi => fi.LastWriteTimeUtc)
                .Take(MaxAccessible);

            foreach (var fi in files)
            {
                try
                {
                    ReplayBundle bundle;
                    using (var fs = File.OpenRead(fi.FullName))
                        bundle = ReplaySerializer.Read(fs);

                    entries.Add(new Entry
                    {
                        FullPath = fi.FullName,
                        FileName = fi.Name,
                        LastWriteUtc = fi.LastWriteTimeUtc,
                        FileSize = fi.Length,
                        Bundle = bundle,
                    });
                }
                catch (Exception e)
                {
                    Log.Warn($"[ReplayStore] Skipped unreadable file {fi.Name}: {e.Message}");
                }
            }
            return entries;
        }

        public static ReplayBundle Load(string fullPath)
        {
            using (var fs = File.OpenRead(fullPath))
                return ReplaySerializer.Read(fs);
        }

        public static void Rotate()
        {
            string dir = ReplayPaths.ReplayDirectory;
            if (!Directory.Exists(dir)) return;

            var files = Directory.GetFiles(dir, "*.replay.gz")
                .Select(f => new FileInfo(f))
                .OrderByDescending(fi => fi.LastWriteTimeUtc)
                .ToList();

            if (files.Count <= MaxAccessible) return;

            var toArchive = files.Skip(MaxAccessible).ToList();
            string archivePath = ReplayPaths.ArchiveZip;

            try
            {
                using (var zipStream = File.Open(archivePath, FileMode.OpenOrCreate))
                using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Update))
                {
                    foreach (var fi in toArchive)
                    {
                        if (zip.GetEntry(fi.Name) != null)
                        {
                            Log.Warn($"[ReplayStore] {fi.Name} already in archive, skipping append.");
                            continue;
                        }
                        var entry = zip.CreateEntry(fi.Name, System.IO.Compression.CompressionLevel.NoCompression);
                        using (var es = entry.Open())
                        using (var src = File.OpenRead(fi.FullName))
                            src.CopyTo(es);
                    }
                }

                foreach (var fi in toArchive)
                {
                    try { File.Delete(fi.FullName); }
                    catch (Exception ex) { Log.Warn($"[ReplayStore] Cannot delete {fi.Name} after archiving: {ex.Message}"); }
                }

                Log.Info($"[ReplayStore] Archived {toArchive.Count} old replays → archive.zip");
            }
            catch (Exception e)
            {
                Log.Error($"[ReplayStore] Rotate failed: {e.GetBaseException().Message}");
            }
        }
    }
}
