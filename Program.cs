using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using System.IO;

class Program
{
    private static HotkeyManager? _hotkey;
    private static bool _isProcessing = false;
    private static readonly object _processingLock = new object();

    [STAThread]
    static async Task Main(string[] args)
    {
        Console.WriteLine("[START] Main entry point reached");
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Console.WriteLine("[STARTUP] Windows Forms visual styles enabled");

        try
        {
            Console.WriteLine("[STARTUP] Creating HotkeyManager instance");
            _hotkey = new HotkeyManager();
            Console.WriteLine("Window Text Translator and Speaker");
            Console.WriteLine("--------------------------------");
            Console.WriteLine($"Target Window: {Config.TargetWindowTitle}");
            Console.WriteLine($"Hotkey: {Config.Hotkey}");
            Console.WriteLine("Press Ctrl+C to exit\n");

            // Parse and register the hotkey
            Console.WriteLine("[STARTUP] Parsing hotkey from config");
            if (!HotkeyManager.TryParseHotkey(Config.Hotkey, out uint modifiers, out var key))
            {
                Console.WriteLine($"Error: Invalid hotkey format: {Config.Hotkey}");
                Console.WriteLine("Using default hotkey: Control+F1");
                key = Keys.F1;
                modifiers = HotkeyManager.MODIFIER_CONTROL;
            }

            try
            {
                Action hotkeyAction = async () =>
                {
                    Console.WriteLine("[HOTKEY] Hotkey pressed. Starting capture/translation pipeline...");
                    try
                    {
                        await ProcessWindowCapture().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in hotkey handler: {ex}");
                    }
                };

                _hotkey.RegisterHotKey(modifiers, key, hotkeyAction);

                Console.WriteLine($"Successfully registered hotkey: {modifiers}+{key}");
                Console.WriteLine("Press the hotkey to capture and translate the active window.\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to register hotkey: {ex.Message}");
                Console.WriteLine("Please try a different key combination or close the application that might be using this hotkey.");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            // Create a hidden form to keep the application running
            using (var form = new Form())
            {
                form.ShowInTaskbar = false;
                form.WindowState = FormWindowState.Minimized;
                form.FormClosing += (s, e) => _hotkey?.Dispose();

                // Set up console close handler
                Console.CancelKeyPress += (s, e) =>
                {
                    e.Cancel = true;
                    form.Close();
                };

                // Run the application with the hidden form
                Application.Run(form);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
        finally
        {
            _hotkey?.Dispose();
        }
    }

    private static async Task ProcessWindowCapture()
    {
        if (_isProcessing)
        {
            string msg = "Already processing a request. Please wait...";
            Console.WriteLine(msg);
            // No need to speak here; just return
            return;
        }
        
        _isProcessing = true;
        string captureFile = string.Empty;

        // Speak initial operation status
        await TextToSpeechService.SpeakAsync("Capturing and requesting translation...");
        try
        {
            try
            {
                // Delete previous last_captured.png before capturing
                string lastCapturePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "last_captured.png");
                if (File.Exists(lastCapturePath))
                {
                    try { File.Delete(lastCapturePath); } catch { /* Ignore cleanup errors */ }
                }

                captureFile = WindowCapture.CaptureActiveWindowToTempFile();
                if (string.IsNullOrEmpty(captureFile))
                {
                    string msg = "Capture error. Failed to capture window. Make sure the target window is visible.";
                    Console.WriteLine("[CAPTURE ERROR] " + msg);
                    await TextToSpeechService.SpeakAsync(msg);
                    return;
                }

                // Save a copy of the captured image for user review
                try
                {
                    File.Copy(captureFile, lastCapturePath, true);
                    Console.WriteLine($"[CAPTURE] Saved last capture to {lastCapturePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CAPTURE] Warning: Could not save last_captured.png: {ex.Message}");
                }

                Console.WriteLine("[CAPTURE] Window captured. Processing...");
            
                // Window title check and translation
                string activeWindowTitle = WindowCapture.GetActiveWindowTitle();
                if (string.IsNullOrEmpty(activeWindowTitle) ||
                    !activeWindowTitle.Contains(Config.TargetWindowTitle, StringComparison.OrdinalIgnoreCase))
                {
                    string msg = $"The active window does not contain '{Config.TargetWindowTitle}'. Searching for a matching window...";
                    Console.WriteLine($"[WINDOW CHECK] {msg}");
                    // Only log, do not speak here

                    // Try to find the first window with the configured title
                    IntPtr foundWindow = WindowCapture.FindWindowByTitle(Config.TargetWindowTitle, exactMatch: true);
                    if (foundWindow == IntPtr.Zero)
                    {
                        string notFoundMsg = $"No window found with title containing '{Config.TargetWindowTitle}'. Aborting.";
                        Console.WriteLine($"[WINDOW CHECK] {notFoundMsg}");
                        await TextToSpeechService.SpeakAsync(notFoundMsg);
                        return;
                    }
                    else
                    {
                        string foundMsg = $"Found window with title containing '{Config.TargetWindowTitle}'. Capturing that window.";
                        Console.WriteLine($"[WINDOW CHECK] {foundMsg}");
                        // Only log, do not speak here

                        // Capture the found window instead
                        using (var bitmap = WindowCapture.CaptureWindow(foundWindow))
                        {
                            if (bitmap == null)
                            {
                                string failMsg = "Failed to capture the found window.";
                                Console.WriteLine($"[CAPTURE ERROR] {failMsg}");
                                await TextToSpeechService.SpeakAsync(failMsg);
                                return;
                            }
                            // Save to temp and to last_captured.png
                            string tempFile = Path.Combine(Path.GetTempPath(), $"capture_{Guid.NewGuid()}.png");
                            try
                            {
                                bitmap.Save(tempFile, System.Drawing.Imaging.ImageFormat.Png);
                                File.Copy(tempFile, lastCapturePath, true);
                                Console.WriteLine($"[CAPTURE] Saved fallback capture to {lastCapturePath}");
                                captureFile = tempFile;
                            }
                            catch (Exception ex)
                            {
                                string saveFailMsg = $"Error saving fallback capture: {ex.Message}";
                                Console.WriteLine($"[CAPTURE ERROR] {saveFailMsg}");
                                await TextToSpeechService.SpeakAsync(saveFailMsg);
                                return;
                            }
                        }
                    }
                }

                Console.WriteLine("[TRANSLATION] Sending image to ChatGPT for OCR & translation...");
                string extractedText = await ChatGptService.GetTextFromImageAsync(captureFile);

                if (string.IsNullOrWhiteSpace(extractedText))
                {
                    string msg = "No text was extracted from the window.";
                    Console.WriteLine("[TRANSLATION] " + msg);
                    await TextToSpeechService.SpeakAsync(msg);
                    return;
                }

                Console.WriteLine("[TRANSLATION] Extracted Text:");
                Console.WriteLine("---------------");
                Console.WriteLine(extractedText);
                Console.WriteLine("---------------\n");

                Console.WriteLine("[TTS] Speaking the extracted text...");
                await TextToSpeechService.SpeakAsync(extractedText);
            }
            finally
            {
                // Clean up the temporary file
                if (!string.IsNullOrEmpty(captureFile) && File.Exists(captureFile))
                {
                    try { File.Delete(captureFile); } catch { /* Ignore */ }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] {ex.Message}");
        }
        finally
        {
            lock (_processingLock)
            {
                _isProcessing = false;
            }
        }
    }
}
