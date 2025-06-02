using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;

public static class WindowCapture
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDC(IntPtr hWnd);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdc, int nXDest, int nYDest, int nWidth, int nHeight, 
        IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hObject);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(IntPtr hdc);

    private const int PW_RENDERFULLCONTENT = 0x00000002;

    public static string? GetActiveWindowTitle()
    {
        try
        {
            const int nChars = 256;
            StringBuilder buff = new StringBuilder(nChars);
            IntPtr handle = GetForegroundWindow();

            if (handle == IntPtr.Zero)
                return null;

            int length = GetWindowText(handle, buff, nChars);
            if (length > 0)
            {
                return buff.ToString();
            }
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in GetActiveWindowTitle: {ex}");
            return null;
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    public static Bitmap? CaptureWindow(IntPtr handle)
    {
        try
        {
            if (!GetWindowRect(handle, out RECT windowRect))
            {
                Debug.WriteLine("GetWindowRect failed");
                return null;
            }

            // Get client area rectangle (relative to window)
            if (!GetClientRect(handle, out RECT clientRect))
            {
                Debug.WriteLine("GetClientRect failed");
                return null;
            }
            POINT clientTopLeft = new POINT { X = clientRect.Left, Y = clientRect.Top };
            if (!ClientToScreen(handle, ref clientTopLeft))
            {
                Debug.WriteLine("ClientToScreen failed");
                return null;
            }
            int clientWidth = clientRect.Right - clientRect.Left;
            int clientHeight = clientRect.Bottom - clientRect.Top;

            int windowWidth = windowRect.Right - windowRect.Left;
            int windowHeight = windowRect.Bottom - windowRect.Top;

            if (windowWidth <= 0 || windowHeight <= 0 || clientWidth <= 0 || clientHeight <= 0)
            {
                Debug.WriteLine($"Invalid window/client dimensions: {windowWidth}x{windowHeight}, {clientWidth}x{clientHeight}");
                return null;
            }

            // Capture full window
            var bmp = new Bitmap(windowWidth, windowHeight, PixelFormat.Format32bppArgb);
            using (Graphics gfxBmp = Graphics.FromImage(bmp))
            {
                IntPtr hdcBitmap = gfxBmp.GetHdc();
                try
                {
                    if (!PrintWindow(handle, hdcBitmap, PW_RENDERFULLCONTENT))
                    {
                        Debug.WriteLine("PrintWindow failed");
                        bmp.Dispose();
                        return null;
                    }
                }
                finally
                {
                    gfxBmp.ReleaseHdc(hdcBitmap);
                }
            }

            // Crop to client area only
            int cropX = clientTopLeft.X - windowRect.Left;
            int cropY = clientTopLeft.Y - windowRect.Top;
            Rectangle cropRect = new Rectangle(cropX, cropY, clientWidth, clientHeight);
            Bitmap clientBmp = bmp.Clone(cropRect, bmp.PixelFormat);
            bmp.Dispose();
            return clientBmp;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in CaptureWindow: {ex}");
            return null;
        }
    }

    public static IntPtr FindWindowByTitle(string titlePart, bool exactMatch = false)
    {
        try
        {
            if (string.IsNullOrEmpty(titlePart))
                return IntPtr.Zero;

            IntPtr foundWindow = IntPtr.Zero;
            
            // Create a delegate for the callback
            EnumWindowsProc callback = (hWnd, lParam) =>
            {
                try
                {
                    const int nChars = 256;
                    StringBuilder buff = new StringBuilder(nChars);
                    if (GetWindowText(hWnd, buff, nChars) > 0)
                    {
                        string windowTitle = buff.ToString();
                        if (!string.IsNullOrEmpty(windowTitle))
                        {
                            if (exactMatch)
                            {
                                if (string.Equals(windowTitle, titlePart, StringComparison.OrdinalIgnoreCase))
                                {
                                    foundWindow = hWnd;
                                    return false; // Stop enumeration
                                }
                            }
                            else
                            {
                                if (windowTitle.IndexOf(titlePart, StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    foundWindow = hWnd;
                                    return false; // Stop enumeration
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in window enumeration: {ex}");
                }
                return true; // Continue enumeration
            };
            
            // Call EnumWindows with the callback
            EnumWindows(callback, IntPtr.Zero);
            return foundWindow;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in FindWindowByTitle: {ex}");
            return IntPtr.Zero;
        }
    }

    public static string? CaptureActiveWindowToTempFile()
    {
        try
        {
            var window = FindWindowByTitle(Config.TargetWindowTitle, exactMatch: true);
            if (window == IntPtr.Zero)
            {
                Console.WriteLine($"Could not find window with title containing: {Config.TargetWindowTitle}");
                return null;
            }

            // Restore window if minimized (iconified)
            if (IsIconic(window))
            {
                ShowWindow(window, SW_RESTORE);
                // Give Windows a moment to redraw/restack
                System.Threading.Thread.Sleep(200);
            }

            using var bitmap = CaptureWindow(window);
            if (bitmap == null)
                return null;

            string tempFile = Path.Combine(Path.GetTempPath(), $"capture_{Guid.NewGuid()}.png");
            try
            {
                bitmap.Save(tempFile, ImageFormat.Png);
                return tempFile;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving capture: {ex.Message}");
                try { File.Delete(tempFile); } catch { /* Ignore cleanup errors */ }
                return null;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in CaptureActiveWindowToTempFile: {ex}");
            return null;
        }
    }
}
