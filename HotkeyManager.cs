using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public class HotkeyManager : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    
    [DllImport("kernel32.dll")]
    private static extern uint GetLastError();
    
    // Wrapper class for P/Invoke methods
    private static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }

    // Modifier constants for hotkey registration (use these instead of ModifierKeys enum)
    public const uint MODIFIER_NONE    = 0x0000;
    public const uint MODIFIER_ALT     = 0x0001;
    public const uint MODIFIER_CONTROL = 0x0002;
    public const uint MODIFIER_SHIFT   = 0x0004;
    public const uint MODIFIER_WIN     = 0x0008;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const int WM_HOTKEY = 0x0312;

    private class HotkeyWindow : Form
    {
        private const int WM_HOTKEY = 0x0312;
        private readonly Dictionary<int, Action> _callbacks = new Dictionary<int, Action>();
        private int _currentId = 0;

        public HotkeyWindow()
        {
            // Make the form invisible
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            this.ShowIcon = false;
            this.Opacity = 0;
            this.FormBorderStyle = FormBorderStyle.None;
            this.Size = new Size(0, 0);
            this.Shown += (s, e) => this.Hide();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (_callbacks.TryGetValue(id, out Action callback))
                {
                    callback();
                }
            }
            base.WndProc(ref m);
        }

        public int RegisterHotKey(uint modifiers, Keys key, Action callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            int id = _currentId++;
            uint fsModifiers = 0;

            // Convert modifier constants to Windows API constants
            if ((modifiers & MODIFIER_ALT) != 0) fsModifiers |= MOD_ALT;
            if ((modifiers & MODIFIER_CONTROL) != 0) fsModifiers |= MOD_CONTROL;
            if ((modifiers & MODIFIER_SHIFT) != 0) fsModifiers |= MOD_SHIFT;
            // Note: Win key is not supported in this implementation

            bool success = NativeMethods.RegisterHotKey(Handle, id, fsModifiers, (uint)key);
            if (!success)
            {
                int error = Marshal.GetLastWin32Error();
                if (error == 1409) // ERROR_HOTKEY_ALREADY_REGISTERED
                {
                    throw new InvalidOperationException($"The hotkey is already registered by another application. Try a different key combination.");
                }
                throw new InvalidOperationException($"Failed to register hotkey (Error code: {error}). Try a different key combination.");
            }
            _callbacks[id] = callback;
            return id;
        }

        public void UnregisterHotKey(int id)
        {
            if (_callbacks.Remove(id))
            {
                // Use the P/Invoke UnregisterHotKey method through the wrapper
                bool success = NativeMethods.UnregisterHotKey(Handle, id);
                if (!success)
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error != 0) // Only log actual errors (0 means success)
                    {
                        Debug.WriteLine($"Failed to unregister hotkey (Error code: {error})");
                    }
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Unregister all hotkeys when the form is closing
            foreach (var id in _callbacks.Keys.ToList())
            {
                UnregisterHotKey(id);
            }
            _callbacks.Clear();
            
            base.OnFormClosing(e);
        }
    }

    private HotkeyWindow _window = new HotkeyWindow();
    private readonly Dictionary<Keys, int> _registeredHotkeys = new Dictionary<Keys, int>();
    private int _currentId = 0x0000; // Application-defined identifier of the hotkey

    public void RegisterHotKey(uint modifiers, Keys key, Action callback)
    {
        if (callback == null)
            throw new ArgumentNullException(nameof(callback));

        try
        {
            int id = _window.RegisterHotKey(modifiers, key, callback);
            _registeredHotkeys[key] = id;
            Debug.WriteLine($"Registered hotkey: {modifiers}+{key} (ID: {id})");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to register hotkey {modifiers}+{key}: {ex.Message}");
            throw;
        }
    }

    public void UnregisterHotKey(Keys key)
    {
        if (_registeredHotkeys.TryGetValue(key, out int id))
        {
            _window.UnregisterHotKey(id);
            _registeredHotkeys.Remove(key);
            Debug.WriteLine($"Unregistered hotkey: {key} (ID: {id})");
        }
    }

    public static bool TryParseHotkey(string hotkeyString, out uint modifiers, out Keys key)
    {
        modifiers = MODIFIER_NONE;
        key = Keys.None;

        if (string.IsNullOrWhiteSpace(hotkeyString))
            return false;

        var parts = hotkeyString.Split('+');
        if (parts.Length == 0)
            return false;

        foreach (var part in parts.Take(parts.Length - 1))
        {
            var partLower = part.Trim().ToLower();
            switch (partLower)
            {
                case "ctrl":
                case "control":
                    modifiers |= MODIFIER_CONTROL;
                    break;
                case "alt":
                    modifiers |= MODIFIER_ALT;
                    break;
                case "shift":
                    modifiers |= MODIFIER_SHIFT;
                    break;
                case "win":
                case "windows":
                    modifiers |= MODIFIER_WIN;
                    break;
            }
        }

        string keyPart = parts.Last().Trim();
        if (Enum.TryParse(keyPart, true, out Keys parsedKey))
        {
            key = parsedKey;
            return true;
        }
        else if (keyPart.Length == 1 && char.IsLetterOrDigit(keyPart[0]))
        {
            key = (Keys)char.ToUpper(keyPart[0]);
            return true;
        }
        else if (keyPart.StartsWith("F", StringComparison.OrdinalIgnoreCase) && int.TryParse(keyPart.Substring(1), out int fKey) && fKey >= 1 && fKey <= 24)
        {
            key = Keys.F1 + (fKey - 1);
            return true;
        }

        return false;
    }

    public void Dispose()
    {
        if (_window != null)
        {
            foreach (var key in _registeredHotkeys.Keys.ToList())
            {
                UnregisterHotKey(key);
            }
            _window.Close();
            _window.Dispose();
            _window = null;
        }
    }

    ~HotkeyManager()
    {
        Dispose();
    }
}
