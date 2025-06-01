using System.Speech.Synthesis;
using System.Threading.Tasks;

public static class TextToSpeechService
{
    private static readonly SpeechSynthesizer _synthesizer = new SpeechSynthesizer();
    private static bool _isInitialized = false;

    public static void Initialize()
    {
        if (!_isInitialized)
        {
            _synthesizer.Volume = Config.SpeechVolume;
            _synthesizer.Rate = (int)((Config.SpeechRate - 1.0) * 10); // Convert from 0.5-2.0 to -5 to 10
            
            // Try to set a female voice if available
            foreach (var voice in _synthesizer.GetInstalledVoices())
            {
                if (voice.VoiceInfo.Gender == VoiceGender.Female)
                {
                    _synthesizer.SelectVoice(voice.VoiceInfo.Name);
                    break;
                }
            }
            
            _isInitialized = true;
        }
    }

    public static async Task SpeakAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        Initialize();
        
        // Print to console before speaking
        System.Console.WriteLine($"[TTS] {text}");

        await Task.Run(() =>
        {
            try
            {
                _synthesizer.Speak(text);
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine($"[TTS Error] {ex.Message}");
            }
        });
    }
}
