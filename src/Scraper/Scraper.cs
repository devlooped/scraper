using System.Text;
using Devlooped.Xml.Css;

public record Scrape(string Selector, string Url, bool BrowserOnly = false);

public record Scraper(IHttpClientFactory HttpFactory, Lazy<IBrowser> Browser, ILogger<Scraper> Logger)
{
    public async Task<IResult> ScrapeAsync(Scrape scrape)
    {
        var results = new List<XElement>();

        if (!scrape.BrowserOnly)
        {
            var http = HttpFactory.CreateClient("Xhtml");
            var response = await http.GetAsync(scrape.Url);

            if (!response.IsSuccessStatusCode)
                return Results.StatusCode((int)response.StatusCode);

            var doc = await response.Content.ReadAsDocumentAsync();
            results.AddRange(doc.CssSelectElements(scrape.Selector));
        }
        else
        {
            Logger.LogInformation("Scraping using browser only: " + scrape.Url + "#" + scrape.Selector);
        }

        if (results.Count == 0)
        {
            if (!scrape.BrowserOnly)
                Logger.LogInformation("Falling back to browser scraping for " + scrape.Url + "#" + scrape.Selector);

            var page = await Browser.Value.NewPageAsync();
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
                // Try with whatever state we got so far, perhaps this will work anyway? :/
                foreach (var element in await page.QuerySelectorAllAsync(scrape.Selector))
                {
                    if (await ReadOuterXml(element) is XElement node)
                        results.Add(node);
                }

                if (results.Count == 0)
                {
                    // Final attempt using Linq2Css?
                    var html = await ReadOuterXml(await page.QuerySelectorAsync("html"));

                    results.AddRange(html.CssSelectElements(scrape.Selector));
                }

                if (results.Count == 0)
                    return new XmlContentResult(await ReadOuterXml(await page.QuerySelectorAsync("html")) ?? new XElement("html"), 404);
                else
                    return Results.Content(new XElement("scraper", results).ToString(), "application/xml");

            }

            var elements = await page.QuerySelectorAllAsync(scrape.Selector);

            foreach (var element in elements)
            {
                if (await ReadOuterXml(element) is XElement node)
                    results.Add(node);
            }
        }

        return Results.Content(new XElement("scraper", results).ToString(), "application/xml");
    }

    async Task<XElement?> ReadOuterXml(IElementHandle? element)
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

    record XmlContentResult(XElement Content, int StatusCode) : IResult
    {
        public async Task ExecuteAsync(HttpContext context)
        {
            context.Response.StatusCode = StatusCode;
            context.Response.ContentType = "application/xml";

            var xml = Content.ToString();
            var length = Encoding.UTF8.GetByteCount(xml);

            context.Response.ContentLength = length;
            await context.Response.WriteAsync(xml);
        }
    }
}
