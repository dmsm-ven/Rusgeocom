using System.IO;

namespace Rusgeocom;

/// <summary>
/// Ждёт, пока в папке появятся все ожидаемые файлы и закончится их запись/конвертация.
/// Файлы считаются готовыми, если набор, размеры и время изменения не менялись в течение stableDelay
/// и файлы не открыты на запись другим процессом (например, конвертером webp -> jpg).
/// Чтение файла другими программами (просмотрщик, Проводник, антивирус) ожиданию не мешает.
/// </summary>
public static class ImageFilesWaiter
{
    private static readonly TimeSpan statusInterval = TimeSpan.FromSeconds(15);

    /// <param name="folder">Папка, в которой появляются файлы.</param>
    /// <param name="baseNames">Имена файлов без расширения, например "135054_1".</param>
    /// <param name="extensions">Допустимые расширения (с точкой), например ".jpg".</param>
    /// <param name="status">Сообщения о том, чего именно не хватает (раз в 15 секунд).</param>
    /// <returns>true - все файлы готовы, false - время ожидания истекло.</returns>
    public static async Task<bool> WaitAsync(
        string folder,
        IReadOnlyList<string> baseNames,
        string[] extensions,
        TimeSpan stableDelay,
        TimeSpan timeout,
        IProgress<string>? status = null,
        CancellationToken cancellationToken = default)
    {
        var started = DateTime.UtcNow;
        var lastStatus = DateTime.UtcNow;
        string? lastSignature = null;
        var stableSince = DateTime.UtcNow;
        string reason = "";

        while (DateTime.UtcNow - started < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var files = FindFiles(folder, baseNames, extensions, out var missing);

            if (files == null)
            {
                lastSignature = null;
                reason = $"не найдены файлы: {string.Join(", ", missing.Select(m => m + ExtensionsHint(extensions)))}. " +
                         $"В папке найдены: {ListExisting(folder, extensions)}";
            }
            else
            {
                var signature = string.Join("|", files.Select(f => $"{f.Name}:{f.Length}:{f.LastWriteTimeUtc.Ticks}"));

                if (signature != lastSignature)
                {
                    lastSignature = signature;
                    stableSince = DateTime.UtcNow;
                    reason = "файлы ещё изменяются";
                }
                else if (DateTime.UtcNow - stableSince >= stableDelay)
                {
                    var locked = files.Where(IsBusy).Select(f => f.Name).ToList();
                    if (locked.Count == 0)
                    {
                        return true;
                    }

                    reason = $"файлы заняты другим процессом (идёт запись/конвертация): {string.Join(", ", locked)}";
                }
            }

            if (status != null && DateTime.UtcNow - lastStatus >= statusInterval)
            {
                lastStatus = DateTime.UtcNow;
                status.Report($"Ожидание: {reason}");
            }

            await Task.Delay(250, cancellationToken);
        }

        status?.Report($"Ожидание прервано по таймауту: {reason}");
        return false;
    }

    private static List<FileInfo>? FindFiles(string folder, IReadOnlyList<string> baseNames, string[] extensions, out List<string> missing)
    {
        var result = new List<FileInfo>();
        missing = new List<string>();

        foreach (var name in baseNames)
        {
            var file = extensions
                .Select(ext => new FileInfo(Path.Combine(folder, name + ext)))
                .FirstOrDefault(f => f.Exists);

            if (file == null)
            {
                missing.Add(name);
            }
            else
            {
                result.Add(file);
            }
        }

        return missing.Count == 0 ? result : null;
    }

    private static string ExtensionsHint(string[] extensions) => extensions.Length == 1 ? extensions[0] : ".*";

    private static string ListExisting(string folder, string[] extensions)
    {
        var names = extensions
            .SelectMany(ext => Directory.GetFiles(folder, "*" + ext))
            .Select(Path.GetFileName)
            .Distinct()
            .Take(10)
            .ToList();

        return names.Count == 0 ? "нет подходящих файлов" : string.Join(", ", names);
    }

    // файл занят, если его нельзя открыть на чтение при условии, что другие процессы могут только читать
    // (запись/конвертация держит файл открытым на запись)
    private static bool IsBusy(FileInfo file)
    {
        try
        {
            using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
            return stream.Length == 0;
        }
        catch (IOException)
        {
            return true;
        }
    }
}
