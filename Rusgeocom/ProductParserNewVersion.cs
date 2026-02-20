using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace Rusgeocom.ParserLib
{
    public class ProductParserNewVersion : ProductParserBase
    {
        private readonly string SPB_HOST = "https://spb.rusgeocom.ru";
        private readonly Action<string> logger;

        public ProductParserNewVersion(Action<string> logger)
        {
            this.logger = logger;
        }

        public async Task<List<Product>> Parse(IEnumerable<string> skuToFind, IProgress<double> indicator)
        {
            var list = new List<Product>();


            int total = skuToFind.Count();
            int current = 0;

            foreach (var sku in skuToFind)
            {
                var product = new Product()
                {
                    SearchSku = sku
                };

                await SearchProduct(product);

                if (product != null && product.Uri != null && list.FirstOrDefault(p => p.Uri == product.Uri) == null)
                {
                    await ParseProductDetails(product);

                    if (product.IsParsed)
                    {
                        logger?.Invoke("Товар добавлен в список");
                    }
                }

                if (!product.IsParsed)
                {
                    product.Name = "Неизвестно";
                    product.Manufacturer = "Неизвестно";
                }

                list.Add(product);

                indicator?.Report((double)++current / total);
            }

            return list;
        }

        public async Task<List<Product>> ParseUrls(IEnumerable<string> urls, IProgress<double> indicator)
        {
            var products = urls.Select(url => new Product() { Uri = url }).ToList();
            int total = products.Count;
            int current = 0;

            foreach (Product product in products)
            {
                await ParseProductDetails(product, ignoreSearchSkuCondition: true);

                indicator?.Report((double)++current / total);
            }

            return products;
        }

        public async Task<List<Product>> ParseBrand(string brandUri)
        {
            var doc = await GetDocument(brandUri);

            List<Product> list = new List<Product>();

            bool hasPages = false;
            int currentPage = 1;
            do
            {
                if (currentPage > 1)
                {
                    doc = await GetDocument(brandUri.TrimEnd('/') + $"?PAGEN_1={currentPage}");
                }
                var brandProducts = doc.DocumentNode
                    .SelectNodes("//div[@class='goods__list']/div//h3/a")
                    .Select(a => new Product()
                    {
                        Uri = SPB_HOST + a.GetAttributeValue("href", null)
                    })
                    .ToArray();

                list.AddRange(brandProducts);
                currentPage++;
                hasPages = doc.DocumentNode.SelectSingleNode("//ul[@class='pagination__list']/li[last()]").HasClass("active") == false;

            } while (hasPages);


            foreach (var product in list)
            {
                await ParseProductDetails(product);
            }

            return list;
        }

        private async Task<HtmlDocument> SearchProduct(Product product)
        {
            var uri = $"{SPB_HOST}/search?search_string={HttpUtility.UrlEncode(product.SearchSku)}";
            var doc = await GetDocument(uri);

            //product.Uri = GetCurrentUri();
            throw new NotImplementedException();

            //return doc;
        }

        private async Task ParseProductDetails(Product product, bool ignoreSearchSkuCondition = false, string rawHtml = "")
        {
            HtmlDocument doc = null;
            if (product.Uri != null)
            {
                doc = await GetDocument(product.Uri);
            }
            else
            {
                doc = new HtmlDocument();
                doc.LoadHtml(rawHtml);
            }

            if (doc?.DocumentNode == null)
            {
                return;
            }

            try
            {
                product.Name = doc.DocumentNode.SelectSingleNode("//h1")?.InnerText.TrimHtml();
                product.Manufacturer = doc.DocumentNode.SelectSingleNode("//div[@itemprop='brand']/meta")?.GetAttributeValue("content", null);
                product.Code = doc.DocumentNode.SelectSingleNode("//div[@class='product-code__item']")?.InnerText.Replace("Код товара", string.Empty).TrimHtml();
                product.Sku = doc.DocumentNode.SelectSingleNode("//span[@class='product-header__bottom-text' and contains(text(), 'Артикул ')]")?.InnerText.Replace("Артикул ", string.Empty).Trim();
                product.Gosreestr = ParseMainCharacteristic("Госреестр", doc);

                product.DescriptionMarkup = doc.DocumentNode
                    .SelectSingleNode("//div[@class='description-text__content']/div")?.InnerHtml.TrimHtml();
                product.DescriptionMarkup = Regex.Replace(product.DescriptionMarkup, @"(\s+)?Купить (.*?), а также .* онлайн-консультанта.", "").TrimHtml();
                product.DescriptionMarkup = $"<p>{product.DescriptionMarkup}</p>";

                if (!ignoreSearchSkuCondition)
                {
                    var conditionA = product.Sku.Equals(product.SearchSku, StringComparison.OrdinalIgnoreCase);
                    var conditionB = product.Code.Equals(product.SearchSku, StringComparison.OrdinalIgnoreCase);

                    if (!conditionA && !conditionB)
                    {
                        logger?.Invoke($"Несовпадение искомого SKU: [{product.SearchSku}] и SKU товара [{product.Sku}]");
                        return;
                    }
                }

                //Breadcrumbs
                if (doc.DocumentNode.SelectSingleNode("//a[@class='breadcrumbs__item-link']/meta[@itemprop='name']") != null)
                {
                    var breadcrumbs = doc.DocumentNode
                        .SelectNodes("//a[@class='breadcrumbs__item-link']/meta[@itemprop='name']")
                        .Select(meta => meta.GetAttributeValue("content", null))
                        .ToArray();

                    product.Breadcrumbs.AddRange(breadcrumbs);
                }

                //Характеристики
                ParseCharacteristics(product, doc);

                //Изображения
                if (doc.DocumentNode.SelectSingleNode("//div[@class='product-images__thumbs-list']//img") != null)
                {
                    var images = doc.DocumentNode
                        .SelectNodes("//div[@class='product-images__thumbs-list']//img")
                        .Select(img => GetImageSource(img.GetAttributeValue("src", null).Replace("small.web", "big.web")))
                        .Distinct()
                        .ToList();

                    product.Images.AddRange(images);
                }

                //Комплектация
                ParseComplectation(product, doc);

                //Инструкции
                ParseInstruction(product, doc);

                //Аксессуары
                //

                //Зонды
                //await ParseProductProbes(product, doc);

                Debug.Write($"Товар успешно запаршен {product.Name}");
                product.IsParsed = true;
            }
            catch
            {
                product.IsParsed = false;
            }
        }

        private void ParseInstruction(Product product, HtmlDocument doc)
        {
            try
            {
                string htmlNode = Regex.Match(doc.DocumentNode.InnerHtml, @",files:\[(.*?)\]")?.Groups[1].Value;
                string decodedUnicode = Regex.Unescape(htmlNode);
                var matches = Regex.Matches(decodedUnicode, "name:(.*?),url:\"(.*?)\"");
                foreach (Match match in matches)
                {
                    if (!match.Success)
                    {
                        continue;
                    }

                    string name = match.Groups[1]?.Value?.TrimHtml();
                    string uri = SPB_HOST + match.Groups[2]?.Value?.TrimHtml();

                    if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(uri) && uri.ToLower().Contains(".pdf"))
                    {
                        product.Instructions.Add(new Pdf()
                        {
                            Name = name.Replace("c$", "Руководство по эксплуатации").Trim('"'),
                            Uri = uri
                        });
                    }
                }

                if (product.Instructions.Any(pdf => pdf.Name.Contains("$")))
                {

                }
            }
            catch
            {
                logger?.Invoke($"Ошибка PDF файлов у товара по URL: {product.Uri}");
            }
        }

        private void ParseComplectation(Product product, HtmlDocument doc)
        {
            var eqip = doc.DocumentNode.SelectSingleNode("//ul[@class='equipment__list']");
            if (eqip != null)
            {
                var liItems = eqip.SelectNodes(".//span[@class='equipment__item-name']").Select(li => li.InnerText.TrimHtml()).ToArray();
                product.ComplectationItems.AddRange(liItems);
            }

        }

        private async Task ParseCharacteristics(Product product, HtmlDocument doc)
        {
            try
            {
                string htmlNode = Regex.Match(doc.DocumentNode.InnerHtml, ",characteristics:\"(.*?)\",shortCharacteristics")?.Groups[1].Value;
                string decodedUnicode = Regex.Unescape(htmlNode);
                HtmlNode techTableNode = HtmlNode.CreateNode(decodedUnicode);

                if (techTableNode != null)
                {
                    var charRow = techTableNode
                        .SelectNodes("//tbody//tr")
                        .ToList();

                    var groupName = "";
                    foreach (var row in charRow)
                    {
                        if (row.HasClass("tech_item"))
                        {
                            groupName = row?.InnerText.TrimHtml();
                        }
                        else
                        {
                            var ch = new Characteristic()
                            {
                                Group = groupName,
                                Name = row.SelectSingleNode("./th")?.InnerText.TrimHtml(),
                                Value = row.SelectSingleNode("./td")?.InnerText.TrimHtml(),
                            };
                            if (!string.IsNullOrWhiteSpace(ch.Name) && !string.IsNullOrWhiteSpace(ch.Value))
                            {
                                product.Characteristics.Add(ch);
                            }
                        }
                    }
                }

            }
            catch
            {
                logger?.Invoke($"Ошибка загрузки характеристик у товара по URL: {product.Uri}");
            }

            if (product.Characteristics == null || product.Characteristics.Count == 0)
            {
                ParseCharacteristicsFromRawHtml(product, doc);
            }
        }

        private void ParseCharacteristicsFromRawHtml(Product product, HtmlDocument doc)
        {
            var charTable = doc.DocumentNode.SelectSingleNode("//div[@class='characteristics-popup__content']/table[@class='tech']");
            bool shortVersion = false;
            if (charTable == null)
            {
                charTable = doc.DocumentNode.SelectSingleNode("//div[@id='characteristics']//table");
                shortVersion = true;
            }
            if (charTable == null)
            {
                return;
            }


            string currentGroup = "Основные характеристики";
            var source = charTable.SelectNodes(".//tr").Skip(shortVersion ? 0 : 1).ToArray();

            foreach (var tr in source)
            {
                if (tr.HasClass("tech_item"))
                {
                    currentGroup = tr.SelectSingleNode("./th").InnerText.TrimHtml();
                    continue;
                }
                var ch = new Characteristic()
                {
                    Name = tr.SelectSingleNode("./th")?.InnerText.TrimHtml(),
                    Value = tr.SelectSingleNode("./td")?.InnerText.TrimHtml(),
                    Group = currentGroup
                };
                product.Characteristics.Add(ch);
            }
        }

        private string ParseMainCharacteristic(string characteristicTitle, HtmlDocument doc)
        {
            string xPath = $"//div[@class='product-info__characteristics']//span[contains(text(), '{characteristicTitle}')]/../../div[2]";
            var node = doc.DocumentNode.SelectSingleNode(xPath);
            if (node != null)
            {
                return node.InnerText.TrimHtml();
            }
            return null;
        }

        private string GetImageSource(string uri)
        {
            if (uri.Contains("resize_cache"))
            {
                var temp = uri;

                temp = temp.Replace("resize_cache/", string.Empty);

                var uriParts = temp.Split('/').ToList();

                uriParts.RemoveAt(uriParts.Count - 2);

                string image = SPB_HOST + string.Join("/", uriParts);

                return image;
            }
            return SPB_HOST + uri;
        }

        internal async Task<Product> ParseSingleFromHtml(string rawHtml)
        {
            var product = new Product();
            await ParseProductDetails(product, ignoreSearchSkuCondition: true, rawHtml);
            return product;
        }
    }
}

