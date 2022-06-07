using System.Net.Http.Headers;

namespace System.Net.Http;

/// <summary>
/// Automatically sets the <see cref="HttpContent.Headers"/> so <see cref="HttpContentHeaders.ContentLocation"/> 
/// matches the requested uri.
/// </summary>
public class ContentLocationHttpHandler : DelegatingHandler
{
    public ContentLocationHttpHandler(HttpMessageHandler inner) : base(inner) { }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
    {
        var response = await base.SendAsync(request, cancellation).ConfigureAwait(false);

        if (response.IsSuccessStatusCode && response.Content.Headers.ContentLocation == null)
            response.Content.Headers.ContentLocation = request.RequestUri;

        return response;
    }
}