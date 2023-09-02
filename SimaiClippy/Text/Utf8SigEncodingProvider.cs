using System.Text;

namespace SimaiClippy.Text;

public class Utf8SigEncodingProvider : EncodingProvider
{
    public static readonly EncodingProvider Instance = new Utf8SigEncodingProvider();

    public override Encoding? GetEncoding(int codepage)
    {
        return codepage == 65101 ? Encoding.UTF8 : null;
    }

    public override Encoding? GetEncoding(string name)
    {
        return name.ToLowerInvariant() == "utf-8-sig" ? Encoding.UTF8 : null;
    }
}

