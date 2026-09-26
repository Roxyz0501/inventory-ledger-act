using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace InventoryLedger
{
    public sealed class CatalogItem
    {
        public uint Id { get; set; }
        public string Name { get; set; }
        public string IconPath { get; set; }
        [ScriptIgnore] public Image Icon { get; set; }
    }
    public sealed class ItemCatalog : IDisposable
    {
        private readonly HttpClient http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(8) };
        private readonly Dictionary<string, CatalogItem> cache = new Dictionary<string, CatalogItem>();
        private readonly HashSet<string> attempted = new HashSet<string>();
        private readonly string directory;
        private bool busy, disposed;
        public event EventHandler Changed;
        public ItemCatalog(string directory) { this.directory = directory; http.DefaultRequestHeaders.UserAgent.ParseAdd("InventoryLedger/0.1.0"); }
        private static string Language(string language) => language == "ja" ? "ja" : "en";
        public CatalogItem Get(uint id, string language) { CatalogItem item; return cache.TryGetValue(Language(language) + ":" + id, out item) ? item : null; }
        public async Task LoadAsync(IEnumerable<uint> ids, string language)
        {
            if (busy || disposed) return; busy = true;
            try
            {
                Directory.CreateDirectory(directory); var lang = Language(language);
                foreach (var id in ids)
                {
                    if (disposed) break;
                    var key = lang + ":" + id; if (attempted.Contains(key)) continue; attempted.Add(key);
                    try
                    {
                        var file = Path.Combine(directory, lang + "-" + id + ".json");
                        var json = new JavaScriptSerializer(); CatalogItem item;
                        if (File.Exists(file) && new FileInfo(file).Length < 65536) item = json.Deserialize<CatalogItem>(File.ReadAllText(file));
                        else
                        {
                            var response = await http.GetStringAsync("https://v2.xivapi.com/api/sheet/Item/" + id + "?fields=Name,Icon&language=" + lang);
                            if (response.Length > 65536) continue;
                            var root = json.Deserialize<Dictionary<string, object>>(response);
                            var fields = (Dictionary<string, object>)root["fields"]; var icon = (Dictionary<string, object>)fields["Icon"];
                            item = new CatalogItem { Id = id, Name = Convert.ToString(fields["Name"]), IconPath = Convert.ToString(icon["path"]) };
                            File.WriteAllText(file, json.Serialize(item));
                        }
                        if (item == null || item.Id != id) continue;
                        cache[key] = item;
                        if (!string.IsNullOrEmpty(item.IconPath) && System.Text.RegularExpressions.Regex.IsMatch(item.IconPath, @"^ui/icon/\d{6}/\d{6}\.tex$"))
                        {
                            var imageFile = Path.Combine(directory, "icon-" + id + ".png");
                            byte[] bytes;
                            if (File.Exists(imageFile) && new FileInfo(imageFile).Length < 1024 * 1024) bytes = File.ReadAllBytes(imageFile);
                            else { bytes = await http.GetByteArrayAsync("https://v2.xivapi.com/api/asset?path=" + Uri.EscapeDataString(item.IconPath) + "&format=png"); if (bytes.Length > 1024 * 1024) continue; File.WriteAllBytes(imageFile, bytes); }
                            using (var stream = new MemoryStream(bytes)) using (var decoded = Image.FromStream(stream))
                                if (!disposed) item.Icon = new Bitmap(decoded);
                        }
                    }
                    catch { /* Item ID remains visible when metadata is unavailable. */ }
                }
            }
            catch { }
            finally { busy = false; if (!disposed) Changed?.Invoke(this, EventArgs.Empty); }
        }
        public void Dispose() { disposed = true; http.Dispose(); foreach (var item in cache.Values) item.Icon?.Dispose(); }
    }
}
