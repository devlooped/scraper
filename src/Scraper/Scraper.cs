using Devlooped.Xml.Css;

public record Scrape(string Selector, string Url, bool BrowserOnly = false);

public static class Scraper
{
    public static async Task<IResult> CssAsync([FromServices] IHttpClientFactory factory, [FromServices] Lazy<IBrowser> browser, Scrape scrape)
    {
        var results = new List<XElement>();

        if (!scrape.BrowserOnly)
        {
            var http = factory.CreateClient("Xhtml");
            var response = await http.GetAsync(scrape.Url);

            if (!response.IsSuccessStatusCode)
                return Results.StatusCode((int)response.StatusCode);

            var doc = await response.Content.ReadAsDocumentAsync();
            results.AddRange(doc.CssSelectElements(scrape.Selector));
        }

        if (results.Count == 0)
        {
            var page = await browser.Value.NewPageAsync();
            await page.GotoAsync(scrape.Url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle
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
