﻿var builder = WebApplication.CreateBuilder(args);

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
    }).Result);

var app = builder.Build();

app.UseRouting();

app.MapPost("/", Scraper.CssAsync);
app.MapGet("/", ([FromServices] IHttpClientFactory factory, [FromServices]Lazy<IBrowser> chromium, string selector, string url, bool? browser) 
    => Scraper.CssAsync(factory, chromium, new Scrape(selector, url, browser ?? false)));

app.Run();