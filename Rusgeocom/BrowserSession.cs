using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Rusgeocom.ParserLib
{
    /// <summary>
    /// Результат прохождения проверки браузера: User-Agent и cookies, которые нужно передать в HttpClient.
    /// </summary>
    public sealed class BrowserSessionResult
    {
        public string UserAgent { get; init; }
        public List<System.Net.Cookie> Cookies { get; init; }
    }

    /// <summary>
    /// Открывает страницу в настоящем Chromium (Playwright), дожидается окончания проверки браузера
    /// (редирект на реальную страницу) и возвращает cookies + User-Agent для дальнейших запросов через HttpClient.
    /// </summary>
    public static class BrowserSession
    {
        /// <summary>
        /// CSS-селектор элемента, который есть только на настоящей странице сайта, но отсутствует на проверочной.
        /// При необходимости замените на более точный (подберите в DevTools на реальной странице).
        /// </summary>
        public const string DEFAULT_READY_SELECTOR = "footer";

        public static async Task<BrowserSessionResult> PassCheckAsync(
            string url,
            bool headless = true,
            string readySelector = DEFAULT_READY_SELECTOR,
            TimeSpan? timeout = null)
        {
            var waitTimeout = (float)(timeout ?? TimeSpan.FromSeconds(60)).TotalMilliseconds;

            using var playwright = await Playwright.CreateAsync();
            await using var browser = await LaunchWithAutoInstallAsync(playwright, headless);

            var context = await browser.NewContextAsync();
            var page = await context.NewPageAsync();

            await page.GotoAsync(url);

            try
            {
                await page.WaitForSelectorAsync(readySelector, new PageWaitForSelectorOptions
                {
                    State = WaitForSelectorState.Attached,
                    Timeout = waitTimeout
                });
            }
            catch (Microsoft.Playwright.TimeoutException ex)
            {
                throw new InvalidOperationException(
                    $"Проверка браузера не пройдена за отведённое время: на странице {url} не появился элемент '{readySelector}'. " +
                    "Попробуйте headless = false, увеличить таймаут или уточнить селектор.", ex);
            }

            // В headless-режиме в User-Agent присутствует "HeadlessChrome" - приводим к обычному виду
            var userAgent = (await page.EvaluateAsync<string>("navigator.userAgent"))
                .Replace("HeadlessChrome", "Chrome");

            var cookies = new List<System.Net.Cookie>();
            foreach (var c in await context.CookiesAsync())
            {
                cookies.Add(new System.Net.Cookie(c.Name, c.Value, c.Path, c.Domain));
            }

            return new BrowserSessionResult { UserAgent = userAgent, Cookies = cookies };
        }

        private static async Task<IBrowser> LaunchWithAutoInstallAsync(IPlaywright playwright, bool headless)
        {
            var options = new BrowserTypeLaunchOptions { Headless = headless };

            try
            {
                return await playwright.Chromium.LaunchAsync(options);
            }
            catch (PlaywrightException ex) when (ex.Message.Contains("Executable doesn't exist"))
            {
                // Chromium ещё не установлен на этом компьютере - скачиваем при первом запуске
                var exitCode = Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
                if (exitCode != 0)
                {
                    throw new InvalidOperationException(
                        "Не удалось установить Chromium для Playwright. Выполните вручную: playwright install chromium", ex);
                }

                return await playwright.Chromium.LaunchAsync(options);
            }
        }
    }
}
