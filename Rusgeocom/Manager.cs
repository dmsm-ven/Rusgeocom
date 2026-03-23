using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Rusgeocom.ParserLib
{
    public class Manager
    {
        private readonly string loggerFileName = "errors.txt";
        private readonly JsonSerializerSettings settings;
        private readonly ProductParserNewVersion parser;
        private readonly ResourceDownloader resourceDownloader;
        private readonly string _storageFile;
        private readonly HttpClient client;
        private readonly Formatter formatter;
        private List<Product> products;

        public Manager(string storageFile, Func<int> startIdResolver)
        {
            settings = new JsonSerializerSettings() { Formatting = Formatting.Indented, PreserveReferencesHandling = PreserveReferencesHandling.Objects };

            var fileLogger = new Action<string>(msg =>
            {
                Debug.WriteLine($"[{DateTime.Now.ToLongTimeString()}] {msg}");
            });

            parser = new ProductParserNewVersion(fileLogger);
            _storageFile = storageFile;
            Load();

            formatter = new Formatter(products, startIdResolver);

            client = new HttpClient(new HttpClientHandler() { CookieContainer = new CookieContainer(), AllowAutoRedirect = true });
            client.Timeout = TimeSpan.FromMinutes(3);

            this.resourceDownloader = new ResourceDownloader(parser.GetClient);
        }

        public async Task Parse(IProgress<double> indicator, string skuToParseFile)
        {
            var skuToParse = File.ReadAllLines(skuToParseFile)
                .Distinct()
                .ToList();

            var data = await parser.Parse(skuToParse, indicator);

            products = data;
            Save(products);
        }


        internal async Task ParseUrls(string urlsFile, IProgress<double> progress)
        {
            string[] urls = File.ReadAllLines(urlsFile);
            var data = await parser.ParseUrls(urls, progress);

            products = data;
            Save(products);
        }

        public async Task ParseBrand(string brandUri)
        {
            var data = await parser.ParseBrand(brandUri);

            products = data;
            Save(products);
        }


        public string GetGeneralExport()
        {
            return formatter.GetGeneralExport();
        }

        public string GetAdditionalImagesExport()
        {
            return formatter.GetAdditionalImagesExport();
        }

        public async Task DownloadResource(IProgress<double> indicator)
        {
            await resourceDownloader.DownloadResource(this.products, indicator);
        }

        internal string GetDescriptionAndEquipmentSql()
        {
            return formatter.GetDescriptionAndEquipmentSql();
        }

        public string GetGosreetr()
        {
            return formatter.GetGosreetr();
        }

        public string GetCategories()
        {
            return formatter.GetCategories();
        }

        public string GetProbes()
        {
            return formatter.GetProbes();
        }

        internal string GetCategoriesProducts()
        {
            return formatter.GetCategoriesProducts();
        }

        public string GetModelRanges()
        {
            return formatter.GetModelRanges();
        }

        public string GetAccessories()
        {
            return formatter.GetAccessories();
        }

        private void Save(List<Product> productsToSave)
        {
            if (productsToSave != null)
            {
                var str = JsonConvert.SerializeObject(productsToSave, settings);
                File.WriteAllText(_storageFile, str);
            }
        }

        private void Load()
        {
            if (File.Exists(_storageFile))
            {
                var str = File.ReadAllText(_storageFile);
                products = JsonConvert.DeserializeObject<List<Product>>(str, settings);
            }
        }

        internal string GetPdfSql()
        {
            return formatter.GetPdfSql();
        }

        internal string GetDimensionsSql()
        {
            return formatter.GetDimensionsSql();
        }

        internal string GetImagesSql()
        {
            return formatter.GetImagesSql();
        }

        internal async Task<string> ParseSingleProductFromHtml(string rawHtml)
        {
            var product = await parser.ParseSingleFromHtml(rawHtml);
            await resourceDownloader.DownloadProductResources(product);
            return formatter.GetDescriptionAndEquipmentSql(product);
        }
    }
}
