using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace LightSide
{
    /// <summary>
    /// Immutable private copies of consumer-managed font files, keyed by source path, length and timestamp and shared
    /// across processes: a source overwritten in place while it is loaded never changes under a live mapping, and it is
    /// held open only for the copy. Fonts the operating system resolves are mapped in place through
    /// <see cref="FileFontSource.OpenFile"/> instead.
    /// </summary>
    internal static class FontFileCache
    {
        private static readonly string root = Path.Combine(
            Path.GetTempPath(), "UniText", "FontCache", "snapshots");

        internal static FileFontSource OpenSnapshot(string sourcePath)
        {
            sourcePath = Path.GetFullPath(sourcePath);
            var sourceInfo = new FileInfo(sourcePath);
            if (!sourceInfo.Exists)
                throw new FileNotFoundException("Font file does not exist.", sourcePath);
            var expectedLength = sourceInfo.Length;
            if (expectedLength <= 0 || expectedLength > int.MaxValue)
                throw new InvalidDataException($"Font file '{sourcePath}' has an unsupported length.");
            var expectedTimestamp = sourceInfo.LastWriteTimeUtc;
            var name = SnapshotName(sourcePath, expectedLength, expectedTimestamp);
            var snapshotPath = Path.Combine(root, name + ".sfnt");
            if (!FileHasLength(snapshotPath, expectedLength))
                Publish(sourceInfo, expectedLength, expectedTimestamp, snapshotPath, name);
            return FileFontSource.OpenFile(snapshotPath);
        }

        private static string SnapshotName(string path, long length, DateTime timestamp)
        {
            using var sha256 = SHA256.Create();
            return FontSourceId.ToHex(sha256.ComputeHash(
                Encoding.UTF8.GetBytes($"{path}|{length:X8}|{timestamp.Ticks:X16}")));
        }

        private static void Publish(FileInfo sourceInfo, long expectedLength, DateTime expectedTimestamp,
            string snapshotPath, string name)
        {
            Directory.CreateDirectory(root);
            var temporaryPath = Path.Combine(root, $"{Guid.NewGuid():N}.tmp");
            try
            {
                using (var input = new FileStream(sourceInfo.FullName, FileMode.Open,
                           FileAccess.Read, FileShare.Read, 65536, FileOptions.SequentialScan))
                using (var output = new FileStream(temporaryPath, FileMode.CreateNew,
                           FileAccess.Write, FileShare.None, 65536, FileOptions.SequentialScan))
                {
                    input.CopyTo(output);
                    output.Flush(true);
                    if (output.Length != expectedLength)
                        throw new IOException($"Font file '{sourceInfo.FullName}' changed while it was copied.");
                }

                sourceInfo.Refresh();
                if (!sourceInfo.Exists || sourceInfo.Length != expectedLength
                                       || sourceInfo.LastWriteTimeUtc != expectedTimestamp)
                    throw new IOException($"Font file '{sourceInfo.FullName}' changed while it was copied.");

                PublishTemporaryFile(temporaryPath, snapshotPath, expectedLength, name);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        /// <summary>Moves a fully written temporary file into place; an existing final file of the expected length is kept, any other is replaced.</summary>
        internal static void PublishTemporaryFile(string temporaryPath, string finalPath,
            long expectedLength, string sourceId)
        {
            if (FileHasLength(finalPath, expectedLength)) return;
            if (File.Exists(finalPath))
            {
                try { File.Replace(temporaryPath, finalPath, null); }
                catch (IOException) when (FileHasLength(finalPath, expectedLength)) { return; }
            }
            else
            {
                try { File.Move(temporaryPath, finalPath); }
                catch (IOException) when (FileHasLength(finalPath, expectedLength)) { return; }
            }
            if (!FileHasLength(finalPath, expectedLength))
                throw new InvalidDataException(
                    $"UniText font cache entry '{sourceId}' was not published completely.");
        }

        internal static bool FileHasLength(string path, long expectedLength)
            => File.Exists(path) && new FileInfo(path).Length == expectedLength;
    }
}
