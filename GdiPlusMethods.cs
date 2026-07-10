using System.Runtime.InteropServices;

internal static class GdiPlusMethods
{
    internal const int Ok = 0;
    internal const int UnitPixel = 2;

    [StructLayout(LayoutKind.Sequential)]
    internal struct GdiplusStartupInput
    {
        internal uint GdiplusVersion;
        internal nint DebugEventCallback;
        internal int SuppressBackgroundThread;
        internal int SuppressExternalCodecs;
    }

    [DllImport("gdiplus.dll")]
    internal static extern int GdiplusStartup(out nint token, ref GdiplusStartupInput input, nint output);

    [DllImport("gdiplus.dll")]
    internal static extern void GdiplusShutdown(nint token);

    [DllImport("gdiplus.dll", EntryPoint = "GdipCreateFromHDC")]
    internal static extern int CreateFromHdc(nint hdc, out nint graphics);

    [DllImport("gdiplus.dll", EntryPoint = "GdipDeleteGraphics")]
    internal static extern int DeleteGraphics(nint graphics);

    [DllImport("gdiplus.dll", EntryPoint = "GdipLoadImageFromFile", CharSet = CharSet.Unicode)]
    internal static extern int LoadImageFromFile(string filename, out nint image);

    [DllImport("gdiplus.dll", EntryPoint = "GdipDisposeImage")]
    internal static extern int DisposeImage(nint image);

    [DllImport("gdiplus.dll", EntryPoint = "GdipGetImageWidth")]
    internal static extern int GetImageWidth(nint image, out uint width);

    [DllImport("gdiplus.dll", EntryPoint = "GdipGetImageHeight")]
    internal static extern int GetImageHeight(nint image, out uint height);

    [DllImport("gdiplus.dll", EntryPoint = "GdipGraphicsClear")]
    internal static extern int GraphicsClear(nint graphics, uint color);

    [DllImport("gdiplus.dll", EntryPoint = "GdipDrawImageRectRectI")]
    internal static extern int DrawImageRectRectI(
        nint graphics,
        nint image,
        int dstX,
        int dstY,
        int dstWidth,
        int dstHeight,
        int srcX,
        int srcY,
        int srcWidth,
        int srcHeight,
        int srcUnit,
        nint imageAttributes,
        nint callback,
        nint callbackData);
}
