using Advanced_Combat_Tracker;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace InventoryLedger
{
    public sealed class InventoryLedgerPlugin : IActPluginV1
    {
        private readonly UpdateService updates = new UpdateService();
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly FfxivCapture capture = new FfxivCapture();
        private PluginSettings settings;
        private SettingsControl control;
        private Label status;
        private string settingsPath, ledgerPath;
        private CaptureEngine engine;
        private ItemCatalog catalog;
        private bool stopped, checking, preparing, blocked, wasCapturing;
        private long shown = -1, saved = -1;
        private DateTime lastSave = DateTime.MinValue;
        private System.Windows.Forms.Timer timer;
        private UpdateCheckResult lastUpdate;
        public void InitPlugin(TabPage page, Label statusLabel)
        {
            status=statusLabel;
            var root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Advanced Combat Tracker","InventoryLedger");
            settingsPath=Path.Combine(root,"settings.xml"); ledgerPath=Path.Combine(root,"ledger.json");
            settings=PluginSettings.Load(settingsPath); settings.InitializeLanguageIfMissing(CultureInfo.CurrentUICulture); settings.Normalize();
            Ledger ledger;
            try
            {
                bool recovered; ledger=LedgerFiles.Load(ledgerPath,out recovered);
                if(recovered) { File.Copy(ledgerPath,ledgerPath+".damaged-"+DateTime.UtcNow.Ticks); status.Text=T("Recovered"); }
            }
            catch { ledger=new Ledger(); blocked=true; status.Text=T("LoadFailed"); }
            engine=new CaptureEngine(ledger,CaptureProfile.Load());
            catalog=new ItemCatalog(Path.Combine(root,"Items"));
            control=new SettingsControl(settings); control.Inventory.SetCatalog(catalog); control.Inventory.RefreshData(ledger); page.Text="Inventory Ledger"; page.Controls.Add(control);
            control.SettingsChanged+=delegate { Save(); shown=-1; };
            control.ApproveCaptureRequested+=delegate
            {
                capture.TryConnect(); if(capture.Connected)capture.Poll();
                if(control.EnteredPatch==CaptureProfile.Load().Patch && capture.Region==1 && !string.IsNullOrEmpty(capture.Build)) { settings.ApprovedGameBuild=capture.Build; Save(); }
                else status.Text=T("BuildBlocked");
            };
            control.CheckUpdatesRequested+=delegate { CheckUpdates(true); };
            control.InstallUpdateRequested+=Install;
            control.LaterRequested+=delegate { if(lastUpdate?.Release==null)return; settings.SkippedVersion=lastUpdate.Release.Version.ToString(); Save(); control.ShowUpdateSkipped(settings.SkippedVersion); };
            Save(); timer=new System.Windows.Forms.Timer { Interval=1000 }; timer.Tick+=Tick; timer.Start(); Tick(null,EventArgs.Empty);
            if(settings.CheckUpdatesOnStartup)CheckUpdates(false);
        }
        private string T(string key, params object[] args)=>Localization.Get(settings?.Language??"en",key,args);
        private void Save() { try { settings.Save(settingsPath); } catch { if(status!=null)status.Text=T("SaveFailed"); } }
        private void Tick(object sender,EventArgs args)
        {
            if(stopped)return;
            var key="Disabled";
            capture.TryConnect(); if(capture.Connected)capture.Poll();
            bool allowed=settings.Enabled && !blocked && capture.Connected && capture.Region==1 && !string.IsNullOrEmpty(capture.Build) && capture.Build==settings.ApprovedGameBuild;
            if(capture.Overflow) { allowed=false; key="Incomplete"; }
            if(allowed)
            {
                if(!wasCapturing)engine.Reset();
                capture.SetAccepting(true);
                foreach(var packet in capture.Drain()) engine.Receive(packet.Connection,packet.Bytes,packet.Utc,capture.Actor);
                key=engine.StatusKey;
            }
            else
            {
                capture.SetAccepting(false); if(wasCapturing)engine.Reset();
                if(blocked)key="LoadFailed"; else if(settings.Enabled)key=capture.Connected?"BuildBlocked":"AwaitPlugin";
            }
            wasCapturing=allowed;
            control.SetCaptureStatus(key,capture.Build,engine.Rejected);
            if(shown!=engine.Revision) { shown=engine.Revision; control.Inventory.RefreshData(engine.Data); }
            if(!blocked && saved!=engine.Revision && DateTime.UtcNow-lastSave>TimeSpan.FromSeconds(5))SaveLedger();
        }
        private void SaveLedger()
        {
            try { LedgerFiles.Save(ledgerPath,engine.Data); saved=engine.Revision; lastSave=DateTime.UtcNow; }
            catch { status.Text=T("SaveFailed"); }
        }
        private async void CheckUpdates(bool manual)
        {
            if (checking || preparing || stopped) return; checking = true; control.SetUpdateChecking();
            try
            {
                var current = Assembly.GetExecutingAssembly().GetName().Version;
                var result = await updates.CheckAsync(current, cancellation.Token); if (stopped) return;
                lastUpdate = result; control.ShowUpdateResult(result, current);
                if (result.Status == UpdateCheckStatus.UpdateAvailable)
                {
                    if (!manual && settings.SkippedVersion == result.Release.Version.ToString()) control.ShowUpdateSkipped(settings.SkippedVersion);
                    else { control.FocusUpdateTab(); status.Text = T("UpdateAvailable", current.ToString(3), result.Release.Version); }
                }
            }
            finally { checking = false; }
        }
        private async void Install(object source, EventArgs e)
        {
            if (preparing || stopped || lastUpdate?.Status != UpdateCheckStatus.UpdateAvailable) return;
            preparing = true; control.SetUpdateText("Preparing");
            try
            {
                var target = ResolvePluginDllPath(ActGlobals.oFormActMain.ActPlugins, this);
                var package = await updates.DownloadAndVerifyAsync(lastUpdate.Release, cancellation.Token); if (stopped) return;
                updates.LaunchUpdater(package, target); control.SetUpdateText("Prepared"); status.Text = T("Prepared");
            }
            catch { if (!stopped) { control.SetUpdateText("UpdateFailed", T("UpdateProblem")); status.Text = T("UpdateProblem"); } }
            finally { preparing = false; }
        }
        internal static string ResolvePluginDllPath(IEnumerable<ActPluginData> plugins, IActPluginV1 instance)
        {
            var registration = plugins?.FirstOrDefault(x => x != null && ReferenceEquals(x.pluginObj, instance));
            return NormalizePluginPath(registration?.pluginFile?.FullName);
        }
        internal static string NormalizePluginPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("PluginPathUnavailable");
            Uri uri; if (Uri.TryCreate(value, UriKind.Absolute, out uri) && uri.IsFile) value = uri.LocalPath;
            if (!Path.IsPathRooted(value)) throw new InvalidOperationException("PluginPathUnavailable");
            var path = Path.GetFullPath(value);
            if (!string.Equals(Path.GetFileName(path), UpdateConfiguration.PluginFileName, StringComparison.OrdinalIgnoreCase) || !File.Exists(path)) throw new InvalidOperationException("PluginPathUnavailable");
            return path;
        }
        public void DeInitPlugin()
        {
            stopped=true; timer?.Stop(); timer?.Dispose(); capture.Dispose(); cancellation.Cancel();
            if(!blocked && engine!=null && saved!=engine.Revision)SaveLedger();
            Save(); control?.Dispose(); catalog?.Dispose(); updates.Dispose(); status.Text=T("Stopped");
        }
    }
}
