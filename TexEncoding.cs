using System.Text;

internal static class TexEncoding
{
    internal static string ReadText(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length == 0)
        {
            return string.Empty;
        }

        Encoding encoding = DetectEncoding(bytes);
        string text = encoding.GetString(bytes);

        if (text.IndexOf('\uFFFD') >= 0 && encoding.CodePage != Encoding.Default.CodePage)
        {
            string fallback = Encoding.Default.GetString(bytes);
            if (CountReplacementChars(fallback) < CountReplacementChars(text))
            {
                text = fallback;
            }
        }

        return NormalizeNewlines(text);
    }

    private static Encoding DetectEncoding(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: false);
        }

        if (bytes.Length >= 2)
        {
            if (bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                return Encoding.Unicode;
            }

            if (bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                return Encoding.BigEndianUnicode;
            }
        }

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);
    }

    private static int CountReplacementChars(string value)
    {
        int count = 0;
        foreach (char c in value)
        {
            if (c == '\uFFFD')
            {
                count++;
            }
        }

        return count;
    }

    private static string NormalizeNewlines(string value)
    {
        return value.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", "\r\n");
    }
}
