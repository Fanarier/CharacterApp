using System.IO;
using Newtonsoft.Json;

namespace CharacterApp
{
    public class AppSettings
    {
        public string SelectedTheme { get; set; } = "Light";

        private static readonly string SettingsPath = "settings.json";

        public static AppSettings Load()
        {
            if (File.Exists(SettingsPath))
            {
                string json = File.ReadAllText(SettingsPath);
                return JsonConvert.DeserializeObject<AppSettings>(json);
            }
            return new AppSettings();
        }

        public void Save()
        {
            string json = JsonConvert.SerializeObject(this, Formatting.Indented);
            File.WriteAllText(SettingsPath, json);
        }
    }
}
