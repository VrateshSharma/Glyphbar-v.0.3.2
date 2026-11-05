using Glyphbar.Platform.Interop;

namespace Glyphbar.Platform.Windowing;

public static class WindowStyler
{
    /// <summary>Keeps a window out of Alt-Tab and the taskbar.</summary>
    public static void MakeToolWindow(nint hwnd)
    {
        long ex = Native.GetWindowLongPtr(hwnd, Native.GWL_EXSTYLE);
        ex = (ex | Native.WS_EX_TOOLWINDOW) & ~Native.WS_EX_APPWINDOW;
        Native.SetWindowLongPtr(hwnd, Native.GWL_EXSTYLE, (nint)ex);
        Native.SetWindowPos(hwnd, 0, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE | Native.SWP_FRAMECHANGED);
    }

    // Rooted for the lifetime of the process: the OS holds the only other reference.
    private static readonly Native.SubclassProc FramelessProc = OnFramelessMessage;

    /// <summary>
    /// Removes the non-client area and extends the DWM frame across the window, so per-pixel alpha from XAML reaches
    /// the desktop. WinUI's presenter re-applies WS_DLGFRAME after any style change, so clearing style bits does not
    /// last; answering WM_NCCALCSIZE keeps the frame gone whatever the style says.
    /// </summary>
    public static void MakeFrameless(nint hwnd)
    {
        Native.SetWindowSubclass(hwnd, FramelessProc, 2, 0);
        var margins = new Native.MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        Native.DwmExtendFrameIntoClientArea(hwnd, ref margins);
        Native.SetWindowPos(hwnd, 0, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOZORDER | Native.SWP_NOACTIVATE | Native.SWP_FRAMECHANGED);
    }

    private static nint OnFramelessMessage(nint hwnd, uint msg, nint wParam, nint lParam, nuint id, nuint data)
    {
        switch ((int)msg)
        {
            case Native.WM_NCCALCSIZE when wParam != 0:
                return 0;
            case Native.WM_NCPAINT:
                return 0;
            case Native.WM_NCACTIVATE:
                return 1;
        }
        return Native.DefSubclassProc(hwnd, msg, wParam, lParam);
    }

    /// <summary>Removes the DWM border and rounding so XAML alone draws the window's shape.</summary>
    public static void ClearFrame(nint hwnd, bool round = false)
    {
        int corner = round ? Native.DWMWCP_ROUND : Native.DWMWCP_DONOTROUND;
        Native.DwmSetWindowAttribute(hwnd, Native.DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
        int border = Native.DWMWA_COLOR_NONE;
        Native.DwmSetWindowAttribute(hwnd, Native.DWMWA_BORDER_COLOR, ref border, sizeof(int));
    }

    public static void SetTopmost(nint hwnd, bool topmost) =>
        Native.SetWindowPos(hwnd, topmost ? Native.HWND_TOPMOST : Native.HWND_NOTOPMOST, 0, 0, 0, 0,
            Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);

    /// <summary>Clips the window to a rounded rectangle; a radius of 0 clears the clip.</summary>
    public static void SetRoundedRegion(nint hwnd, int width, int height, int radiusPixels)
    {
        if (radiusPixels <= 0)
        {
            Native.SetWindowRgn(hwnd, 0, true);
            return;
        }
        // The OS owns the region after a successful SetWindowRgn.
        nint region = Native.CreateRoundRectRgn(0, 0, width + 1, height + 1, radiusPixels * 2, radiusPixels * 2);
        if (Native.SetWindowRgn(hwnd, region, true) == 0)
            Native.DeleteObject(region);
    }
}
