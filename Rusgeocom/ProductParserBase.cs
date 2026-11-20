using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Rusgeocom.ParserLib
{
    public abstract class ProductParserBase
    {
        protected readonly HttpClient client;

        private readonly CookieContainer cookieContainer = new CookieContainer();
        private readonly HashSet<string> passedHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim browserCheckLock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// true - проверка браузера проходится в скрытом Chromium (без окна), false - откроется окно браузера (удобно для отладки).
        /// </summary>
        public bool BrowserHeadless { get; set; } = true;

        /// <summary>
        /// CSS-селектор элемента, наличие которого означает, что проверка пройдена и открыта реальная страница сайта.
        /// </summary>
        public string BrowserReadySelector { get; set; } = BrowserSession.DEFAULT_READY_SELECTOR;

        public ProductParserBase()
        {
            // credentials: "include" означает, что нужны куки — используем CookieContainer
            var handler = new HttpClientHandler
            {
                CookieContainer = cookieContainer,
                UseCookies = true,
                AllowAutoRedirect = true,

            };

            client = new HttpClient(handler);

            client.DefaultRequestHeaders.Referrer = new Uri("https://www.rusgeocom.ru/");
            // Заголовки
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9,ru;q=0.8");
            client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { NoCache = true };
            client.DefaultRequestHeaders.Pragma.Add(new NameValueHeaderValue("no-cache"));
            client.DefaultRequestHeaders.TryAddWithoutValidation("Priority", "u=0, i");
            client.DefaultRequestHeaders.TryAddWithoutValidation("sec-ch-ua", "\"Not-A.Brand\";v=\"99\", \"Google Chrome\";v=\"157\", \"Chromium\";v=\"157\"");
            client.DefaultRequestHeaders.TryAddWithoutValidation("sec-ch-ua-mobile", "?0");
            client.DefaultRequestHeaders.TryAddWithoutValidation("sec-ch-ua-platform", "\"Windows\"");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Dest", "document");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-Site", "same-origin");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Sec-Fetch-User", "?1");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Upgrade-Insecure-Requests", "1");
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/157.0.0.0 Safari/537.36");


            // User-Agent — в fetch его не видно (браузер добавляет сам), но для C# обязательно нужно добавить вручную,
            // иначе сервер сразу поймёт, что это не браузер

        }

        /// <summary>
        /// Проходит проверку браузера (NGENIX) для хоста указанного адреса в настоящем Chromium
        /// и передаёт полученные cookies и User-Agent в HttpClient. Для каждого хоста выполняется один раз за сессию.
        /// </summary>
        protected async Task EnsureBrowserSessionAsync(string url)
        {
            var host = new Uri(url).Host;

            await browserCheckLock.WaitAsync();
            try
            {
                lock (passedHosts)
                {
                    if (passedHosts.Contains(host))
                    {
                        return;
                    }
                }

                var session = await BrowserSession.PassCheckAsync(url, BrowserHeadless, BrowserReadySelector);

                foreach (var cookie in session.Cookies)
                {
                    try
                    {
                        cookieContainer.Add(cookie);
                    }
                    catch (CookieException)
                    {
                        // пропускаем cookie с недопустимыми символами - на работу сессии они не влияют
                    }
                }

                // User-Agent должен совпадать с браузером, прошедшим проверку, иначе cookie может не приниматься
                client.DefaultRequestHeaders.Remove("User-Agent");
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", session.UserAgent);
                // версия Chrome в sec-ch-ua могла не совпасть с версией Chromium из Playwright
                client.DefaultRequestHeaders.Remove("sec-ch-ua");

                lock (passedHosts)
                {
                    passedHosts.Add(host);
                }
            }
            finally
            {
                browserCheckLock.Release();
            }
        }

        /// <summary>
        /// Сбрасывает признак пройденной проверки: следующий запрос снова откроет браузер (например, если cookie протухла).
        /// </summary>
        public void ResetBrowserSession()
        {
            lock (passedHosts)
            {
                passedHosts.Clear();
            }
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

