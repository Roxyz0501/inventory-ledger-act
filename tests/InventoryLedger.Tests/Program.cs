using InventoryLedger;
using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

internal static partial class Program
{
    private static int assertions;
    private static string artifacts;
    private static CaptureProfile profile;
    private static DateTime now = new DateTime(2026,9,27,0,0,0,DateTimeKind.Utc);
    [STAThread] private static int Main()
    {
        try
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            artifacts=Path.Combine(Environment.CurrentDirectory,"artifacts","tests-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(artifacts);
            profile=CaptureProfile.Load();
            Capture(); Persistence(); Languages(); UpdateSafety(); Render();
            Console.WriteLine("PASS: "+assertions+" assertions; "+artifacts); return 0;
        }
        catch(Exception e) { Console.Error.WriteLine(e.GetType().FullName); Console.Error.WriteLine(e.Message); Console.Error.WriteLine(e.StackTrace); return 1; }
    }
    private static void Check(bool condition,string label) { if(!condition)throw new Exception(label); assertions++; }
    private static void Throws(Action a,string label) { bool thrown=false;try{a();}catch{thrown=true;}Check(thrown,label); }
    private static byte[] Packet(string type,int size)
    {
        var b=new byte[size+32]; Put(b,0,(uint)b.Length); Put(b,4,(uint)42); Put(b,8,(uint)42); Put(b,18,profile.Opcodes[type]);return b;
    }
    private static void Put(byte[] b,int offset,uint x)=>Buffer.BlockCopy(BitConverter.GetBytes(x),0,b,offset,4);
    private static void Put(byte[] b,int offset,ushort x)=>Buffer.BlockCopy(BitConverter.GetBytes(x),0,b,offset,2);
    private static void Put64(byte[] b,int offset,ulong x)=>Buffer.BlockCopy(BitConverter.GetBytes(x),0,b,offset,8);
    private static void Text(byte[] b,int offset,string s)=>Buffer.BlockCopy(Encoding.UTF8.GetBytes(s),0,b,offset,Encoding.UTF8.GetByteCount(s));
    private static byte[] Login(ulong id)
    {
        var b=Packet("PlayerSetup",0x318);Put64(b,32,id);Put(b,48,(uint)42);Text(b,32+0x2f8,"Test Character");return b;
    }
    private static byte[] Info(uint sequence,ushort container,ushort slot,uint id,uint quantity,string type="ItemInfo")
    {
        var b=Packet(type,64);Put(b,32,sequence);Put(b,40,container);Put(b,42,slot);Put(b,44,quantity);Put(b,48,id);return b;
    }
    private static byte[] End(uint sequence,uint container,uint count)
    {
        var b=Packet("ContainerInfo",16);Put(b,32,sequence);Put(b,36,count);Put(b,40,container);return b;
    }
    private static void Send(CaptureEngine e,byte[] b)=>e.Receive("test",b,now,42);
    private static void Capture()
    {
        var data=new Ledger();var e=new CaptureEngine(data,profile);
        Send(e,Info(1,0,0,2,5));Check(data.Containers.Count==0,"no owner before login");Send(e,Login(1));
        Send(e,Info(1,0,0,2,5));Check(data.Containers.Count==0,"no partial overwrite");Send(e,End(1,0,1));
        Check(data.Find("C:0000000000000001",0).Items.Single().Quantity==5,"commit snapshot");
        Send(e,Info(2,0,0,2,1));Send(e,End(2,0,2));Check(data.Containers.Single().Items.Single().Quantity==5,"missing item preserves old snapshot");
        Send(e,Info(2,0,1,3,9));Check(data.Containers.Single().Items.Count==2,"out of order snapshot completes");
        Send(e,End(1,0,0));Check(data.Containers.Single().Items.Count==2,"older sequence ignored");
        Send(e,End(3,0,0));Check(data.Containers.Single().Items.Count==0,"confirmed empty snapshot");
        Send(e,Info(4,2000,0,1,100));Send(e,End(4,2000,1));now=now.AddSeconds(1);
        Send(e,Info(5,2000,0,1,80,"UpdateInventorySlot"));Send(e,Info(5,2000,0,1,80,"UpdateInventorySlot"));
        Check(data.Entries.Count==1 && data.Entries[0].Amount==-20 && data.Entries[0].Evidence=="UnknownReason","balance change not guessed as sale or trade; duplicate ignored");
        var r=Packet("RetainerInformation",65);Put64(r,40,999);Put(r,52,(uint)123);Text(r,77,"Test Retainer");Send(e,r);
        Check(data.Owners.Single(x=>x.Kind=="Retainer").Id=="R:00000000000003E7","retainer full 64-bit ID");
        Send(e,Info(6,10000,0,2,12));Send(e,End(6,10000,1));Check(data.Find("R:00000000000003E7",10000)==null,"unresolved retainer not guessed");
        var npc=Packet("NpcSpawn",640);Text(npc,32+574,"Test Retainer");Send(e,npc);
        Check(data.Find("R:00000000000003E7",10000).Items[0].Quantity==12,"retainer spawn associates queued snapshot");
        var fc=Packet("FreeCompanyInfo",80);Put64(fc,32,888);Send(e,fc);Send(e,Info(7,20000,0,3,19));Send(e,End(7,20000,1));
        Check(data.Find("F:0000000000000378",20000).Items[0].Quantity==19,"FC uses independent owner");
        Send(e,Login(2));Send(e,fc);Send(e,Info(8,20000,0,3,20));Send(e,End(8,20000,1));
        Check(data.Containers.Count(x=>x.Id==20000)==1,"FC is not counted twice across characters");
        Send(e,Login(1));Send(e,Info(9,2000,0,1,1,"UpdateInventorySlot"));Check(data.Entries.Count==1,"old session balance never used as live baseline");
        var truncated=Packet("ItemInfo",2);Send(e,truncated);Send(e,new byte[1]); Check(e.Rejected>0,"malformed packet contained");
        var foreign=Info(10,0,0,999,99);Put(foreign,8,(uint)43);Send(e,foreign);Send(e,End(10,0,1));Check(data.Find("C:0000000000000001",0).Items.Count==0,"foreign actor ignored");
        var before=data.Containers.Count;e.Receive("other",Info(11,3,0,2,4),now,42);e.Receive("other",End(11,3,1),now,42);Check(before==data.Containers.Count,"other connection ignored");
        data.Validate();
    }
    private static void Persistence()
    {
        var path=Path.Combine(artifacts,"ledger.json");var data=new Ledger();data.Owners.Add(new Owner{Id="C:1",Name="日本語",Kind="Character"});
        data.Replace("C:1",0,new[]{new Item{Id=2,Slot=0,Quantity=3}},now);LedgerFiles.Save(path,data);LedgerFiles.Save(path,data);
        bool recovered;Check(LedgerFiles.Load(path,out recovered).Owners[0].Name=="日本語" && !recovered,"UTF8 persistence");
        File.WriteAllText(path,"corrupt");Check(LedgerFiles.Load(path,out recovered).Owners.Count==1 && recovered,"backup recovery");
        File.WriteAllText(path+".bak","bad");Throws(()=>LedgerFiles.Load(path,out recovered),"both corrupt must not return empty");
        Check(LedgerFiles.CsvCell("  =cmd").StartsWith("\"'"),"CSV formula neutralized");Check(LedgerFiles.CsvCell("a,\"b")=="\"a,\"\"b\"","CSV quoted");
        Check(LedgerFiles.CsvCell("-20")=="\"-20\"", "negative balance remains numeric");
        Check(LedgerFiles.CsvCell("-1+CMD()").StartsWith("\"'"), "negative formula is still escaped");
        var settings=PluginSettings.Load(Path.Combine(artifacts,"absent.xml"));settings.InitializeLanguageIfMissing(new CultureInfo("ja-JP"));settings.Save(Path.Combine(artifacts,"settings.xml"));
        settings=PluginSettings.Load(Path.Combine(artifacts,"settings.xml"));settings.InitializeLanguageIfMissing(new CultureInfo("en-US"));Check(settings.Language=="ja" && settings.CheckUpdatesOnStartup && !settings.Enabled,"language remembered and safe defaults");
    }
    private static void Languages()
    {
        foreach(var pair in new[]{new[]{"ja-JP","ja"},new[]{"zh-TW","zh-CN"},new[]{"ko-KR","ko"},new[]{"fr-FR","en"}})Check(Localization.MapCulture(new CultureInfo(pair[0]))==pair[1],"OS language mapping");
        foreach(var lang in new[]{"en","ja","zh-CN","ko"})
        { Check(Localization.Get(lang,"FallbackProbe")=="English fallback","English fallback");Check(Localization.Get(lang,"HistoryLimit")!="HistoryLimit","translated limitation"); }
        string error;int calls=0;
        Check(SettingsControl.TryOpenSupportLink(info=>{calls++;Check(info.FileName==SettingsControl.SupportUrl && info.UseShellExecute,"support opens fixed URL");return null;},out error)&&calls==1,"explicit link dispatch");
        Check(!SettingsControl.TryOpenSupportLink(info=>{throw new IOException();},out error),"browser error contained");
    }
    private static void Render()
    {
        foreach(var lang in new[]{"en","ja","zh-CN","ko"})
        using(var form=new Form{ClientSize=new Size(700,800),ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-20000,-20000)})
        using(var control=new SettingsControl(new PluginSettings{Language=lang}))
        {
            form.Controls.Add(control);form.Show();form.PerformLayout();
            var tabs=Descendants(control).OfType<TabControl>().First();
            for(int i=0;i<tabs.TabCount;i++)
            {
                tabs.SelectedIndex=i;control.PerformLayout();
                using(var bitmap=new Bitmap(control.Width,control.Height)){control.DrawToBitmap(bitmap,new Rectangle(0,0,control.Width,control.Height));bitmap.Save(Path.Combine(artifacts,lang+"-"+i+".png"));}
            }
            Check(tabs.TabCount==4,"inventory/settings/update/support tabs");
            form.ClientSize=new Size(440,650);form.PerformLayout();
            tabs.SelectedIndex=1;
            Check(Descendants(control).OfType<CheckBox>().All(x=>x.Height>=x.Font.Height),"checkbox text fits narrow pane");
            var data=new Ledger();data.Owners.Add(new Owner{Id="C:test",Kind="Character",Name="Test Character"});
            data.Replace("C:test",0,new[]{new Item{Id=2,Slot=0,Quantity=18,Hq=true}},now);control.Inventory.RefreshData(data);
            tabs.SelectedIndex=0;
            var viewMode=Descendants(control.Inventory).OfType<ComboBox>().Single(x=>x.Items.Count==4);viewMode.SelectedIndex=1;
            using(var bitmap=new Bitmap(control.Width,control.Height)){control.DrawToBitmap(bitmap,new Rectangle(0,0,control.Width,control.Height));bitmap.Save(Path.Combine(artifacts,lang+"-grid-narrow.png"));}
            Check(Descendants(control.Inventory).OfType<ListView>().Single().Items.Count==1,"grid with fixture survives handle creation and redraw");
        }
    }
    private static System.Collections.Generic.IEnumerable<Control> Descendants(Control parent)
    { foreach(Control child in parent.Controls){yield return child;foreach(var grand in Descendants(child))yield return grand;} }
}
