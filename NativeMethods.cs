using System.Runtime.InteropServices;

internal static partial class NativeMethods
{
    internal const int CS_HREDRAW = 0x0002;
    internal const int CS_VREDRAW = 0x0001;
    internal const int WS_CHILD = 0x40000000;
    internal const int WS_VISIBLE = 0x10000000;
    internal const int WS_VSCROLL = 0x00200000;
    internal const int WS_HSCROLL = 0x00100000;
    internal const int ES_LEFT = 0x0000;
    internal const int ES_MULTILINE = 0x0004;
    internal const int ES_AUTOVSCROLL = 0x0040;
    internal const int ES_AUTOHSCROLL = 0x0080;
    internal const int ES_READONLY = 0x0800;
    internal const int WM_SETFONT = 0x0030;
    internal const int WM_SETTEXT = 0x000C;
    internal const int WM_SETREDRAW = 0x000B;
    internal const int WM_PAINT = 0x000F;
    internal const int WM_DESTROY = 0x0002;
    internal const int WM_KEYDOWN = 0x0100;
    internal const int WM_LBUTTONDOWN = 0x0201;
    internal const int WM_MOUSEWHEEL = 0x020A;
    internal const int EM_SETLIMITTEXT = 0x00C5;
    internal const int DEFAULT_GUI_FONT = 17;
    internal const int ANSI_FIXED_FONT = 11;
    internal const int COLOR_WINDOW = 5;
    internal const int IDC_ARROW = 32512;
    internal const int TRANSPARENT = 1;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal unsafe struct WNDCLASSEX
    {
        internal uint cbSize;
        internal uint style;
        internal nint lpfnWndProc;
        internal int cbClsExtra;
        internal int cbWndExtra;
        internal nint hInstance;
        internal nint hIcon;
        internal nint hCursor;
        internal nint hbrBackground;
        internal char* lpszMenuName;
        internal char* lpszClassName;
        internal nint hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PAINTSTRUCT
    {
        internal nint hdc;
        internal int fErase;
        internal RECT rcPaint;
        internal int fRestore;
        internal int fIncUpdate;
        internal long rgbReserved1;
        internal long rgbReserved2;
        internal long rgbReserved3;
        internal long rgbReserved4;
    }

    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", SetLastError = true)]
    internal static extern nint CreateWindowEx(
        int dwExStyle,
        [MarshalAs(UnmanagedType.LPWStr)] string lpClassName,
        [MarshalAs(UnmanagedType.LPWStr)] string? lpWindowName,
        int dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        nint hWndParent,
        nint hMenu,
        nint hInstance,
        nint lpParam);

    [DllImport("user32.dll", EntryPoint = "RegisterClassExW", SetLastError = true)]
    internal static extern unsafe ushort RegisterClassEx(WNDCLASSEX* lpwcx);

    [DllImport("user32.dll", EntryPoint = "DefWindowProcW")]
    internal static extern nint DefWindowProc(nint hWnd, uint msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    internal static extern nint BeginPaint(nint hWnd, out PAINTSTRUCT lpPaint);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EndPaint(nint hWnd, ref PAINTSTRUCT lpPaint);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetClientRect(nint hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool InvalidateRect(nint hWnd, nint lpRect, [MarshalAs(UnmanagedType.Bool)] bool bErase);

    [DllImport("user32.dll")]
    internal static extern nint SetFocus(nint hWnd);

    [DllImport("user32.dll", EntryPoint = "LoadCursorW", SetLastError = true)]
    internal static extern nint LoadCursor(nint hInstance, nint lpCursorName);

    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", SetLastError = true)]
    internal static extern nint GetModuleHandle([MarshalAs(UnmanagedType.LPWStr)] string? lpModuleName);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyWindow(nint hWnd);

    [DllImport("user32.dll", EntryPoint = "SendMessageW", SetLastError = true)]
    internal static extern nint SendMessage(nint hWnd, int msg, nint wParam, nint lParam);

    [DllImport("user32.dll", EntryPoint = "SendMessageW", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern nint SendMessageText(nint hWnd, int msg, nint wParam, string lParam);

    [DllImport("gdi32.dll")]
    internal static extern nint GetStockObject(int i);

    [DllImport("gdi32.dll")]
    internal static extern int SetBkMode(nint hdc, int mode);

    [DllImport("gdi32.dll")]
    internal static extern uint SetTextColor(nint hdc, uint color);

    [DllImport("gdi32.dll", EntryPoint = "TextOutW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool TextOut(nint hdc, int x, int y, string lpString, int c);
}
