using HtmlAgilityPack;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;

namespace Rusgeocom;

/// <summary>
/// Разбор HTML страницы товара rusgeocom.ru (скопированной из браузера): описание, характеристики,
/// комплектация, габариты и изображения. Интерфейс аналогичен ViParser из VseInstrumentiParser.
/// </summary>
public class RusgeocomParser
{
    private const string SITE_HOST = "https://rusgeocom.ru";

    private readonly HttpClient client;
    private readonly string downloadFolder;

    public string? LastParseDescriptionHtml { get; private set; }
    public DimensionsData? LastDimensionsData { get; private set; }
    public string[]? LastImagesData { get; private set; }
    public string? LastModelData { get; private set; }

    public RusgeocomParser(string downloadFolder)
    {
        client = new HttpClient(new HttpClientHandler()
        {
            AllowAutoRedirect = true,
            CookieContainer = new CookieContainer(),
            UseCookies = true
        });
        client.Timeout = TimeSpan.FromSeconds(10);
        client.DefaultRequestHeaders.Referrer = new Uri("https://www.rusgeocom.ru/");
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/157.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9,ru;q=0.8");
        client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };
        client.DefaultRequestHeaders.Pragma.Add(new NameValueHeaderValue("no-cache"));
        this.downloadFolder = downloadFolder;
    }

    internal async Task<string> ParseFromHtml(string html,
        string? model = null,
        IProgress<string>? progress = null)
    {
        ClearLastParsedData();

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var title = doc.DocumentNode.SelectSingleNode("//h1")?.InnerText.TrimHtml();
        if (string.IsNullOrEmpty(title))
        {
            throw new FormatException("Не найден заголовок товара (h1): скопирована не страница товара.");
        }

        // модель по умолчанию - артикул товара, если его нет - последнее слово заголовка
        if (string.IsNullOrWhiteSpace(model))
        {
            model = doc.DocumentNode
                .SelectSingleNode("//span[@class='product-header__bottom-text' and contains(text(), 'Артикул ')]")?
                .InnerText.Replace("Артикул ", string.Empty).Trim();
        }
        if (string.IsNullOrWhiteSpace(model))
        {
            model = title.Split(' ').Last().Trim();
        }

        if (string.IsNullOrWhiteSpace(model) || model.Length < 3)
        {
            throw new FormatException("Model name is invalid or too short.");
        }

        LastModelData = model;

        var images = ParseImages(doc);
        LastImagesData = new string[images.Length];

        await DownloadImages(model, images).ConfigureAwait(false);

        progress?.Report($"Загружены изображения: {images.Length}");

        return BuildDescription(doc);
    }

    private static string[] ParseImages(HtmlDocument doc)
    {
        var nodes = doc.DocumentNode.SelectNodes("//div[@class='product-images__thumbs-list']//img");
        if (nodes == null)
        {
            return [];
        }

        return nodes
            .Select(img => img.GetAttributeValue("src", ""))
            .Where(src => !string.IsNullOrEmpty(src) && !src.StartsWith("data"))
            .Select(src => GetImageSource(src.Replace("small.web", "big.web")))
            .Distinct()
            .ToArray();
    }

    private static string GetImageSource(string uri)
    {
        if (uri.Contains("resize_cache"))
        {
            var uriParts = uri.Replace("resize_cache/", string.Empty).Split('/').ToList();
            uriParts.RemoveAt(uriParts.Count - 2);
            return SITE_HOST + string.Join("/", uriParts);
        }

        return SITE_HOST + uri;
    }

    private async Task DownloadImages(string model, string[] images)
    {
        string pathModel = model.Replace("/", "_").Replace("\\", "_");

        var imageCount = images.Length;
        var maxDegreeOfParallelism = Math.Min(8, Math.Max(2, Environment.ProcessorCount));
        var semaphore = new SemaphoreSlim(maxDegreeOfParallelism);
        var downloadTasks = new List<Task>(imageCount);

        for (int i = 0; i < imageCount; i++)
        {
            var index = i;
            var imageUrl = images[index];

            await semaphore.WaitAsync().ConfigureAwait(false);

            downloadTasks.Add(Task.Run(async () =>
            {
                try
                {
                    var bytes = await client.GetByteArrayAsync(imageUrl).ConfigureAwait(false);
                    var fileName = Path.Combine(downloadFolder, $"{pathModel}_{index + 1}{Path.GetExtension(imageUrl)}");
                    await File.WriteAllBytesAsync(fileName, bytes).ConfigureAwait(false);
                    LastImagesData![index] = Path.GetFileNameWithoutExtension(fileName) + ".jpg";
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        await Task.WhenAll(downloadTasks).ConfigureAwait(false);
    }

    private void ClearLastParsedData()
    {
        LastDimensionsData = null;
        LastImagesData = null;
        LastModelData = null;
        LastParseDescriptionHtml = null;
    }

    private string BuildDescription(HtmlDocument doc)
    {
        string shortDescription = ParseShortDescription(doc);
        var complectation = ParseComplectation(doc);
        var characteristics = ParseCharacteristics(doc);

        LastDimensionsData = ParseDimensions(characteristics);

        var sb = new StringBuilder();

        if (shortDescription.Length > 16)
        {
            sb.Append(shortDescription);
        }

        if (complectation.Count > 0)
        {
            sb.AppendLine("<h2>Комплектация</h2>");
            sb.AppendLine("<ol>");
            complectation.ForEach(i => sb.AppendLine($"<li>{i}</li>"));
            sb.AppendLine("</ol>");
        }

        if (characteristics.Count > 0)
        {
            sb
                .AppendLine("<h2>Технические характеристики</h2>")
                .AppendLine("<div class=\"table-responsive\">")
                .AppendLine("<table class=\"table\">")
                .AppendLine("<tbody>");

            foreach (var group in characteristics.GroupBy(c => c.Group))
            {
                sb.AppendLine($"<tr><th colspan=\"2\"><strong>{group.Key}</strong></th></tr>");
                foreach (var c in group)
                {
                    sb.AppendLine($"<tr><td>{c.Name}</td><td>{c.Value}</td></tr>");
                }
            }

            sb
                .AppendLine("</tbody>")
                .AppendLine("</table>")
                .AppendLine("</div>");
        }

        LastParseDescriptionHtml = sb.ToString()
            .Replace("<p><p>", "<p>")
            .Replace("<tr><th colspan=\"2\"><strong></strong></th></tr>", string.Empty)
            .Replace("\r", string.Empty)
            .TrimHtml();

        return LastParseDescriptionHtml;
    }

    private static string ParseShortDescription(HtmlDocument doc)
    {
        var markup = doc.DocumentNode
            .SelectSingleNode("//div[@class='description-text__content']/div")?.InnerHtml.TrimHtml();
        if (string.IsNullOrEmpty(markup))
        {
            return string.Empty;
        }

        markup = Regex.Replace(markup, @"(\s+)?Купить (.*?), а также .* онлайн-консультанта.", "").TrimHtml();

        var descriptionDoc = new HtmlDocument();
        descriptionDoc.LoadHtml($"<p>{markup}</p>");

        descriptionDoc.DocumentNode.SelectNodes("//img")?.ToList().ForEach(img => img.Remove());
        descriptionDoc.DocumentNode.SelectSingleNode("//p[contains(text(), 'Купить')]")?.Remove();

        // ссылки в описании заменяются выделенным текстом
        return Regex.Replace(descriptionDoc.DocumentNode.InnerHtml, "<a(.*)>(.*?)</a>", "<strong>$2</strong>");
    }

    private static List<string> ParseComplectation(HtmlDocument doc)
    {
        var items = doc.DocumentNode
            .SelectSingleNode("//ul[@class='equipment__list']")?
            .SelectNodes(".//span[@class='equipment__item-name']");

        return items?.Select(li => li.InnerText.TrimHtml()).ToList() ?? [];
    }

    private static List<Characteristic> ParseCharacteristics(HtmlDocument doc)
    {
        // краткая таблица на странице товара
        var charTable = doc.DocumentNode.SelectSingleNode("//div[@id='characteristics']/div/table");
        if (charTable != null)
        {
            var chars = charTable.SelectNodes("//tr")?
                .Select(tr => new Characteristic(
                    "Основные характеристики",
                    tr.SelectSingleNode("./th[1]")?.InnerText.TrimHtml() ?? "",
                    tr.SelectSingleNode("./td[1]")?.InnerText.TrimHtml() ?? ""))
                .Where(c => !string.IsNullOrWhiteSpace(c.Name) && !string.IsNullOrWhiteSpace(c.Value))
                .ToList();

            if (chars is { Count: > 0 })
            {
                return chars;
            }
        }

        return ParseCharacteristicsFromPopup(doc);
    }

    // полная таблица характеристик с группами (если краткой нет)
    private static List<Characteristic> ParseCharacteristicsFromPopup(HtmlDocument doc)
    {
        var charTable = doc.DocumentNode.SelectSingleNode("//div[@class='characteristics-popup__content']/table[@class='tech']");
        bool shortVersion = false;
        if (charTable == null)
        {
            charTable = doc.DocumentNode.SelectSingleNode("//div[@id='characteristics']//table");
            shortVersion = true;
        }

        var rows = charTable?.SelectNodes(".//tr");
        if (rows == null)
        {
            return [];
        }

        var result = new List<Characteristic>();
        string currentGroup = "Основные характеристики";

        foreach (var tr in rows.Skip(shortVersion ? 0 : 1))
        {
            if (tr.HasClass("tech_item"))
            {
                currentGroup = tr.SelectSingleNode("./th")?.InnerText.TrimHtml() ?? currentGroup;
                continue;
            }

            result.Add(new Characteristic(
                currentGroup,
                tr.SelectSingleNode("./th")?.InnerText.TrimHtml() ?? "",
                tr.SelectSingleNode("./td")?.InnerText.TrimHtml() ?? ""));
        }

        return result;
    }

    // габариты и вес берутся из характеристик ("Размеры"/"Габариты", "Вес"/"Масса")
    private static DimensionsData ParseDimensions(List<Characteristic> characteristics)
    {
        var dimensionsValue = characteristics.FirstOrDefault(c => c.Name is "Размеры" or "Габариты")?.Value ?? "";
        var weightValue = characteristics.FirstOrDefault(c => c.Name is "Вес" or "Масса")?.Value ?? "";

        var dimMatch = Regex.Match(dimensionsValue, @"(\d+) ?[xхXХ] ?(\d+) ?[xхXХ] ?(\d+) ?мм");
        var weightMatch = Regex.Match(weightValue, @"(\d+([\.,]\d+)?) ?кг");

        decimal.TryParse(weightMatch.Groups[1].Value.Replace(",", "."),
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var weight);

        return new DimensionsData
        {
            LengthMm = dimMatch.Success ? int.Parse(dimMatch.Groups[1].Value) : 0,
            WidthMm = dimMatch.Success ? int.Parse(dimMatch.Groups[2].Value) : 0,
            HeightMm = dimMatch.Success ? int.Parse(dimMatch.Groups[3].Value) : 0,
            WeightKg = weight
        };
    }

    private record Characteristic(string Group, string Name, string Value);
}

internal static class StringExtensions
{
    public static string TrimHtml(this string s) => s.Trim('\r', '\n', '\t', ' ');
}
