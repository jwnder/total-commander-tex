internal static class TexImageParser
{
    internal static List<string> GetIncludedImages(string texPath)
    {
        string baseDirectory = Path.GetDirectoryName(texPath) ?? Directory.GetCurrentDirectory();
        string text = TexEncoding.ReadText(texPath);
        List<string> images = [];

        int index = 0;
        while ((index = text.IndexOf("\\includegraphics", index, StringComparison.Ordinal)) >= 0)
        {
            int braceStart = text.IndexOf('{', index);
            if (braceStart < 0)
            {
                break;
            }

            int braceEnd = text.IndexOf('}', braceStart + 1);
            if (braceEnd < 0)
            {
                break;
            }

            string imagePath = text.Substring(braceStart + 1, braceEnd - braceStart - 1)
                .Trim()
                .Replace('/', Path.DirectorySeparatorChar);

            string fullPath = Path.GetFullPath(Path.Combine(baseDirectory, imagePath));
            if (File.Exists(fullPath))
            {
                images.Add(fullPath);
            }

            index = braceEnd + 1;
        }

        return images;
    }
}
