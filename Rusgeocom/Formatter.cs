using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace Rusgeocom.ParserLib
{
    public class Formatter
    {
        private readonly List<Product> products;
        private readonly Lazy<int> START_ETK_ID;

        public Formatter(List<Product> products, Func<int> startIdResolver)
        {
            if (products != null)
            {
                this.products = products.OrderBy(p => p.Manufacturer).ThenBy(p => p.Model).ToList();
            }

            START_ETK_ID = new Lazy<int>(() => startIdResolver());
        }

        public string GetGeneralExport()
        {
            var sb = new StringBuilder();

            int id = START_ETK_ID.Value;
            foreach (var product in products)
            {
                string mainImage = GetMainImage(product);

                string caption = "";
                if (!string.IsNullOrWhiteSpace(product.Model) && !string.IsNullOrWhiteSpace(product.ProductType))
                {
                    caption = $"{product.Model} {product.Manufacturer} {product.ProductType}";
                }
                else
                {
                    caption = product.Name;
                    if (caption.IndexOf(product.Manufacturer, StringComparison.OrdinalIgnoreCase) == -1)
                    {
                        caption += $" {product.Manufacturer}";
                    }
                }
                string keyword = Regex.Replace(Transliteration.Front(caption, true), "-{2,}", "-");
                string meta_title = $"{caption} купить в Санкт-Петербурге";
                string meta_desc = $"{meta_title} с доставкой по России";

                sb
                    .AppendTab(id.ToString())   //product_id
                    .AppendTab(product.Name)   //name(ru)
                    .AppendTab(string.Empty)   //categories
                    .AppendTab(product.Sku)   //sku
                    .AppendTab(string.Empty)   //upc
                    .AppendTab(string.Empty)   //ean
                    .AppendTab(string.Empty)   //jan
                    .AppendTab(string.Empty)   //isbn
                    .AppendTab(string.Empty)   //mpn
                    .AppendTab("0")   //location
                    .AppendTab("0")   //quantity
                    .AppendTab(product.Model ?? string.Empty)   //model
                    .AppendTab(product.Manufacturer)   //manufacturer
                    .AppendTab(mainImage)   //image_name
                    .AppendTab("yes")   //shipping
                    .AppendTab("0")   //price
                    .AppendTab("0")   //points
                    .AppendTab(string.Empty)   //date_added
                    .AppendTab(string.Empty)   //date_modified
                    .AppendTab(string.Empty)   //date_available
                    .AppendTab("0")   //weight
                    .AppendTab("kg")   //weight_unit
                    .AppendTab("0")    //length
                    .AppendTab("0")    //width
                    .AppendTab("0")    //height
                    .AppendTab("mm")   //length_unit
                    .AppendTab("true")   //status
                    .AppendTab("0")   //tax_class_id
                    .AppendTab(keyword)   //seo_keyword
                    .AppendTab(string.Empty)   //description(ru)
                    .AppendTab("0")   //category_show(ru)
                    .AppendTab("0")   //main_product(ru)
                    .AppendTab(meta_title)   //meta_title(ru)
                    .AppendTab(meta_desc)   //meta_description(ru)
                    .AppendTab(string.Empty)   //meta_keywords(ru)
                    .AppendTab("10")   //stock_status_id
                    .AppendTab("0,1,2,3,4,5,6,7,8")   //store_ids
                    .AppendTab("0:,1:,2:,3:,4:,5:,6:,7:,8:")   //layout
                    .AppendTab(string.Empty)   //related_ids
                    .AppendTab(string.Empty)   //adjacent_ids
                    .AppendTab(string.Empty)   //tags(ru)
                    .AppendTab("1")   //sort_order
                    .AppendTab("true")   //subtract
                    .AppendLine("1"); //minimum

                id++;
            }

            var result = sb.ToString();
            return result;
        }

        internal string GetCategoriesProducts()
        {
            var validCategories = new string[]
            {
                "Аксессуары",
                "Измерители параметров окружающей среды",
                "Лазерные уровни",
                "Дальномеры",
                "Нивелиры",
                "Теодолиты",
                "Тепловизоры",
            };

            var sb = new StringBuilder();

            sb.Append("INSERT INTO oc_product_to_category (product_id, category_id, main_category) VALUES");

            foreach (var product in products.Where(p => p.Manufacturer == "RGK"))
            {
                if (product.Category == "Дальномеры")
                {
                    product.Category = "Лазерные дальномеры";
                }

                if (validCategories.Contains(product.Category))
                {
                    string catName = product.Category + " RGK";
                    string product_id = $"(SELECT product_id FROM oc_product WHERE product_id >= 181000 AND sku = '{product.Sku}')";
                    string category_id = $"(SELECT category_id FROM oc_category_description WHERE name = '{catName}')";

                    sb.AppendLine($"({product_id}, {category_id}, 1),");
                }
            }

            var result = sb.ToString().Trim('\r', '\n', '\t', ',', ' ') + ";";
            return result;
        }

        internal string GetDescriptionAndEquipmentSql(Product product, bool htmlEscape = false)
        {
            string newDesc = BuildDescription(product);
            if (htmlEscape)
            {
                newDesc = HttpUtility.HtmlEncode(newDesc);
            }
            return newDesc;
        }

        internal string GetDescriptionAndEquipmentSql()
        {
            var sb = new StringBuilder();

            foreach (var product in products)
            {
                string newDesc = HttpUtility.HtmlEncode(BuildDescription(product));
                string pid = GetPidSelect(product);
                sb.Append("UPDATE IGNORE oc_product_description ")
                    .Append($"SET description = '{newDesc}' ")
                    .AppendLine($"WHERE product_id = {pid};");
            }

            return sb.ToString().Trim();
        }

        public string GetCategories()
        {
            var data = products
                .Where(p => p.Manufacturer != null && p.Category != null && p.Manufacturer == "RGK")
                .GroupBy(p => p.Category)
                .OrderByDescending(g => g.Count())
                .ToDictionary(g => g.Key, g => g.Count());

            string result = string.Join("\r\n", data.Select(kvp => $"{kvp.Key}\t{kvp.Value}"));
            return result;
        }

        public string GetGosreetr()
        {
            var sb = new StringBuilder();
            //attribute_id = 40561

            sb.AppendLine("INSERT INTO oc_product_attribute (product_id, attribute_id, language_id, text) VALUES");
            foreach (var product in products.Where(p => !string.IsNullOrWhiteSpace(p.Gosreestr)))
            {
                if (string.IsNullOrWhiteSpace(product.Sku)) { continue; }

                string product_id = $"(SELECT product_id FROM oc_product WHERE product_id >= {START_ETK_ID.Value} AND sku = '{product.Sku}')";

                sb.AppendLine($"({product_id}, 40561, 2, '{product.Gosreestr}'),");
            }

            var result = sb.ToString().TrimHtml().Trim(',') + ";";
            return result;
        }

        public string GetProbes()
        {
            var sb = new StringBuilder();

            string probesGroupId = "4";
            sb.AppendLine("INSERT INTO oc_product_related (product_id, related_id, related_group_id) VALUES");

            var source = products.Where(p => p.ProbeCodes.Any()).ToList();

            //foreach (var product in source)
            //{
            //    if (string.IsNullOrWhiteSpace(product.Sku)) { continue; }

            //    foreach (var aCode in product.ProbeCodes)
            //    {
            //        if (codeToSkuMap.ContainsKey(aCode))
            //        {
            //            string product_id = $"(SELECT product_id FROM oc_product WHERE product_id >= {START_ETK_ID} AND sku = '{product.Sku}')";
            //            string aSku = codeToSkuMap[aCode];
            //            string related_id = $"(SELECT product_id FROM oc_product WHERE product_id >= {START_ETK_ID} AND sku = '{aSku}')";
            //            sb.AppendLine($"({product_id}, {related_id}, {probesGroupId}),");
            //        }
            //    }
            //}

            var result = sb.ToString().Trim('\r', '\n', ',') + ";";
            return result;
        }

        public string GetAccessories()
        {
            var sb = new StringBuilder();

            string accessoriesGroupId = "5";
            sb.AppendLine("INSERT INTO oc_product_related (product_id, related_id, related_group_id) VALUES");

            var source = products.Where(p => p.AccessoriesCodes.Any()).ToList();

            //foreach (var product in source)
            //{
            //    if (string.IsNullOrWhiteSpace(product.Sku)) { continue; }

            //    foreach(var aCode in product.AccessoriesCodes)
            //    {
            //        if (codeToSkuMap.ContainsKey(aCode))
            //        {
            //            string product_id = $"(SELECT product_id FROM oc_product WHERE product_id >= {START_ETK_ID} AND sku = '{product.Sku}')";
            //            string aSku = codeToSkuMap[aCode];
            //            string related_id = $"(SELECT product_id FROM oc_product WHERE product_id >= {START_ETK_ID} AND sku = '{aSku}')";
            //            sb.AppendLine($"({product_id}, {related_id}, {accessoriesGroupId}),");
            //        }
            //    }
            //}

            var result = sb.ToString().Trim('\r', '\n', ',') + ";";
            return result;
        }

        public string GetModelRanges()
        {
            //var sb = new StringBuilder();

            var modelRangeData = products
                .Where(p => p.ModelRangeCodes.Any())
                .GroupBy(p => string.Join(",", p.ModelRangeCodes.Concat(new string[] { p.Code }).Distinct().OrderBy(c => c)))
                .ToDictionary(i => i.Key, i => string.Join("\t", i.Select(p => p.Name)));

            var result = string.Join("\r\n", modelRangeData.Select(kvp => $"{kvp.Key}\t{kvp.Value}"));
            return result;
        }

        public string GetAdditionalImagesExport()
        {
            var sb = new StringBuilder();

            int id = START_ETK_ID.Value;
            foreach (var product in products)
            {
                int sort_order = 1;
                foreach (var image in product.Images.Skip(1))
                {
                    string imagePath = $"catalog/{product.manufacturer_ftp_path}/products/{product.Sku}_{sort_order}.jpg";
                    sb.AppendLine($"{id}\t{imagePath}\t{sort_order}");
                    sort_order++;
                }

                id++;
            }

            var result = sb.ToString();
            return result;
        }

        private string GetMainImage(Product product)
        {
            var firstImage = product.Images.FirstOrDefault();
            if (firstImage != null)
            {
                return $"catalog/{product.manufacturer_ftp_path}/products/{product.Sku}_1.jpg";
            }
            else
            {
                return "catalog/placeholder.jpg";
            }
        }

        private string BuildDescription(Product product)
        {
            var sb = new StringBuilder();

            if (product.DescriptionMarkup != null && product.DescriptionMarkup.Length > 16)
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(product.DescriptionMarkup);

                if (doc.DocumentNode.SelectSingleNode("//img") != null)
                {
                    doc.DocumentNode.SelectNodes("//img").ToList().ForEach(img => img.Remove());
                }

                string promoText = "";
                if (doc.DocumentNode.SelectSingleNode("//p[contains(text(), 'Купить')]") != null)
                {
                    doc.DocumentNode.SelectSingleNode("//p[contains(text(), 'Купить')]").Remove();
                }

                var clearDescription = doc.DocumentNode.InnerHtml;
                clearDescription = Regex.Replace(clearDescription, "<a(.*)>(.*?)</a>", "<strong>$2</strong>");

                sb.Append(clearDescription);
            }

            if (product.ComplectationItems.Any())
            {
                sb.AppendLine("<h2>Комплектация</h2>");
                sb.AppendLine("<ol>");
                product.ComplectationItems.ForEach(i => sb.AppendLine($"<li>{i.TrimHtml()}</li>"));
                sb.AppendLine("</ol>");
            }

            if (product.Characteristics.Any())
            {
                sb
                    .AppendLine("<h2>Технические характеристики</h2>")
                    .AppendLine("<div class=\"table-responsive\">")
                    .AppendLine("<table class=\"table\">")
                    .AppendLine("<tbody>");

                var cGroups = product.Characteristics.GroupBy(c => c.Group).ToList();

                foreach (var g in cGroups)
                {
                    sb.AppendLine($"<tr><th colspan=\"2\"><strong>{g.Key}</strong></th></tr>");
                    foreach (var c in g)
                    {
                        sb.AppendLine($"<tr><td>{c.Name}</td><td>{c.Value}</td></tr>");
                    }
                }

                sb
                    .AppendLine("</tbody>")
                    .AppendLine("</table>")
                    .AppendLine("</div>");
            }

            var result = sb.ToString()
                .Replace("<p><p>", "<p>")
                .Replace("<tr><th colspan=\"2\"><strong></strong></th></tr>", string.Empty)
                .Replace("\r", string.Empty).TrimHtml();

            return result;
        }

        internal string GetPdfSql()
        {
            var sb = new StringBuilder();

            sb.AppendLine("INSERT INTO oc_product_pdf (product_id, name, path) VALUES");
            string yandexDiskRoot = "https://disk.yandex.ru/d/V1KNVJO3SY3ROw";

            foreach (var product in products.Where(p => p.Instructions.Any()))
            {
                string product_id = GetPidSelect(product);
                string manufacturerPath = product.manufacturer_ftp_path;

                foreach (var pdf in product.Instructions)
                {
                    string pdfPath = $"{yandexDiskRoot}/{manufacturerPath}/{pdf.Uri.CreateMD5() + Path.GetExtension(pdf.Uri)}";
                    sb.AppendLine($"({product_id}, '{pdf.Name}', '{pdfPath}'),");
                }
            }

            var result = sb.ToString().TrimHtml().Trim(',') + ";";
            return result;
        }

        private string GetPidSelect(Product product, bool partial = false)
        {
            if (partial)
            {
                return $"WHERE product_id >= {START_ETK_ID.Value} AND (sku = '{product.Sku}' OR model = '{product.Sku}') ";
            }
            return $"(SELECT product_id FROM oc_product WHERE product_id >= {START_ETK_ID.Value} AND (sku = '{product.Sku}' OR model = '{product.Sku}') LIMIT 1)";
        }

        internal string GetDimensionsSql()
        {
            var sb = new StringBuilder();
            foreach (var product in products)
            {
                if (product.Dimensions.NotEmpty)
                {
                    sb.Append("UPDATE oc_product SET ");
                    sb.Append($"length = {product.Dimensions.Length.ToString().Replace(",", ".")}, ");
                    sb.Append($"width = {product.Dimensions.Width.ToString().Replace(",", ".")}, ");
                    sb.Append($"height = {product.Dimensions.Height.ToString().Replace(",", ".")}, ");
                    sb.Append($"weight = {product.Dimensions.Weight.ToString().Replace(",", ".")}");
                    sb.AppendLine($" {GetPidSelect(product, partial: true)};");
                }
            }

            var result = sb.ToString().TrimHtml().Trim(',') + ";";
            return result;
        }

        internal string GetImagesSql()
        {
            var sb = new StringBuilder();

            foreach (var product in products)
            {
                int sort_order = 1;

                if (product.Images.Count == 0)
                {
                    continue;
                }

                var firstImage = product.Images.First();

                string imagePath = $"catalog/{product.manufacturer_ftp_path}/products/{product.Sku}_1.jpg";
                string pidPartial = GetPidSelect(product, partial: true);
                string pidFull = GetPidSelect(product, partial: false);

                sb.AppendLine($"UPDATE IGNORE oc_product SET image = '{imagePath}' {pidPartial};");

                if (product.Images.Count > 1)
                {
                    foreach (var image in product.Images.Skip(1))
                    {
                        imagePath = $"catalog/{product.manufacturer_ftp_path}/products/{product.Sku}_{sort_order}.jpg";
                        sb.AppendLine($"INSERT IGNORE INTO oc_product_image (product_id, image, sort_order) VALUES ({pidFull}, '{imagePath}', {sort_order});");
                        sort_order++;
                    }
                }

            }

            var result = sb.ToString();
            return result;
        }
    }

}
