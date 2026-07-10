using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

internal static unsafe class RenderWindow
{
    private const string ClassName = "TexListerRenderWindow";
    private static readonly ConcurrentDictionary<nint, ViewerState> States = new();
    private static bool _registered;
    private static nint _gdiplusToken;

    internal static nint Create(nint parentWin, List<string> images)
    {
        EnsureRegistered();

        nint hwnd = NativeMethods.CreateWindowEx(
            0,
            ClassName,
            "TeX preview",
            NativeMethods.WS_CHILD | NativeMethods.WS_VISIBLE,
            0,
            0,
            100,
            100,
            parentWin,
            0,
            NativeMethods.GetModuleHandle(null),
            0);

        if (hwnd != 0)
        {
            States[hwnd] = new ViewerState(images);
            NativeMethods.InvalidateRect(hwnd, 0, true);
        }

        return hwnd;
    }

    private static void EnsureRegistered()
    {
        if (_registered)
        {
            return;
        }

        GdiPlusMethods.GdiplusStartupInput startupInput = new()
        {
            GdiplusVersion = 1
        };
        GdiPlusMethods.GdiplusStartup(out _gdiplusToken, ref startupInput, 0);

        fixed (char* className = ClassName)
        {
            NativeMethods.WNDCLASSEX windowClass = new()
            {
                cbSize = (uint)sizeof(NativeMethods.WNDCLASSEX),
                style = NativeMethods.CS_HREDRAW | NativeMethods.CS_VREDRAW,
                lpfnWndProc = (nint)(delegate* unmanaged[Stdcall]<nint, uint, nint, nint, nint>)&WndProc,
                hInstance = NativeMethods.GetModuleHandle(null),
                hCursor = NativeMethods.LoadCursor(0, NativeMethods.IDC_ARROW),
                hbrBackground = NativeMethods.COLOR_WINDOW + 1,
                lpszClassName = className
            };

            NativeMethods.RegisterClassEx(&windowClass);
        }

        _registered = true;
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static nint WndProc(nint hwnd, uint msg, nint wParam, nint lParam)
    {
        switch (msg)
        {
            case NativeMethods.WM_PAINT:
                Paint(hwnd);
                return 0;
            case NativeMethods.WM_LBUTTONDOWN:
                NativeMethods.SetFocus(hwnd);
                return 0;
            case NativeMethods.WM_KEYDOWN:
                HandleKey(hwnd, wParam);
                return 0;
            case NativeMethods.WM_MOUSEWHEEL:
                HandleMouseWheel(hwnd, wParam);
                return 0;
            case NativeMethods.WM_DESTROY:
                if (States.TryRemove(hwnd, out ViewerState? state))
                {
                    state.Dispose();
                }
                return 0;
            default:
                return NativeMethods.DefWindowProc(hwnd, msg, wParam, lParam);
        }
    }

    private static void HandleKey(nint hwnd, nint wParam)
    {
        if (!States.TryGetValue(hwnd, out ViewerState? state))
        {
            return;
        }

        int key = (int)wParam;
        switch (key)
        {
            case 0x21: // Page Up
            case 0x25: // Left
            case 0x26: // Up
                state.Previous();
                NativeMethods.InvalidateRect(hwnd, 0, true);
                break;
            case 0x20: // Space
            case 0x22: // Page Down
            case 0x27: // Right
            case 0x28: // Down
                state.Next();
                NativeMethods.InvalidateRect(hwnd, 0, true);
                break;
            case 0x24: // Home
                state.GoToStart();
                NativeMethods.InvalidateRect(hwnd, 0, true);
                break;
            case 0x23: // End
                state.GoToEnd();
                NativeMethods.InvalidateRect(hwnd, 0, true);
                break;
            case 0xBB: // + / =
            case 0x6B: // Numpad +
                state.ZoomIn();
                NativeMethods.InvalidateRect(hwnd, 0, true);
                break;
            case 0xBD: // - / _
            case 0x6D: // Numpad -
                state.ZoomOut();
                NativeMethods.InvalidateRect(hwnd, 0, true);
                break;
            case 0x30: // 0
            case 0x60: // Numpad 0
                state.ResetZoom();
                NativeMethods.InvalidateRect(hwnd, 0, true);
                break;
        }
    }

    private static void HandleMouseWheel(nint hwnd, nint wParam)
    {
        if (!States.TryGetValue(hwnd, out ViewerState? state))
        {
            return;
        }

        short delta = unchecked((short)(((long)wParam >> 16) & 0xffff));
        if (delta < 0)
        {
            state.Next();
        }
        else
        {
            state.Previous();
        }

        NativeMethods.InvalidateRect(hwnd, 0, true);
    }

    private static void Paint(nint hwnd)
    {
        nint hdc = NativeMethods.BeginPaint(hwnd, out NativeMethods.PAINTSTRUCT paint);
        try
        {
            if (!States.TryGetValue(hwnd, out ViewerState? state))
            {
                return;
            }

            NativeMethods.GetClientRect(hwnd, out NativeMethods.RECT rect);
            if (GdiPlusMethods.CreateFromHdc(hdc, out nint graphics) != GdiPlusMethods.Ok)
            {
                return;
            }

            try
            {
                GdiPlusMethods.GraphicsClear(graphics, 0xFFFFFFFF);
                DrawCurrentImage(graphics, rect, state);
            }
            finally
            {
                GdiPlusMethods.DeleteGraphics(graphics);
            }

            DrawPageText(hdc, state);
        }
        finally
        {
            NativeMethods.EndPaint(hwnd, ref paint);
        }
    }

    private static void DrawCurrentImage(nint graphics, NativeMethods.RECT rect, ViewerState state)
    {
        nint image = state.GetCurrentImage();
        if (image == 0)
        {
            return;
        }

        GdiPlusMethods.GetImageWidth(image, out uint imageWidth);
        GdiPlusMethods.GetImageHeight(image, out uint imageHeight);
        int clientWidth = Math.Max(1, rect.Right - rect.Left);
        int clientHeight = Math.Max(1, rect.Bottom - rect.Top);
        double scale = Math.Min((double)clientWidth / imageWidth, (double)clientHeight / imageHeight) * state.Zoom;
        int drawWidth = Math.Max(1, (int)(imageWidth * scale));
        int drawHeight = Math.Max(1, (int)(imageHeight * scale));
        int drawX = (clientWidth - drawWidth) / 2;
        int drawY = (clientHeight - drawHeight) / 2;

        GdiPlusMethods.DrawImageRectRectI(
            graphics,
            image,
            drawX,
            drawY,
            drawWidth,
            drawHeight,
            0,
            0,
            (int)imageWidth,
            (int)imageHeight,
            GdiPlusMethods.UnitPixel,
            0,
            0,
            0);
    }

    private static void DrawPageText(nint hdc, ViewerState state)
    {
        string text = $"Page {state.PageNumber} / {state.PageCount}  Zoom {state.ZoomPercent}%";
        NativeMethods.SetBkMode(hdc, NativeMethods.TRANSPARENT);
        NativeMethods.SetTextColor(hdc, 0x00505050);
        NativeMethods.TextOut(hdc, 12, 10, text, text.Length);
    }

    private sealed class ViewerState : IDisposable
    {
        private readonly List<string> _images;
        private int _index;
        private nint _loadedImage;
        private string? _loadedPath;
        private double _zoom = 1.0;

        internal ViewerState(List<string> images)
        {
            _images = images;
        }

        internal int PageNumber => _index + 1;

        internal int PageCount => _images.Count;

        internal double Zoom => _zoom;

        internal int ZoomPercent => (int)Math.Round(_zoom * 100);

        internal void Next()
        {
            if (_index < _images.Count - 1)
            {
                _index++;
            }
        }

        internal void Previous()
        {
            if (_index > 0)
            {
                _index--;
            }
        }

        internal void GoToStart()
        {
            _index = 0;
        }

        internal void GoToEnd()
        {
            _index = Math.Max(0, _images.Count - 1);
        }

        internal void ZoomIn()
        {
            _zoom = Math.Min(5.0, _zoom * 1.25);
        }

        internal void ZoomOut()
        {
            _zoom = Math.Max(0.2, _zoom / 1.25);
        }

        internal void ResetZoom()
        {
            _zoom = 1.0;
        }

        internal nint GetCurrentImage()
        {
            string path = _images[_index];
            if (_loadedImage != 0 && string.Equals(path, _loadedPath, StringComparison.OrdinalIgnoreCase))
            {
                return _loadedImage;
            }

            DisposeLoadedImage();
            if (GdiPlusMethods.LoadImageFromFile(path, out _loadedImage) != GdiPlusMethods.Ok)
            {
                _loadedImage = 0;
                _loadedPath = null;
                return 0;
            }

            _loadedPath = path;
            return _loadedImage;
        }

        public void Dispose()
        {
            DisposeLoadedImage();
        }

        private void DisposeLoadedImage()
        {
            if (_loadedImage != 0)
            {
                GdiPlusMethods.DisposeImage(_loadedImage);
                _loadedImage = 0;
                _loadedPath = null;
            }
        }
    }
}
