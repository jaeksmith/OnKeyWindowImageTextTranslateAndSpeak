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

    public static Bitmap? CaptureWindow(IntPtr handle)
    {
        try
        {
            if (!GetWindowRect(handle, out RECT rect))
            {
                Debug.WriteLine("GetWindowRect failed");
                return null;
            }

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;

            if (width <= 0 || height <= 0)
            {
                Debug.WriteLine($"Invalid window dimensions: {width}x{height}");
                return null;
            }

            var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
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
            return bmp;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error in CaptureWindow: {ex}");
            return null;
        }
    }

    private static IntPtr FindWindowByTitle(string titlePart)
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
                        if (!string.IsNullOrEmpty(windowTitle) && 
                            windowTitle.IndexOf(titlePart, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            foundWindow = hWnd;
                            return false; // Stop enumeration
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
            var window = FindWindowByTitle(Config.TargetWindowTitle);
            if (window == IntPtr.Zero)
            {
                Console.WriteLine($"Could not find window with title containing: {Config.TargetWindowTitle}");
                return null;
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
