using HtmlAgilityPack;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Rusgeocom.ParserLib
{
    public abstract class ProductParserBase
    {
        protected readonly HttpClient client;


        public ProductParserBase()
        {
            // credentials: "include" означает, что нужны куки — используем CookieContainer
            var cookieContainer = new CookieContainer();

            var handler = new HttpClientHandler
            {
                CookieContainer = cookieContainer,
                UseCookies = true,
                AllowAutoRedirect = true
            };

            client = new HttpClient(handler);

            client.DefaultRequestHeaders.Referrer = new Uri("https://www.rusgeocom.ru/products/lazernyj-dalnomer-leica-disto-d2-new");
            // Заголовки
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9,ru;q=0.8");
            client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };
            client.DefaultRequestHeaders.Pragma.Add(new NameValueHeaderValue("no-cache"));
            client.DefaultRequestHeaders.TryAddWithoutValidation("Priority", "u=0, i");
            client.DefaultRequestHeaders.TryAddWithoutValidation("sec-ch-ua", "\"Not=A?Brand\";v=\"99\", \"Google Chrome\";v=\"151\", \"Chromium\";v=\"151\"");
            client.DefaultRequestHeaders.TryAddWithoutValidation("sec-ch-ua-mobile", "?0");
            client.DefaultRequestHeaders.TryAddWithoutValidation("sec-ch-ua-platform", "\"Windows\"");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Dest", "document");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Site", "same-origin");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-User", "?1");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Upgrade-Insecure-Requests", "1");
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/151.0.0.0 Safari/537.36");


            // User-Agent — в fetch его не видно (браузер добавляет сам), но для C# обязательно нужно добавить вручную,
            // иначе сервер сразу поймёт, что это не браузер

        }

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
        }
    }
}

