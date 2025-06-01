using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public static class ChatGptService
{
    private static readonly HttpClient _httpClient = new HttpClient();
    private const string ApiUrl = "https://api.openai.com/v1/chat/completions";

    public static async Task<string> GetTextFromImageAsync(string imagePath, string targetLanguage = "English")
    {
        try
        {
            // Read the image file and convert to base64
            byte[] imageBytes = await File.ReadAllBytesAsync(imagePath);
            string base64Image = Convert.ToBase64String(imageBytes);

            // Detect MIME type from file extension
            string extension = Path.GetExtension(imagePath).ToLower();
            string mimeType = extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".bmp" => "image/bmp",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "image/png"
            };
            string dataUrl = $"data:{mimeType};base64,{base64Image}";

            // Prepare the request payload
            var request = new
            {
                model = Config.Model,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = $"Extract all text from this image and translate it to {targetLanguage}. If the text is already in {targetLanguage}, just return it. If there are multiple languages, translate all to {targetLanguage}. If the text is not in a language you recognize, try to transliterate it. If the text is in multiple orientations, handle each one. Return only the translated text, no additional commentary or formatting." },
                            new { type = "image_url", image_url = new { url = dataUrl, detail = "auto" } }
                        }
                    }
                },
                max_tokens = Config.MaxTokens,
                temperature = Config.Temperature
            };

            // Serialize and sanitize the request for logging
            string rawRequest = JsonSerializer.Serialize(request);
            string sanitizedRequest = rawRequest
                .Replace(Config.ApiKey, "###API-KEY-REMOVED###");

            // Remove base64 image data from the log (replace with tag)
            sanitizedRequest = System.Text.RegularExpressions.Regex.Replace(
                sanitizedRequest,
                @"data:[^;]+;base64,[^""]+",
                "data:###IMAGE-DATA-REMOVED###"
            );

            Console.WriteLine("[API REQUEST] Sending request to OpenAI:");
            Console.WriteLine(sanitizedRequest);

            var content = new StringContent(rawRequest, Encoding.UTF8, "application/json");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Config.ApiKey);

            var response = await _httpClient.PostAsync(ApiUrl, content);
            string responseContent = await response.Content.ReadAsStringAsync();

            // Sanitize response for logging
            string sanitizedResponse = responseContent;
            // Remove session tokens if present (replace with tag)
            sanitizedResponse = System.Text.RegularExpressions.Regex.Replace(
                sanitizedResponse,
                @"(""access_token"":"")[^""]+",
                "$1###SESSION-TOKEN-REMOVED###"
            );
            // Remove base64 image data if echoed (rare)
            sanitizedResponse = System.Text.RegularExpressions.Regex.Replace(
                sanitizedResponse,
                @"data:[^;]+;base64,[^""]+",
                "data:###IMAGE-DATA-REMOVED###"
            );
            // Remove API key if echoed (very rare)
            sanitizedResponse = sanitizedResponse.Replace(Config.ApiKey, "###API-KEY-REMOVED###");

            Console.WriteLine("[API RESPONSE] Response from OpenAI:");
            Console.WriteLine(sanitizedResponse);

            response.EnsureSuccessStatusCode();

            // Parse the response
            using JsonDocument doc = JsonDocument.Parse(responseContent);
            var choices = doc.RootElement.GetProperty("choices");
            if (choices.GetArrayLength() == 0)
                return "No text was extracted from the image.";

            var message = choices[0].GetProperty("message");
            return message.GetProperty("content").GetString()?.Trim() ?? "No text was extracted from the image.";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing image with ChatGPT: {ex.Message}");
            return $"Error: {ex.Message}";
        }
    }
}
