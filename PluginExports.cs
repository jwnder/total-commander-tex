using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

public static unsafe class PluginExports
{
    private const int EditControlId = 1001;

    [UnmanagedCallersOnly(EntryPoint = "ListLoadW", CallConvs = [typeof(CallConvStdcall)])]
    public static nint ListLoadW(nint parentWin, char* fileToLoad, int showFlags)
    {
        string? path = Marshal.PtrToStringUni((nint)fileToLoad);
        return Load(parentWin, path);
    }

    [UnmanagedCallersOnly(EntryPoint = "ListLoad", CallConvs = [typeof(CallConvStdcall)])]
    public static nint ListLoad(nint parentWin, byte* fileToLoad, int showFlags)
    {
        string? path = Marshal.PtrToStringAnsi((nint)fileToLoad);
        return Load(parentWin, path);
    }

    [UnmanagedCallersOnly(EntryPoint = "ListCloseWindow", CallConvs = [typeof(CallConvStdcall)])]
    public static void ListCloseWindow(nint listWin)
    {
        if (listWin != 0)
        {
            NativeMethods.DestroyWindow(listWin);
        }
    }

    [UnmanagedCallersOnly(EntryPoint = "ListGetDetectStringW", CallConvs = [typeof(CallConvStdcall)])]
    public static void ListGetDetectStringW(char* detectString, int maxLen)
    {
        WriteUtf16(detectString, maxLen, "EXT=\"TEX\" | EXT=\"LATEX\" | EXT=\"LTX\" | EXT=\"STY\" | EXT=\"CLS\" | EXT=\"PS\" | EXT=\"EPS\"");
    }

    [UnmanagedCallersOnly(EntryPoint = "ListGetDetectString", CallConvs = [typeof(CallConvStdcall)])]
    public static void ListGetDetectString(byte* detectString, int maxLen)
    {
        WriteAnsi(detectString, maxLen, "EXT=\"TEX\" | EXT=\"LATEX\" | EXT=\"LTX\" | EXT=\"STY\" | EXT=\"CLS\" | EXT=\"PS\" | EXT=\"EPS\"");
    }

    private static nint Load(nint parentWin, string? path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            if (TexCompileRenderer.IsPostScript(path))
            {
                TexRenderResult psResult = TexCompileRenderer.RenderPostScript(path);
                if (psResult.Success)
                {
                    return RenderWindow.Create(parentWin, psResult.Images);
                }

                return CreateTextView(parentWin, psResult.Error ?? "Unable to render PostScript file.");
            }

            try
            {
                List<string> images = TexImageParser.GetIncludedImages(path);
                if (images.Count > 0)
                {
                    return RenderWindow.Create(parentWin, images);
                }
            }
            catch (Exception ex)
            {
                return CreateTextView(parentWin, "Unable to parse TeX image references:\r\n" + ex.Message);
            }

            TexRenderResult renderResult = TexCompileRenderer.Render(path);
            if (renderResult.Success)
            {
                return RenderWindow.Create(parentWin, renderResult.Images);
            }

            if (!string.IsNullOrWhiteSpace(renderResult.Error))
            {
                return CreateTextView(parentWin, renderResult.Error);
            }
        }

        return CreateTextView(parentWin, LoadTextForDisplay(path));
    }

    private static nint CreateTextView(nint parentWin, string text)
    {
        nint edit = NativeMethods.CreateWindowEx(
            0,
            "EDIT",
            string.Empty,
            NativeMethods.WS_CHILD | NativeMethods.WS_VISIBLE | NativeMethods.WS_VSCROLL | NativeMethods.WS_HSCROLL |
            NativeMethods.ES_LEFT | NativeMethods.ES_MULTILINE | NativeMethods.ES_AUTOVSCROLL |
            NativeMethods.ES_AUTOHSCROLL | NativeMethods.ES_READONLY,
            0,
            0,
            100,
            100,
            parentWin,
            EditControlId,
            0,
            0);

        if (edit == 0)
        {
            return 0;
        }

        NativeMethods.SendMessage(edit, NativeMethods.EM_SETLIMITTEXT, 0, 0);

        nint font = NativeMethods.GetStockObject(NativeMethods.ANSI_FIXED_FONT);
        if (font != 0)
        {
            NativeMethods.SendMessage(edit, NativeMethods.WM_SETFONT, font, 1);
        }

        NativeMethods.SendMessage(edit, NativeMethods.WM_SETREDRAW, 0, 0);
        NativeMethods.SendMessageText(edit, NativeMethods.WM_SETTEXT, 0, text);
        NativeMethods.SendMessage(edit, NativeMethods.WM_SETREDRAW, 1, 0);

        return edit;
    }

    private static string LoadTextForDisplay(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "No TeX file was provided.";
        }

        try
        {
            return TexEncoding.ReadText(path);
        }
        catch (Exception ex)
        {
            return "Unable to load TeX file:\r\n" + ex.Message;
        }
    }

    private static void WriteUtf16(char* destination, int maxLen, string value)
    {
        if (destination == null || maxLen <= 0)
        {
            return;
        }

        int count = Math.Min(value.Length, maxLen - 1);
        for (int i = 0; i < count; i++)
        {
            destination[i] = value[i];
        }

        destination[count] = '\0';
    }

    private static void WriteAnsi(byte* destination, int maxLen, string value)
    {
        if (destination == null || maxLen <= 0)
        {
            return;
        }

        byte[] bytes = Encoding.ASCII.GetBytes(value);
        int count = Math.Min(bytes.Length, maxLen - 1);
        for (int i = 0; i < count; i++)
        {
            destination[i] = bytes[i];
        }

        destination[count] = 0;
    }
}
