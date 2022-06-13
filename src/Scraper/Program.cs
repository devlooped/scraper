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
    .AddLazy()
    .AddSingleton((_) => Playwright.CreateAsync().Result)
    .AddSingleton((services) => services.GetRequiredService<IPlaywright>().Chromium.LaunchAsync(new BrowserTypeLaunchOptions
    {
        ExecutablePath = Path.Combine(AppContext.BaseDirectory, "runtimes", "linux-x64", "native", "chrome"),
        Headless = true,
    }).Result)
    .AddTransient<Scraper>();

var app = builder.Build();

app.UseRouting();

app.MapPost("/", ([FromServices] Scraper scraper, Scrape request) => scraper.ScrapeAsync(request));
app.MapGet("/", ([FromServices] Scraper scraper, string selector, string url, bool? browserOnly) 
    => scraper.ScrapeAsync(new Scrape(selector, url, browserOnly ?? false)));

app.Run();