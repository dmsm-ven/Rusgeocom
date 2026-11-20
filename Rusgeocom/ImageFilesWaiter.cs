using System.IO;

namespace Rusgeocom;

/// <summary>
/// Ждёт, пока в папке появятся все ожидаемые файлы и закончится их запись/конвертация.
/// Файлы считаются готовыми, если набор, размеры и время изменения не менялись в течение stableDelay
/// и файлы не заняты другим процессом (например, конвертером webp -> jpg).
/// </summary>
public static class ImageFilesWaiter
{
    /// <param name="folder">Папка, в которой появляются файлы.</param>
    /// <param name="baseNames">Имена файлов без расширения, например "AMO140_1".</param>
    /// <param name="extensions">Допустимые расширения (с точкой), например ".jpg".</param>
    /// <returns>true - все файлы готовы, false - время ожидания истекло.</returns>
    public static async Task<bool> WaitAsync(
        string folder,
        IReadOnlyList<string> baseNames,
        string[] extensions,
        TimeSpan stableDelay,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var started = DateTime.UtcNow;
        string? lastSignature = null;
        var stableSince = DateTime.UtcNow;

        while (DateTime.UtcNow - started < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var signature = GetSignature(folder, baseNames, extensions);

            if (signature == null)
            {
                lastSignature = null; // ещё не все файлы на месте
            }
            else if (signature != lastSignature)
            {
                lastSignature = signature;
                stableSince = DateTime.UtcNow;
            }
            else if (DateTime.UtcNow - stableSince >= stableDelay && AreUnlocked(folder, baseNames, extensions))
            {
                return true;
            }

            await Task.Delay(250, cancellationToken);
        }

        return false;
    }

    private static List<FileInfo>? FindFiles(string folder, IReadOnlyList<string> baseNames, string[] extensions)
    {
        var result = new List<FileInfo>();

        foreach (var name in baseNames)
        {
            var file = extensions
                .Select(ext => new FileInfo(Path.Combine(folder, name + ext)))
                .FirstOrDefault(f => f.Exists);

            if (file == null)
            {
                return null;
            }

            result.Add(file);
        }

        return result;
    }

    private static string? GetSignature(string folder, IReadOnlyList<string> baseNames, string[] extensions)
    {
        var files = FindFiles(folder, baseNames, extensions);
        return files == null
            ? null
            : string.Join("|", files.Select(f => $"{f.Name}:{f.Length}:{f.LastWriteTimeUtc.Ticks}"));
    }

    private static bool AreUnlocked(string folder, IReadOnlyList<string> baseNames, string[] extensions)
    {
        var files = FindFiles(folder, baseNames, extensions);
        if (files == null)
        {
            return false;
        }

        foreach (var file in files)
        {
            try
            {
                using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.None);
                if (stream.Length == 0)
                {
                    return false;
                }
            }
            catch (IOException)
            {
                return false;
            }
        }

        return true;
    }
}
