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
            await page.WaitForSelectorAsync(scrape.Selector, new PageWaitForSelectorOptions
            {
                State = WaitForSelectorState.Attached
            });

            var elements = await page.QuerySelectorAllAsync(scrape.Selector);

            foreach (var element in elements)
            {
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
                    results.Add((XElement)XNode.ReadFrom(reader));
            }
        }

        return Results.Content(new XElement("scraper", results).ToString(), "application/xml");
    }
}
