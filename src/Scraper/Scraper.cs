﻿using System.Text;
using Devlooped.Web;

public record Scrape(string Selector, string Url, bool BrowserOnly = false);

record Scraper(IHttpClientFactory HttpFactory, AsyncLazy<IBrowser> Browser, ILogger<Scraper> Logger)
{
    public async Task<IResult> ScrapeAsync(Scrape scrape)
    {
        var results = new List<XElement>();

        if (!scrape.BrowserOnly)
        {
            var http = HttpFactory.CreateClient();
            var response = await http.GetAsync(scrape.Url);

            // We'll automatically fallback to browser
            if (response.IsSuccessStatusCode)
            {
                var doc = HtmlDocument.Load(await response.Content.ReadAsStreamAsync());
                results.AddRange(doc.CssSelectElements(scrape.Selector));
            }
        }
        else
        {
            Logger.LogInformation("Scraping using browser only: " + scrape.Url + "#" + scrape.Selector);
        }

        if (results.Count == 0)
        {
            if (!scrape.BrowserOnly)
                Logger.LogInformation("Falling back to browser scraping for " + scrape.Url + "#" + scrape.Selector);

            var browser = await Browser;
            var page = await browser.NewPageAsync();
            await page.GotoAsync(scrape.Url);
            
            try
            {
                await page.WaitForSelectorAsync(scrape.Selector, new PageWaitForSelectorOptions
                {
                    State = WaitForSelectorState.Attached
                });
            }
            catch (TimeoutException)
            {
                Logger.LogInformation("Timed out waiting for selector. Trying direct page query");

                var pageFallback = false;

                // Try with whatever state we got so far, perhaps this will work anyway? :/
                foreach (var element in await page.QuerySelectorAllAsync(scrape.Selector))
                {
                    if (await ReadOuterXmlAsync(element) is XElement node)
                        results.Add(node);
                }

                if (results.Count == 0)
                {
                    Logger.LogInformation("Final try using Linq to CSS by loading page raw HTML");

                    // Final attempt using Linq2Css?
                    var html = await ReadOuterXmlAsync(await page.QuerySelectorAsync("html"));

                    results.AddRange(html.CssSelectElements(scrape.Selector));
                }
                else
                {
                    pageFallback = true;
                }

                if (results.Count == 0)
                    return new XmlContentResult(await ReadOuterXmlAsync(await page.QuerySelectorAsync("html")) ?? new XElement("html"), 404);
                else
                    return new XmlContentResult(new XElement("scraper", results))
                    {
                        Headers =
                        {
                            { "X-Fallback", pageFallback ? "html" : "linq2css" }
                        }
                    };
            }

            var elements = await page.QuerySelectorAllAsync(scrape.Selector);

            foreach (var element in elements)
            {
                if (await ReadOuterXmlAsync(element) is XElement node)
                    results.Add(node);
            }
        }

        return new XmlContentResult(new XElement("scraper", results));
    }

    static async Task<XElement?> ReadOuterXmlAsync(IElementHandle? element)
    {
        if (element == null)
            return null;
        
        var html = await element.EvaluateAsync<string>("el => el.outerHTML");
        using var reader = new SgmlReader(new XmlReaderSettings
        {
            CheckCharacters = true,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            IgnoreWhitespace = true,
        })
        {
            InputStream = new StringReader(html),
            WhitespaceHandling = WhitespaceHandling.Significant,
        };

        if (reader.MoveToContent() == XmlNodeType.Element)
            return (XElement)XNode.ReadFrom(reader);

        return null;
    }

    record XmlContentResult(XElement Content, int StatusCode = 200) : IResult
    {
        public Dictionary<string, string> Headers { get; } = new();

        public async Task ExecuteAsync(HttpContext context)
        {
            context.Response.StatusCode = StatusCode;
            context.Response.ContentType = "application/xml";

            foreach (var header in Headers)
            {
                context.Response.Headers[header.Key] = header.Value;
            }

            var xml = Content.ToString();
            var length = Encoding.UTF8.GetByteCount(xml);

            context.Response.ContentLength = length;
            await context.Response.WriteAsync(xml);
        }
    }
}
