using Microsoft.Extensions.Configuration;
using System;
using System.IO;

public static class Config
{
    public static string ApiKey { get; private set; }
    public static string TargetWindowTitle { get; private set; }
    public static string Hotkey { get; private set; }
    public static string Model { get; private set; }
    public static double SpeechRate { get; private set; }
    public static int SpeechVolume { get; private set; }
    public static int MaxTokens { get; private set; }
    public static double Temperature { get; private set; }

    static Config()
    {
        try
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            var configuration = builder.Build();

            // Load API key from file
            var apiKeyPath = Path.Combine(Directory.GetCurrentDirectory(), "ChatGPT.API-Key");
            if (!File.Exists(apiKeyPath))
            {
                throw new FileNotFoundException("ChatGPT API key file not found. Please create a file named 'ChatGPT.API-Key' in the application directory with your API key.");
            }
            ApiKey = File.ReadAllText(apiKeyPath).Trim();

            // Load other settings
            var appSettings = configuration.GetSection("AppSettings");
            TargetWindowTitle = appSettings["TargetWindowTitle"] ?? "VRChat";
            Hotkey = appSettings["Hotkey"] ?? "Control+F1";
            Model = appSettings["Model"] ?? "gpt-4o";
            
            if (!double.TryParse(appSettings["SpeechRate"], out double speechRate))
                speechRate = 1.25;
            SpeechRate = Math.Clamp(speechRate, 0.5, 3.0);
            
            if (!int.TryParse(appSettings["SpeechVolume"], out int volume))
                volume = 100;
            SpeechVolume = Math.Clamp(volume, 0, 100);
            
            if (!int.TryParse(appSettings["MaxTokens"], out int maxTokens))
                maxTokens = 1000;
            MaxTokens = Math.Clamp(maxTokens, 100, 4000);
            
            if (!double.TryParse(appSettings["Temperature"], out double temperature))
                temperature = 0.7;
            Temperature = Math.Clamp(temperature, 0.0, 2.0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading configuration: {ex.Message}");
            throw;
        }
    }
}
