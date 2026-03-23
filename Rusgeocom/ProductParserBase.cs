using HtmlAgilityPack;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Rusgeocom.ParserLib
{
    public abstract class ProductParserBase
    {
        protected readonly HttpClient client;
        protected readonly Lazy<IWebDriver> driver = new Lazy<IWebDriver>(() =>
        {
            var options = new ChromeOptions();
            options.BrowserVersion = "137";
            foreach (var header in headers)
            {
                options.AddArgument($"--{header.Key}={header.Value}");
            }
            return new ChromeDriver(options); // Example for Chrome
        });

        private static Dictionary<string, string> headers = new Dictionary<string, string>()
            {
                { "accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7" },
                { "accept-encoding", "gzip, deflate, br, zstd" },
                { "accept-language", "en-US,en;q=0.9,ru-RU;q=0.8,ru;q=0.7" },
                { "user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/146.0.0.0 Safari/537.36" },
                { "sec-ch-ua", "\"Chromium\";v=\"146\", \"Not-A.Brand\";v=\"24\", \"Google Chrome\";v=\"146\"" },
            };

        public ProductParserBase()
        {
            client = new HttpClient(new HttpClientHandler()
            {
                AllowAutoRedirect = true,
                CookieContainer = new CookieContainer(),
                UseCookies = true,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            });

            foreach (var h in headers)
            {
                client.DefaultRequestHeaders.Add(h.Key, h.Value);
            }
        }

        protected string GetCurrentUri() => driver.Value.Url;

        protected async Task<HtmlDocument> GetDocument(string uri)
        {
            try
            {
                var doc = new HtmlDocument();
                var response = await client.GetAsync(uri);
                var html = await response.Content.ReadAsStringAsync();
                doc.LoadHtml(html);
                return doc;
            }
            catch
            {
                throw;
            }
            /*
            try
            {
                driver.Value.Navigate().GoToUrl(uri);
                var doc = new HtmlDocument();
                doc.LoadHtml(driver.Value.PageSource);
                return doc;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading document using Selenium: {ex.Message}");
                return null;
            }
            */
        }
    }
}

