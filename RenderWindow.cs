using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

internal static unsafe class RenderWindow
{
    private const string ClassName = "TexListerRenderWindow";
    private const int ScrollStep = 80;
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
                state.Previous();
                NativeMethods.InvalidateRect(hwnd, 0, true);
                break;
            case 0x20: // Space
            case 0x22: // Page Down
                state.Next();
                NativeMethods.InvalidateRect(hwnd, 0, true);
                break;
            case 0x25: // Left
                state.PanBy(-ScrollStep, 0);
                NativeMethods.InvalidateRect(hwnd, 0, true);
                break;
            case 0x26: // Up
                state.PanBy(0, -ScrollStep);
                NativeMethods.InvalidateRect(hwnd, 0, true);
                break;
            case 0x27: // Right
                state.PanBy(ScrollStep, 0);
                NativeMethods.InvalidateRect(hwnd, 0, true);
                break;
            case 0x28: // Down
                state.PanBy(0, ScrollStep);
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
            case 0x4C: // L
                state.RotateLeft();
                NativeMethods.InvalidateRect(hwnd, 0, true);
                break;
            case 0x52: // R
                state.RotateRight();
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
            state.PanBy(0, ScrollStep);
        }
        else
        {
            state.PanBy(0, -ScrollStep);
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
        bool rotated = state.RotationDegrees is 90 or 270;
        uint fitWidth = rotated ? imageHeight : imageWidth;
        uint fitHeight = rotated ? imageWidth : imageHeight;
        double scale = Math.Min((double)clientWidth / fitWidth, (double)clientHeight / fitHeight) * state.Zoom;
        int drawWidth = Math.Max(1, (int)(imageWidth * scale));
        int drawHeight = Math.Max(1, (int)(imageHeight * scale));
        int rotatedWidth = rotated ? drawHeight : drawWidth;
        int rotatedHeight = rotated ? drawWidth : drawHeight;

        state.ClampPan(clientWidth, clientHeight, rotatedWidth, rotatedHeight);

        int drawX = (clientWidth - rotatedWidth) / 2 - state.PanX;
        int drawY = (clientHeight - rotatedHeight) / 2 - state.PanY;

        GdiPlusMethods.TranslateWorldTransform(graphics, drawX + rotatedWidth / 2f, drawY + rotatedHeight / 2f, 0);
        GdiPlusMethods.RotateWorldTransform(graphics, state.RotationDegrees, 0);

        GdiPlusMethods.DrawImageRectRectI(
            graphics,
            image,
            -drawWidth / 2,
            -drawHeight / 2,
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

        GdiPlusMethods.ResetWorldTransform(graphics);
    }

    private static void DrawPageText(nint hdc, ViewerState state)
    {
        string text = $"Page {state.PageNumber} / {state.PageCount}  Zoom {state.ZoomPercent}%  Rotate {state.RotationDegrees}°";
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
        private int _panX;
        private int _panY;
        private int _rotationDegrees;

        internal ViewerState(List<string> images)
        {
            _images = images;
        }

        internal int PageNumber => _index + 1;

        internal int PageCount => _images.Count;

        internal double Zoom => _zoom;

        internal int ZoomPercent => (int)Math.Round(_zoom * 100);

        internal int PanX => _panX;

        internal int PanY => _panY;

        internal int RotationDegrees => _rotationDegrees;

        internal void Next()
        {
            if (_index < _images.Count - 1)
            {
                _index++;
                ResetViewOffset();
            }
        }

        internal void Previous()
        {
            if (_index > 0)
            {
                _index--;
                ResetViewOffset();
            }
        }

        internal void GoToStart()
        {
            _index = 0;
            ResetViewOffset();
        }

        internal void GoToEnd()
        {
            _index = Math.Max(0, _images.Count - 1);
            ResetViewOffset();
        }

        internal void ZoomIn()
        {
            _zoom = Math.Min(5.0, _zoom * 1.25);
        }

        internal void ZoomOut()
        {
            _zoom = Math.Max(0.2, _zoom / 1.25);
            ClampPanToZeroIfFit();
        }

        internal void ResetZoom()
        {
            _zoom = 1.0;
            _panX = 0;
            _panY = 0;
        }

        internal void PanBy(int dx, int dy)
        {
            _panX += dx;
            _panY += dy;
        }

        internal void RotateLeft()
        {
            _rotationDegrees = (_rotationDegrees + 270) % 360;
            _panX = 0;
            _panY = 0;
        }

        internal void RotateRight()
        {
            _rotationDegrees = (_rotationDegrees + 90) % 360;
            _panX = 0;
            _panY = 0;
        }

        internal void ClampPan(int clientWidth, int clientHeight, int contentWidth, int contentHeight)
        {
            int maxPanX = Math.Max(0, (contentWidth - clientWidth) / 2);
            int maxPanY = Math.Max(0, (contentHeight - clientHeight) / 2);
            _panX = Math.Clamp(_panX, -maxPanX, maxPanX);
            _panY = Math.Clamp(_panY, -maxPanY, maxPanY);
        }

        private void ResetViewOffset()
        {
            _panX = 0;
            _panY = 0;
        }

        private void ClampPanToZeroIfFit()
        {
            if (_zoom <= 1.0)
            {
                _panX = 0;
                _panY = 0;
            }
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
