using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

internal sealed class TexRenderResult
{
    private TexRenderResult(List<string> images, string? error)
    {
        Images = images;
        Error = error;
    }

    internal List<string> Images { get; }

    internal string? Error { get; }

    internal bool Success => Images.Count > 0;

    internal static TexRenderResult FromImages(List<string> images)
    {
        return new TexRenderResult(images, null);
    }

    internal static TexRenderResult FromError(string error)
    {
        return new TexRenderResult([], error);
    }
}

internal static class TexCompileRenderer
{
    internal static bool IsPostScript(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".ps", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".eps", StringComparison.OrdinalIgnoreCase);
    }

    internal static TexRenderResult Render(string texPath)
    {
        string? psPath = FindSiblingPostScript(texPath);
        if (psPath is not null)
        {
            TexRenderResult psResult = RenderPostScript(texPath, psPath);
            if (psResult.Success)
            {
                return psResult;
            }
        }

        string? consumerPsPath = FindConsumerPostScript(texPath);
        if (consumerPsPath is not null)
        {
            TexRenderResult psResult = RenderPostScript(texPath, consumerPsPath);
            if (psResult.Success)
            {
                return psResult;
            }
        }

        string? xelatex = ToolLocator.Find("xelatex.exe");
        string? pdftoppm = ToolLocator.Find("pdftoppm.exe");

        if (xelatex is null || pdftoppm is null)
        {
            return TexRenderResult.FromError(
                "Unable to render LaTeX because xelatex.exe or pdftoppm.exe was not found.\r\n\r\n" +
                "Install MiKTeX or TeX Live and make sure both tools are available.");
        }

        string cacheDir = GetCacheDirectory(texPath);
        Directory.CreateDirectory(cacheDir);

        string jobName = Path.GetFileNameWithoutExtension(texPath);
        string pdfPath = Path.Combine(cacheDir, jobName + ".pdf");

        string compileOutput = RunProcess(
            xelatex,
            Path.GetDirectoryName(texPath) ?? Directory.GetCurrentDirectory(),
            [
                "-interaction=nonstopmode",
                "-halt-on-error",
                "-output-directory=" + cacheDir,
                Path.GetFileName(texPath)
            ]);

        if (!File.Exists(pdfPath))
        {
            TexRenderResult placeholderResult = RenderWithGeneratedMissingEpsPlaceholders(texPath, xelatex, pdftoppm);
            if (placeholderResult.Success)
            {
                return placeholderResult;
            }

            TexRenderResult listingResult = RenderSourceListing(texPath, xelatex, pdftoppm, compileOutput);
            if (listingResult.Success)
            {
                return listingResult;
            }

            return listingResult.Error is not null
                ? listingResult
                : TexRenderResult.FromError("LaTeX compilation failed.\r\n\r\n" + TrimOutput(compileOutput));
        }

        foreach (string oldImage in Directory.EnumerateFiles(cacheDir, "page-*.jpg"))
        {
            File.Delete(oldImage);
        }

        string convertOutput = RunProcess(
            pdftoppm,
            cacheDir,
            [
                "-jpeg",
                "-r",
                "160",
                pdfPath,
                Path.Combine(cacheDir, "page")
            ]);

        List<string> images = Directory
            .EnumerateFiles(cacheDir, "page-*.jpg")
            .OrderBy(NaturalPageOrder)
            .ToList();

        if (images.Count == 0)
        {
            TexRenderResult listingResult = RenderSourceListing(texPath, xelatex, pdftoppm, convertOutput);
            if (listingResult.Success)
            {
                return listingResult;
            }

            return listingResult.Error is not null
                ? listingResult
                : TexRenderResult.FromError("PDF conversion failed.\r\n\r\n" + TrimOutput(convertOutput));
        }

        return TexRenderResult.FromImages(images);
    }

    private static TexRenderResult RenderWithGeneratedMissingEpsPlaceholders(string texPath, string xelatex, string pdftoppm)
    {
        List<string> missingEpsFiles = FindMissingEpsReferences(texPath);
        if (missingEpsFiles.Count == 0)
        {
            return TexRenderResult.FromError("No missing EPS references found.");
        }

        string cacheDir = GetCacheDirectoryForKey(texPath + "|missing-eps");
        Directory.CreateDirectory(cacheDir);

        foreach (string oldImage in Directory.EnumerateFiles(cacheDir, "page-*.jpg"))
        {
            File.Delete(oldImage);
        }

        string copiedTex = Path.Combine(cacheDir, Path.GetFileName(texPath));
        File.Copy(texPath, copiedTex, overwrite: true);

        foreach (string epsReference in missingEpsFiles)
        {
            string placeholderPath = Path.GetFullPath(Path.Combine(cacheDir, epsReference.Replace('/', Path.DirectorySeparatorChar)));
            string? parent = Path.GetDirectoryName(placeholderPath);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                Directory.CreateDirectory(parent);
            }

            WritePlaceholderEps(placeholderPath, epsReference);
        }

        string compileOutput = RunProcess(
            xelatex,
            cacheDir,
            [
                "-interaction=nonstopmode",
                "-halt-on-error",
                "-output-directory=" + cacheDir,
                Path.GetFileName(copiedTex)
            ]);

        string pdfPath = Path.Combine(cacheDir, Path.GetFileNameWithoutExtension(texPath) + ".pdf");
        if (!File.Exists(pdfPath))
        {
            return TexRenderResult.FromError("LaTeX compilation with generated EPS placeholders failed.\r\n\r\n" + TrimOutput(compileOutput));
        }

        string convertOutput = RunProcess(
            pdftoppm,
            cacheDir,
            [
                "-jpeg",
                "-r",
                "160",
                pdfPath,
                Path.Combine(cacheDir, "page")
            ]);

        List<string> images = Directory
            .EnumerateFiles(cacheDir, "page-*.jpg")
            .OrderBy(NaturalPageOrder)
            .ToList();

        if (images.Count == 0)
        {
            return TexRenderResult.FromError("PDF conversion after EPS placeholder generation failed.\r\n\r\n" + TrimOutput(convertOutput));
        }

        return TexRenderResult.FromImages(images);
    }

    internal static TexRenderResult RenderSourceListing(string texPath)
    {
        string? xelatex = ToolLocator.Find("xelatex.exe");
        string? pdftoppm = ToolLocator.Find("pdftoppm.exe");

        if (xelatex is null || pdftoppm is null)
        {
            return TexRenderResult.FromError(
                "Unable to render TeX source listing because xelatex.exe or pdftoppm.exe was not found.\r\n\r\n" +
                "Install MiKTeX or TeX Live and make sure both tools are available.");
        }

        return RenderSourceListing(texPath, xelatex, pdftoppm, string.Empty);
    }

    internal static TexRenderResult RenderPostScript(string psPath)
    {
        return RenderPostScript(psPath, psPath);
    }

    private static TexRenderResult RenderSourceListing(string texPath, string xelatex, string pdftoppm, string priorError)
    {
        string cacheDir = GetCacheDirectoryForKey(texPath + "|listing");
        Directory.CreateDirectory(cacheDir);

        foreach (string oldImage in Directory.EnumerateFiles(cacheDir, "page-*.jpg"))
        {
            File.Delete(oldImage);
        }

        string wrapperPath = Path.Combine(cacheDir, "source_listing.tex");
        File.WriteAllText(wrapperPath, BuildSourceListingDocument(texPath), Encoding.UTF8);

        string compileOutput = RunProcess(
            xelatex,
            cacheDir,
            [
                "-interaction=nonstopmode",
                "-halt-on-error",
                "-output-directory=" + cacheDir,
                wrapperPath
            ]);

        string pdfPath = Path.Combine(cacheDir, "source_listing.pdf");
        if (!File.Exists(pdfPath))
        {
            return TexRenderResult.FromError(
                "LaTeX compilation failed, and source-listing fallback also failed.\r\n\r\n" +
                TrimOutput(priorError + "\r\n\r\n" + compileOutput));
        }

        string convertOutput = RunProcess(
            pdftoppm,
            cacheDir,
            [
                "-jpeg",
                "-r",
                "160",
                pdfPath,
                Path.Combine(cacheDir, "page")
            ]);

        List<string> images = Directory
            .EnumerateFiles(cacheDir, "page-*.jpg")
            .OrderBy(NaturalPageOrder)
            .ToList();

        if (images.Count == 0)
        {
            return TexRenderResult.FromError(
                "Source-listing PDF conversion failed.\r\n\r\n" +
                TrimOutput(priorError + "\r\n\r\n" + convertOutput));
        }

        return TexRenderResult.FromImages(images);
    }

    private static string BuildSourceListingDocument(string texPath)
    {
        string normalizedPath = texPath.Replace('\\', '/');
        string title = EscapeLatex(Path.GetFileName(texPath));
        return """
               \documentclass[10pt]{article}
               \usepackage[a4paper,margin=15mm]{geometry}
               \usepackage{verbatim}
               \usepackage[T1]{fontenc}
               \pagestyle{plain}
               \begin{document}
               \section*{
               """ + title + """
               }
               \small
               \verbatiminput{
               """ + normalizedPath + """
               }
               \end{document}
               """;
    }

    private static string EscapeLatex(string value)
    {
        return value
            .Replace(@"\", @"\textbackslash{}")
            .Replace("{", @"\{")
            .Replace("}", @"\}")
            .Replace("#", @"\#")
            .Replace("$", @"\$")
            .Replace("%", @"\%")
            .Replace("&", @"\&")
            .Replace("_", @"\_")
            .Replace("^", @"\textasciicircum{}")
            .Replace("~", @"\textasciitilde{}");
    }

    private static TexRenderResult RenderPostScript(string texPath, string psPath)
    {
        string? ghostscript = ToolLocator.Find("gswin64c.exe") ?? ToolLocator.Find("gswin32c.exe");
        if (ghostscript is null)
        {
            return TexRenderResult.FromError("A matching PostScript file exists, but Ghostscript was not found:\r\n" + psPath);
        }

        string cacheDir = GetCacheDirectory(psPath);
        Directory.CreateDirectory(cacheDir);

        foreach (string oldImage in Directory.EnumerateFiles(cacheDir, "page-*.jpg"))
        {
            File.Delete(oldImage);
        }

        string outputPattern = Path.Combine(cacheDir, "page-%03d.jpg");
        string convertOutput = RunProcess(
            ghostscript,
            Path.GetDirectoryName(texPath) ?? Directory.GetCurrentDirectory(),
            [
                "-dSAFER",
                "-dBATCH",
                "-dNOPAUSE",
                "-sDEVICE=jpeg",
                "-r160",
                "-sOutputFile=" + outputPattern,
                psPath
            ]);

        List<string> images = Directory
            .EnumerateFiles(cacheDir, "page-*.jpg")
            .OrderBy(NaturalPageOrder)
            .ToList();

        if (images.Count == 0)
        {
            return TexRenderResult.FromError("PostScript conversion failed.\r\n\r\n" + TrimOutput(convertOutput));
        }

        return TexRenderResult.FromImages(images);
    }

    private static string RunProcess(string fileName, string workingDirectory, IReadOnlyList<string> arguments)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("Unable to start " + fileName);
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit(30000);

        if (!process.HasExited)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best effort cleanup after a render timeout.
            }

            return output + "\r\n" + error + "\r\nTimed out after 30 seconds.";
        }

        return output + "\r\n" + error;
    }

    private static string GetCacheDirectory(string texPath)
    {
        FileInfo fileInfo = new(texPath);
        string key = texPath + "|" + fileInfo.Length + "|" + fileInfo.LastWriteTimeUtc.Ticks;
        return GetCacheDirectoryForKey(key);
    }

    private static string GetCacheDirectoryForKey(string key)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        string name = Convert.ToHexString(hash).Substring(0, 16).ToLowerInvariant();
        return Path.Combine(Path.GetTempPath(), "TexListerPlugin", name);
    }

    private static string? FindSiblingPostScript(string texPath)
    {
        string candidate = Path.ChangeExtension(texPath, ".ps");
        return File.Exists(candidate) ? candidate : null;
    }

    private static string? FindConsumerPostScript(string texPath)
    {
        string? directory = Path.GetDirectoryName(texPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        string baseName = Path.GetFileNameWithoutExtension(texPath);
        List<string> baseNames = [baseName];
        if (baseName.Equals("notestpoints", StringComparison.OrdinalIgnoreCase))
        {
            baseNames.Add("testpoints");
        }

        foreach (string candidateTex in Directory.EnumerateFiles(directory, "*.tex").OrderBy(static x => x, StringComparer.OrdinalIgnoreCase))
        {
            if (string.Equals(candidateTex, texPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string text;
            try
            {
                text = TexEncoding.ReadText(candidateTex);
            }
            catch
            {
                continue;
            }

            if (baseNames.Any(name =>
                    text.Contains("\\input{" + name + "}", StringComparison.Ordinal) ||
                    text.Contains("\\input{" + name + ".tex}", StringComparison.Ordinal) ||
                    text.Contains("\\include{" + name + "}", StringComparison.Ordinal) ||
                    text.Contains("\\include{" + name + ".tex}", StringComparison.Ordinal)))
            {
                string psPath = Path.ChangeExtension(candidateTex, ".ps");
                if (File.Exists(psPath))
                {
                    return psPath;
                }
            }
        }

        return null;
    }

    private static List<string> FindMissingEpsReferences(string texPath)
    {
        string baseDirectory = Path.GetDirectoryName(texPath) ?? Directory.GetCurrentDirectory();
        string text = TexEncoding.ReadText(texPath);
        List<string> result = [];

        int index = 0;
        while ((index = text.IndexOf(".eps", index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            int start = text.LastIndexOf('{', index);
            int end = text.IndexOf('}', index);
            if (start >= 0 && end > index)
            {
                string reference = text.Substring(start + 1, end - start - 1).Trim();
                if (reference.EndsWith(".eps", StringComparison.OrdinalIgnoreCase))
                {
                    string fullPath = Path.GetFullPath(Path.Combine(baseDirectory, reference.Replace('/', Path.DirectorySeparatorChar)));
                    if (!File.Exists(fullPath) && !result.Contains(reference, StringComparer.OrdinalIgnoreCase))
                    {
                        result.Add(reference);
                    }
                }
            }

            index += 4;
        }

        return result;
    }

    private static void WritePlaceholderEps(string path, string label)
    {
        string safeLabel = label.Replace("(", "[").Replace(")", "]");
        File.WriteAllLines(
            path,
            [
                "%!PS-Adobe-3.0 EPSF-3.0",
                "%%BoundingBox: 0 0 240 140",
                "0.92 setgray",
                "0 0 240 140 rectfill",
                "0 setgray",
                "/Helvetica findfont 11 scalefont setfont",
                "20 72 moveto",
                "(Missing EPS placeholder) show",
                "20 54 moveto",
                "(" + safeLabel + ") show",
                "showpage",
                "%%EOF"
            ],
            Encoding.ASCII);
    }

    private static int NaturalPageOrder(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        int dash = name.LastIndexOf('-');
        if (dash >= 0 && int.TryParse(name[(dash + 1)..], out int page))
        {
            return page;
        }

        return int.MaxValue;
    }

    private static string TrimOutput(string output)
    {
        const int maxLength = 12000;
        output = output.Trim();
        if (output.Length <= maxLength)
        {
            return output;
        }

        return output[^maxLength..];
    }
}

internal static class ToolLocator
{
    internal static string? Find(string toolName)
    {
        foreach (string directory in CandidateDirectories())
        {
            string path = Path.Combine(directory, toolName);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static IEnumerable<string> CandidateDirectories()
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(path))
        {
            foreach (string directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                yield return directory.Trim('"');
            }
        }

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        yield return Path.Combine(localAppData, "Programs", "MiKTeX", "miktex", "bin", "x64");
        yield return Path.Combine(localAppData, "Programs", "MiKTeX", "miktex", "bin");
        yield return Path.Combine(programFiles, "MiKTeX", "miktex", "bin", "x64");
        yield return Path.Combine(programFiles, "MiKTeX", "miktex", "bin");
        yield return Path.Combine(programFiles, "TeXLive", "2026", "bin", "windows");
        yield return Path.Combine(programFilesX86, "MiKTeX", "miktex", "bin");
        yield return Path.Combine(programFiles, "gs", "gs10.07.1", "bin");
        yield return Path.Combine(programFiles, "gs", "gs10.06.0", "bin");
    }
}
