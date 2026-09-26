using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace InventoryLedger
{
    public sealed class Owner
    {
        public string Id { get; set; }
        public string CharacterId { get; set; }
        public string Name { get; set; }
        public string Kind { get; set; }
        public uint Gil { get; set; }
        public int Level { get; set; }
        public int Job { get; set; }
        public DateTime UpdatedUtc { get; set; }
    }
    public sealed class Item
    {
        public int Slot { get; set; }
        public uint Id { get; set; }
        public uint Quantity { get; set; }
        public bool Hq { get; set; }
        public uint UnitPrice { get; set; }
        public Item Copy() => (Item)MemberwiseClone();
    }
    public sealed class Container
    {
        public string OwnerId { get; set; }
        public int Id { get; set; }
        public DateTime UpdatedUtc { get; set; }
        public bool Complete { get; set; }
        public bool OwnershipInferred { get; set; }
        public List<Item> Items { get; set; } = new List<Item>();
    }
    public sealed class LedgerEntry
    {
        public string Key { get; set; }
        public DateTime Utc { get; set; }
        public string OwnerId { get; set; }
        public string Kind { get; set; }
        public long Amount { get; set; }
        public string Evidence { get; set; }
    }
    public sealed class Ledger
    {
        public int Schema { get; set; } = 1;
        public List<Owner> Owners { get; set; } = new List<Owner>();
        public List<Container> Containers { get; set; } = new List<Container>();
        public List<LedgerEntry> Entries { get; set; } = new List<LedgerEntry>();
        public void Validate()
        {
            if (Schema != 1 || Owners == null || Containers == null || Entries == null || Owners.Count > 10000 || Containers.Count > 100000 || Entries.Count > 1000000)
                throw new InvalidDataException("Invalid ledger schema");
            if (Owners.Any(x => x == null || string.IsNullOrEmpty(x.Id)) || Owners.Select(x => x.Id).Distinct().Count() != Owners.Count) throw new InvalidDataException("Invalid owners");
            if (Containers.Any(x => x == null || x.Items == null || x.Items.Count > 1000 || !Owners.Any(o => o.Id == x.OwnerId) || x.Items.Any(i => i == null || i.Slot < 0 || i.Slot > 999) || x.Items.Select(i => i.Slot).Distinct().Count() != x.Items.Count)) throw new InvalidDataException("Invalid containers");
            if (Containers.Select(x => x.OwnerId + ":" + x.Id).Distinct().Count() != Containers.Count) throw new InvalidDataException("Duplicate containers");
            if (Entries.Any(x => x == null || x.Key == null)) throw new InvalidDataException("Invalid entries");
        }
        public Owner FindOwner(string id) => Owners.FirstOrDefault(x => x.Id == id);
        public Container Find(string owner, int container) => Containers.FirstOrDefault(x => x.OwnerId == owner && x.Id == container);
        public void Replace(string owner, int container, IEnumerable<Item> items, DateTime utc)
        {
            var old = Find(owner, container);
            if (old != null && old.UpdatedUtc > utc) return;
            var next = new Container { OwnerId = owner, Id = container, UpdatedUtc = utc, Complete = true, OwnershipInferred = global::InventoryLedger.Containers.Retainer(container), Items = items.Where(x => x.Id != 0 && x.Quantity > 0).Select(x => x.Copy()).ToList() };
            if (old != null) Containers.Remove(old);
            Containers.Add(next);
        }
        public void Update(string owner, int container, Item item, DateTime utc)
        {
            var c = Find(owner, container);
            if (c != null && c.UpdatedUtc > utc) return;
            if (c == null) { c = new Container { OwnerId = owner, Id = container }; Containers.Add(c); }
            var old = c.Items.FirstOrDefault(x => x.Slot == item.Slot);
            // An observed balance difference is not proof of a purchase, sale or trade.
            if ((container == 2000 || container == 12000 || container == 22000) && item.Id == 1 && old != null && old.Id == 1 && old.Quantity != item.Quantity)
            {
                var key = owner + ":" + container + ":" + utc.Ticks + ":" + item.Quantity;
                if (!Entries.Any(x => x.Key == key)) Entries.Add(new LedgerEntry { Key = key, Utc = utc, OwnerId = owner, Kind = "BalanceChange", Amount = (long)item.Quantity - old.Quantity, Evidence = "UnknownReason" });
            }
            c.Items.RemoveAll(x => x.Slot == item.Slot);
            if (item.Id != 0 && item.Quantity > 0) c.Items.Add(item.Copy());
            c.UpdatedUtc = utc;
        }
    }
    public static class LedgerFiles
    {
        private static JavaScriptSerializer Json() => new JavaScriptSerializer { MaxJsonLength = 64 * 1024 * 1024, RecursionLimit = 64 };
        public static Ledger Load(string path, out bool recovered)
        {
            recovered = false;
            if (!File.Exists(path)) return new Ledger();
            try { return Read(path); }
            catch { if (!File.Exists(path + ".bak")) throw; var value = Read(path + ".bak"); recovered = true; return value; }
        }
        private static Ledger Read(string path)
        {
            if (new FileInfo(path).Length > 64L * 1024 * 1024) throw new InvalidDataException("Ledger too large");
            var result = Json().Deserialize<Ledger>(File.ReadAllText(path, Encoding.UTF8));
            if (result == null) throw new InvalidDataException("Empty ledger");
            result.Validate(); return result;
        }
        public static void Save(string path, Ledger ledger)
        {
            ledger.Validate(); Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temp = path + ".tmp"; File.WriteAllText(temp, Json().Serialize(ledger), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak"); else File.Move(temp, path);
        }
        public static string CsvCell(string value)
        {
            value = value ?? "";
            var trimmed = value.TrimStart();
            long integer;
            var number = long.TryParse(value, System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out integer);
            if (!number && trimmed.Length > 0 && "=+-@".Contains(trimmed[0]) || value.StartsWith("\t") || value.StartsWith("\r") || value.StartsWith("\n")) value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        public static void Csv(string path, IEnumerable<string[]> rows)
        {
            using (var w = new StreamWriter(path, false, new UTF8Encoding(true))) foreach (var row in rows) w.WriteLine(string.Join(",", row.Select(CsvCell)));
        }
    }
    public static class Containers
    {
        public static bool Retainer(int id) => id >= 10000 && id <= 10006 || id == 11000 || id >= 12000 && id <= 12002;
        public static bool Fc(int id) => id >= 20000 && id <= 20010 || id == 22000 || id == 22001;
        public static bool Supported(int id) => id >= 0 && id <= 3 || id == 1000 || id == 2000 || id == 2001 || id >= 3200 && id <= 3209 || id == 3300 || id == 3400 || id == 3500 || id == 4000 || id == 4001 || id == 4100 || id == 4101 || Retainer(id) || Fc(id);
        public static string Kind(int id) => Retainer(id) ? "Retainer" : Fc(id) ? "Fc" : id >= 4000 ? "Saddlebag" : id >= 3200 ? "Armoury" : id == 1000 ? "Equipment" : id == 2000 ? "Currency" : id == 2001 ? "Crystals" : "Bags";
    }
}
