internal static class TexDocumentClassifier
{
    internal static bool LooksStandaloneDocument(string texPath)
    {
        string text = TexEncoding.ReadText(texPath);
        return text.Contains("\\documentclass", StringComparison.Ordinal) ||
               text.Contains("\\documentstyle", StringComparison.Ordinal) ||
               text.Contains("\\begin{document}", StringComparison.Ordinal);
    }
}
