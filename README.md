# Window Text Translator and Speaker

A Windows application that captures the active window, extracts text using ChatGPT's vision capabilities, translates it to English (if needed), and reads it aloud using text-to-speech.

## Features

- Captures the active window when a hotkey is pressed
- Extracts text using ChatGPT's vision capabilities
- Translates text to English if it's in another language
- Reads the extracted text aloud using Windows text-to-speech
- Configurable target window title and hotkey
- Runs in the background

## Prerequisites

- .NET 8.0 or later
- A valid OpenAI API key with access to the GPT-4 Vision model
- Windows 10 or later

## Setup

1. Clone or download this repository
2. Create a file named `ChatGPT.API-Key` in the application directory and paste your OpenAI API key in it (no quotes or additional text)
3. (Optional) Modify the `appsettings.json` file to change the default settings:
   - `TargetWindowTitle`: The window title to look for (default: "VRChat")
   - `Hotkey`: The hotkey combination to trigger the capture (default: "Control+F1")
   - `Model`: The OpenAI model to use (default: "gpt-4o", required for image/OCR support as of June 2024)
   - `SpeechRate`: The speech rate (1.0 is normal, 2.0 is twice as fast, 0.5 is half as fast)
   - `SpeechVolume`: The speech volume (0-100)
   - `MaxTokens`: Maximum number of tokens to generate (default: 1000)
   - `Temperature`: Controls randomness (0.0 to 2.0, default: 0.7)

## Usage

1. Run the application
2. Make sure the target window (default: any window with "VRChat" in the title) is active
3. Press the configured hotkey (default: Ctrl+F1)
4. The application will:
   - Capture the active window
   - Send the image to ChatGPT for text extraction and translation
   - Display the extracted text in the console
   - Read the text aloud using text-to-speech

## Building from Source

1. Install the .NET 8.0 SDK
2. Clone this repository
3. Run `dotnet build` in the project directory
4. The executable will be in the `bin/Debug/net8.0` or `bin/Release/net8.0` directory

## Notes

- The application requires an internet connection to use the ChatGPT API
- The first run may take longer as it needs to download the required NuGet packages
- Make sure the target window is not minimized when capturing
- The application will only capture the active window if its title contains the configured target text (case-insensitive)

## License

This project is open source and available under the [MIT License](LICENSE).
