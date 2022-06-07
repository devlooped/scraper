using System.Xml;
using System.Xml.Linq;

namespace System.Net.Http;

static class HttpContentExtensions
{
    public static Task<XDocument> ReadAsDocumentAsync(this HttpContent content, CancellationToken cancellation)
        => ReadAsDocumentAsync(content, default, cancellation);

    public static async Task<XDocument> ReadAsDocumentAsync(this HttpContent content, string? defaultBaseUrl = default, CancellationToken cancellation = default)
    {
        if (content.Headers.ContentType?.MediaType != "text/xml" &&
            content.Headers.ContentType?.MediaType != "application/xml")
            throw new ArgumentException("The content does not contain an XML media type.");

        using var html = await content.ReadAsStreamAsync(cancellation);

        // Ignore query string & fragment from URI.
        var baseUri = content.Headers.ContentLocation ?? (defaultBaseUrl != null ? new Uri(defaultBaseUrl) : null);
        if (baseUri != null)
            baseUri = new Uri(baseUri.GetComponents(UriComponents.Scheme | UriComponents.Host | UriComponents.Path, UriFormat.Unescaped));

        if (baseUri == null)
            return XDocument.Load(html, LoadOptions.None);

        return XDocument.Load(
            new BaseUriXmlReader(baseUri.AbsoluteUri, XmlReader.Create(html)),
            LoadOptions.SetBaseUri);
    }
}
