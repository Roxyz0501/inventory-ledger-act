using Advanced_Combat_Tracker;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace InventoryLedger
{
    internal sealed class CapturedPacket
    {
        public string Connection;
        public DateTime Utc;
        public byte[] Bytes;
    }
    internal sealed class FfxivCapture : IDisposable
    {
        private object subscription, repository;
        private EventInfo network;
        private Delegate handler;
        private volatile bool accepting;
        private readonly System.Collections.Generic.HashSet<ushort> relevant = new System.Collections.Generic.HashSet<ushort>(
            CaptureProfile.Load().Opcodes.Where(x => new[] { "PlayerSetup", "Logout", "RetainerInformation", "FreeCompanyInfo", "EventStart", "EventFinish", "NpcSpawn", "ContainerInfo", "ItemInfo", "UpdateInventorySlot", "CurrencyCrystalInfo", "ItemMarketBoardInfo" }.Contains(x.Key)).Select(x => x.Value));
        private readonly ConcurrentQueue<CapturedPacket> queue = new ConcurrentQueue<CapturedPacket>();
        public bool Connected => subscription != null;
        public string Build { get; private set; } = "";
        public uint Actor { get; private set; }
        public byte Region { get; private set; }
        public bool Overflow { get; private set; }
        public bool TryConnect()
        {
            if (Connected) return true;
            try
            {
                var plugin = ActGlobals.oFormActMain.ActPlugins.Select(x => x.pluginObj).FirstOrDefault(x => x != null && x.GetType().FullName == "FFXIV_ACT_Plugin.FFXIV_ACT_Plugin");
                if (plugin == null) return false;
                repository = plugin.GetType().GetProperty("DataRepository").GetValue(plugin);
                subscription = plugin.GetType().GetProperty("DataSubscription").GetValue(plugin);
                network = subscription.GetType().GetEvent("NetworkReceived");
                handler = Delegate.CreateDelegate(network.EventHandlerType, this, GetType().GetMethod("Receive", BindingFlags.NonPublic | BindingFlags.Instance));
                network.AddEventHandler(subscription, handler); return true;
            }
            catch { Dispose(); return false; }
        }
        public void Poll()
        {
            try
            {
                Build = Convert.ToString(Call("GetGameVersion")).Trim();
                Actor = Convert.ToUInt32(Call("GetCurrentPlayerID"));
                Region = Convert.ToByte(Call("GetGameRegion"));
            }
            catch { Build = ""; Actor = 0; Region = 0; Dispose(); }
        }
        private object Call(string name) => repository.GetType().GetMethod(name).Invoke(repository, null);
        public void SetAccepting(bool value)
        {
            accepting = value;
            if (!value) { CapturedPacket p; while (queue.TryDequeue(out p)) { } Overflow = false; }
        }
        private void Receive(string connection, long epoch, byte[] bytes)
        {
            if (!accepting || bytes == null || bytes.Length < 32 || bytes.Length > 65536) return;
            if (!relevant.Contains(BitConverter.ToUInt16(bytes,18))) return;
            if (queue.Count >= 4096) { Overflow = true; accepting = false; return; }
            DateTime utc;
            try { utc = DateTimeOffset.FromUnixTimeMilliseconds(epoch).UtcDateTime; }
            catch { return; }
            queue.Enqueue(new CapturedPacket { Connection = connection, Utc = utc, Bytes = (byte[])bytes.Clone() });
        }
        public CapturedPacket[] Drain()
        {
            var result = new System.Collections.Generic.List<CapturedPacket>();
            CapturedPacket p; while (result.Count < 4096 && queue.TryDequeue(out p)) result.Add(p);
            return result.OrderBy(x => x.Utc).ToArray();
        }
        public void Dispose()
        {
            SetAccepting(false);
            try { if (subscription != null && handler != null) network.RemoveEventHandler(subscription, handler); } catch { }
            subscription = repository = null; network = null; handler = null;
        }
    }
}
