using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Xml;
using Sgml;

namespace System.Net.Http;

/// <summary>
/// Automatically sanitizes and cleans up HTML content ("text/html" media type only) so it's well-formed XML.
/// </summary>
public partial class XhtmlHttpHandler : DelegatingHandler
{
    static readonly Regex scriptRegex = new Regex(@"<script.*?</script>", RegexOptions.Singleline);

    public XhtmlHttpHandler(HttpMessageHandler inner) : base(inner) { }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
    {
        var response = await base.SendAsync(request, cancellation).ConfigureAwait(false);

        if (response.Content.Headers.ContentType?.MediaType == "text/html")
        {
            var html = await response.Content.ReadAsStringAsync(cancellation);
            // <script> parsing can seriously affect performance of SgmlReader,
            // so remove them entirely by simple string replacement.
            html = scriptRegex.Replace(html, "");

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

            var stream = new MemoryStream();
            using var writer = XmlWriter.Create(stream);

            writer.WriteNode(new XhtmlContentReader(reader), false);
            writer.Flush();
            stream.Position = 0;

            response.Content = new StreamContent(stream);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/xml");
        }

        return response;
    }
}