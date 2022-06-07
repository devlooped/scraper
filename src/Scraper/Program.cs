var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("http");
builder.Services.AddHttpClient("xhtml")
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
    }).Result);

var app = builder.Build();

app.UseRouting();

app.MapPost("/css", Scrape.CssAsync);
app.MapGet("/css", ([FromServices] IHttpClientFactory factory, [FromServices]Lazy<IBrowser> chromium, string selector, string url, bool? browser) 
    => Scrape.CssAsync(factory, chromium, new ScrapeRequest(selector, url, browser ?? false)));

app.Run();