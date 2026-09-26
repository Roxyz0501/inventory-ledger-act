using System;
using System.Globalization;
using System.IO;
using System.Xml.Serialization;

namespace InventoryLedger
{
    public sealed class PluginSettings
    {
        public string Language { get; set; }
        public bool Enabled { get; set; }
        public string ApprovedGameBuild { get; set; } = "";
        public bool CheckUpdatesOnStartup { get; set; } = true;
        public string SkippedVersion { get; set; } = "";
        public void InitializeLanguageIfMissing(CultureInfo culture) { if (string.IsNullOrEmpty(Language)) Language = Localization.MapCulture(culture); }
        public void Normalize() { if (!string.IsNullOrEmpty(Language)) Language = Localization.NormalizeLanguage(Language); }
        public static PluginSettings Load(string path) { try { using (var f = File.OpenRead(path)) return (PluginSettings)new XmlSerializer(typeof(PluginSettings)).Deserialize(f); } catch { return new PluginSettings(); } }
        public void Save(string path)
        {
            Normalize(); Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var f = File.Create(path + ".tmp")) new XmlSerializer(typeof(PluginSettings)).Serialize(f, this);
            if (File.Exists(path)) File.Replace(path + ".tmp", path, path + ".bak"); else File.Move(path + ".tmp", path);
        }
    }
}
