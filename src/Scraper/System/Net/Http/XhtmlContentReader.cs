using System.Xml;

namespace System.Net.Http;

/// <summary>
/// Skips known non-content elements such as scripts, styles and iframes. 
/// It also removes all XML namespaces, since for HTML content it's typically 
/// irrelevant.
/// </summary>
class XhtmlContentReader : XmlWrappingReader
{
    const string XmlNsNamespace = "http://www.w3.org/2000/xmlns/";

    /// <summary>
    /// Skips elements that typically aren't processed as content: 
    /// script, noscript, style and iframe.
    /// </summary>
    public static HashSet<string> DefaultSkipElements { get; } = new()
    {
        "script",
        "noscript",
        "style",
        "iframe",
    };

    public XhtmlContentReader(XmlReader baseReader) : base(baseReader) { }

    public HashSet<string> SkipElements { get; } = DefaultSkipElements;

    public override int AttributeCount
    {
        get
        {
            var count = 0;
            for (var go = MoveToFirstAttribute(); go; go = MoveToNextAttribute())
                count++;

            return count;
        }
    }

    public override bool MoveToFirstAttribute()
    {
        var moved = base.MoveToFirstAttribute();
        while (moved && (IsXmlNs || IsLocalXmlNs))
            moved = MoveToNextAttribute();

        if (!moved) 
            base.MoveToElement();

        return moved;
    }

    /// <summary>
    /// See <see cref="XmlReader.MoveToNextAttribute"/>.
    /// </summary>
    public override bool MoveToNextAttribute()
    {
        var moved = base.MoveToNextAttribute();
        while (moved && (IsXmlNs || IsLocalXmlNs))
            moved = MoveToNextAttribute();

        return moved;
    }

    /// <summary>
    /// We only support the <c>xml</c> prefix, used for <c>xml:lang</c> and <c>xml:space</c> 
    /// built-in text handling in XHTML.
    /// </summary>
    public override string Prefix => base.Prefix == "xml" ? "xml" : "";

    public override string NamespaceURI => Prefix == "xml" ? base.NamespaceURI : "";

    bool IsXmlNs => base.NamespaceURI == XmlNsNamespace;

    bool IsLocalXmlNs => Prefix == "xmlns";

    public override bool Read()
    {
        var read = base.Read();
        if (read && base.NodeType == XmlNodeType.Element && SkipElements.Contains(LocalName))
            base.Skip();

        return read;
    }
}