var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("Default");
builder.Services.AddHttpClient("Xhtml")
    .ConfigurePrimaryHttpMessageHandler(() =>
        new ContentLocationHttpHandler(
            new XhtmlHttpHandler(new HttpClientHandler
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                UseDefaultCredentials = true,
                UseProxy = false,
                Proxy = null
            })));

builder.Services
    .AddAsyncLazy()
    .AddSingleton(_ => Playwright.CreateAsync())
    .AddSingleton(async services => 
    {
        var playwright = await services.GetRequiredService<AsyncLazy<IPlaywright>>();
        return await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            ExecutablePath = Path.Combine(AppContext.BaseDirectory, "runtimes", "linux-x64", "native", "chrome"),
            Headless = true,
        });
    })
    .AddTransient<Scraper>();

var app = builder.Build();

app.UseRouting();

app.MapPost("/", ([FromServices] Scraper scraper, Scrape request) => scraper.ScrapeAsync(request));
app.MapGet("/", ([FromServices] Scraper scraper, string selector, string url, bool? browserOnly)
    => scraper.ScrapeAsync(new Scrape(selector, url, browserOnly ?? false)));

app.Run();