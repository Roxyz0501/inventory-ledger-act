// Packet field layouts referenced from pcap-ffxiv (MIT, karashiiro).
// See README for the pinned source revision and complete third-party notices.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;

namespace InventoryLedger
{
    public sealed class CaptureProfile
    {
        public string Patch { get; set; }
        public string Region { get; set; }
        public Dictionary<string, ushort> Opcodes { get; set; }
        public static CaptureProfile Load()
        {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("InventoryLedger.CaptureProfile.json"))
            using (var r = new StreamReader(s)) return new JavaScriptSerializer().Deserialize<CaptureProfile>(r.ReadToEnd());
        }
    }
    public sealed class CaptureEngine
    {
        private sealed class Batch
        {
            public uint Sequence;
            public int Container;
            public string Owner;
            public int? Expected;
            public DateTime Utc;
            public Dictionary<int, Item> Items = new Dictionary<int, Item>();
        }
        private readonly Dictionary<ushort, string> opcodes;
        private readonly Dictionary<string, Batch> batches = new Dictionary<string, Batch>();
        private readonly Dictionary<string, uint> committed = new Dictionary<string, uint>();
        private readonly HashSet<string> sessionBaselines = new HashSet<string>();
        private readonly List<Batch> waitingRetainer = new List<Batch>();
        private readonly HashSet<string> observedRetainers = new HashSet<string>();
        private string character, fc, retainer, connection;
        private uint actor;
        private DateTime loginUtc;
        public Ledger Data { get; }
        public long Revision { get; private set; }
        public long Rejected { get; private set; }
        public string StatusKey { get; private set; } = "AwaitLogin";
        public CaptureEngine(Ledger data, CaptureProfile profile) { Data = data; opcodes = profile.Opcodes.ToDictionary(x => x.Value, x => x.Key); }
        public void Reset()
        {
            character = fc = retainer = connection = null; actor = 0; batches.Clear(); committed.Clear(); sessionBaselines.Clear(); waitingRetainer.Clear(); observedRetainers.Clear(); StatusKey = "AwaitLogin";
        }
        private static ushort U16(byte[] b, int p) => BitConverter.ToUInt16(b, p);
        private static uint U32(byte[] b, int p) => BitConverter.ToUInt32(b, p);
        private static ulong U64(byte[] b, int p) => BitConverter.ToUInt64(b, p);
        private static string Name(byte[] b, int p, int length) { var end = Array.IndexOf(b, (byte)0, p, length); return Encoding.UTF8.GetString(b, p, end < 0 ? length : end - p).Trim(); }
        public void Receive(string channel, byte[] b, DateTime utc, uint currentActor)
        {
            try
            {
                if (b == null || b.Length < 32 || b.Length > 65536 || U32(b, 0) != b.Length) { Rejected++; return; }
                string type; if (!opcodes.TryGetValue(U16(b, 18), out type)) return;
                const int p = 32;
                var target = U32(b, 8);
                if (currentActor == 0 || target != currentActor) return;
                // Never carry ownership or balance baselines over a logout, process or connection change.
                if (type == "PlayerSetup")
                {
                    if (b.Length < p + 0x318 || U32(b, p + 16) != currentActor || U64(b, p) == 0) { Rejected++; return; }
                    Reset(); actor = currentActor; connection = channel; loginUtc = utc;
                    character = "C:" + U64(b, p).ToString("X16");
                    Upsert(new Owner { Id = character, CharacterId = character, Name = Name(b, p + 0x2f8, 32), Kind = "Character", UpdatedUtc = utc });
                    StatusKey = "Capturing"; return;
                }
                if (character == null || actor != currentActor || channel != connection) { if (actor != currentActor) Reset(); return; }
                if (utc < loginUtc) return;
                if (type == "Logout") { Reset(); return; }
                foreach (var key in batches.Where(x => utc - x.Value.Utc > TimeSpan.FromSeconds(15)).Select(x => x.Key).ToArray()) batches.Remove(key);
                waitingRetainer.RemoveAll(x => utc - x.Utc > TimeSpan.FromSeconds(10));
                switch (type)
                {
                    case "RetainerInformation":
                        if (b.Length < p + 65 || U64(b, p + 8) == 0) break;
                        var id = "R:" + U64(b, p + 8).ToString("X16");
                        Upsert(new Owner { Id = id, CharacterId = character, Kind = "Retainer", Name = Name(b, p + 45, 20), Gil = U32(b, p + 20), Job = b[p + 26], Level = b[p + 27], UpdatedUtc = utc });
                        observedRetainers.Add(id); break;
                    case "FreeCompanyInfo":
                        if (b.Length < p + 80) break;
                        fc = U64(b, p) == 0 ? null : "F:" + U64(b, p).ToString("X16");
                        if (fc != null) Upsert(new Owner { Id = fc, Kind = "Fc", Name = fc, UpdatedUtc = utc });
                        break;
                    case "EventStart": case "EventFinish":
                        retainer = null; waitingRetainer.Clear();
                        foreach (var key in batches.Where(x => Containers.Retainer(x.Value.Container)).Select(x => x.Key).ToArray()) batches.Remove(key);
                        break;
                    case "NpcSpawn":
                        if (b.Length < p + 606 || waitingRetainer.Count == 0) break;
                        var name = Name(b, p + 574, 32);
                        var candidates = Data.Owners.Where(x => observedRetainers.Contains(x.Id) && x.CharacterId == character && x.Name == name).ToArray();
                        if (candidates.Length != 1) break;
                        retainer = candidates[0].Id;
                        foreach (var batch in waitingRetainer) { batch.Owner = retainer; Commit(batch); }
                        waitingRetainer.Clear(); break;
                    case "ContainerInfo":
                        if (b.Length < p + 16) break;
                        AddBatch(U32(b, p), checked((int)U32(b, p + 8)), checked((int)U32(b, p + 4)), null, utc); break;
                    case "ItemInfo": case "UpdateInventorySlot":
                        if (b.Length < p + 64) break;
                        var container = U16(b, p + 8);
                        var item = new Item { Slot = U16(b, p + 10), Id = U32(b, p + 16), Quantity = U32(b, p + 12), Hq = b[p + 32] != 0 };
                        if (type == "ItemInfo") AddBatch(U32(b, p), container, null, item, utc);
                        else Update(container, item, utc);
                        break;
                    case "CurrencyCrystalInfo":
                        if (b.Length < p + 32) break;
                        AddBatch(U32(b, p), U16(b, p + 4), null, new Item { Slot = U16(b, p + 6), Quantity = U32(b, p + 8), Id = U32(b, p + 16) }, utc); break;
                    case "ItemMarketBoardInfo":
                        if (b.Length < p + 20 || retainer == null || U32(b, p + 4) != 12002) break;
                        var c = Data.Find(retainer, 12002);
                        var entry = c?.Items.FirstOrDefault(x => x.Slot == U32(b, p + 8));
                        if (entry != null) { entry.UnitPrice = U32(b, p + 16); Revision++; }
                        break;
                }
            }
            catch (Exception ex) when (ex is ArgumentException || ex is OverflowException || ex is IndexOutOfRangeException)
            { Rejected++; }
        }
        private string OwnerFor(int container) => Containers.Retainer(container) ? retainer : Containers.Fc(container) ? fc : character;
        private void Upsert(Owner owner)
        {
            var old = Data.FindOwner(owner.Id); if (old != null && old.UpdatedUtc > owner.UpdatedUtc) return;
            Data.Owners.RemoveAll(x => x.Id == owner.Id); Data.Owners.Add(owner); Revision++;
        }
        private void AddBatch(uint seq, int container, int? expected, Item item, DateTime utc)
        {
            if (!Containers.Supported(container) || expected > 1000 || expected < 0 || item != null && item.Slot > 999) { Rejected++; return; }
            var key = seq + ":" + container;
            Batch batch;
            if (!batches.TryGetValue(key, out batch))
            {
                if (batches.Count >= 256) { batches.Clear(); StatusKey = "Incomplete"; }
                batch = new Batch { Sequence = seq, Container = container, Owner = Containers.Retainer(container) ? null : OwnerFor(container), Utc = utc };
                batches[key] = batch;
            }
            if (expected.HasValue) batch.Expected = expected;
            if (item != null) batch.Items[item.Slot] = item;
            if (utc > batch.Utc) batch.Utc = utc;
            if (batch.Expected.HasValue && batch.Items.Count == batch.Expected.Value)
            {
                batches.Remove(key);
                if (Containers.Retainer(container))
                {
                    retainer = null;
                    if (waitingRetainer.Count >= 64) waitingRetainer.Clear();
                    waitingRetainer.Add(batch);
                }
                else Commit(batch);
            }
        }
        private void Commit(Batch batch)
        {
            if (batch.Owner == null || Data.FindOwner(batch.Owner) == null) return;
            var key = batch.Owner + ":" + batch.Container;
            uint prior;
            if (committed.TryGetValue(key, out prior) && unchecked((int)(batch.Sequence - prior)) <= 0) return;
            committed[key] = batch.Sequence; sessionBaselines.Add(key);
            Data.Replace(batch.Owner, batch.Container, batch.Items.Values, batch.Utc); Revision++;
        }
        private void Update(int container, Item item, DateTime utc)
        {
            if (!Containers.Supported(container) || item.Slot > 999) return;
            var owner = OwnerFor(container); if (owner == null || Data.FindOwner(owner) == null) return;
            // Do not infer a live balance change against a snapshot from a previous session.
            if (!sessionBaselines.Contains(owner + ":" + container)) return;
            Data.Update(owner, container, item, utc); Revision++;
        }
    }
}
