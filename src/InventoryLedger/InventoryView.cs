using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace InventoryLedger
{
    public sealed class InventoryView : UserControl
    {
        private sealed class Choice { public string Id, Text; public override string ToString() => Text; }
        private readonly TextBox search = new TextBox { Width = 230 };
        private readonly ComboBox owners = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
        private readonly ComboBox mode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
        private readonly Button export = new Button { AutoSize = true };
        private readonly Label summary = new Label { Dock = DockStyle.Fill, AutoSize = false, ForeColor = Color.FromArgb(73,96,128) };
        private readonly DataGridView table = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells, BackgroundColor = Color.White, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
        private readonly ListView grid = new ListView { Dock = DockStyle.Fill, View = View.LargeIcon, BackColor = Color.FromArgb(240,242,245), ShowItemToolTips = true };
        private readonly ImageList images = new ImageList { ImageSize = new Size(40,40), ColorDepth = ColorDepth.Depth32Bit };
        private readonly Bitmap missing = new Bitmap(40,40);
        private readonly ToolTip searchTip = new ToolTip();
        private readonly Panel body = new Panel { Dock = DockStyle.Fill };
        private string language = "en";
        private Ledger ledger = new Ledger();
        private ItemCatalog catalog;
        private bool rebinding;
        private List<string[]> output = new List<string[]>();
        public InventoryView()
        {
            Dock = DockStyle.Fill; Padding = new Padding(12); BackColor = Color.White;
            var filters = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true };
            filters.Controls.AddRange(new Control[] { search, owners, mode, export });
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,124)); root.RowStyles.Add(new RowStyle(SizeType.Absolute,80)); root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            root.Controls.Add(filters,0,0); root.Controls.Add(summary,0,1); root.Controls.Add(body,0,2); body.Controls.Add(table); body.Controls.Add(grid); Controls.Add(root);
            table.RowTemplate.Height = 30; grid.LargeImageList = images;
            search.TextChanged += delegate { Render(); }; owners.SelectedIndexChanged += delegate { if (!rebinding) Render(); }; mode.SelectedIndexChanged += delegate { if (!rebinding) Render(); };
            export.Click += Export; ApplyLanguage("en");
        }
        private string T(string key, params object[] args) => Localization.Get(language,key,args);
        public void SetCatalog(ItemCatalog value) { catalog=value; catalog.Changed += CatalogChanged; }
        private void CatalogChanged(object sender, EventArgs e) { if (!IsDisposed) Render(false); }
        public void ApplyLanguage(string value)
        {
            language=value; rebinding=true; var previous=mode.SelectedIndex;
            mode.Items.Clear(); mode.Items.AddRange(new object[] { T("List"),T("Grid"),T("Retainers"),T("History") }); mode.SelectedIndex=previous < 0 ? 0 : previous;
            export.Text=T("Export"); search.AccessibleName=T("Search"); searchTip.SetToolTip(search,T("Search"));
            rebinding=false; RefreshData(ledger);
        }
        public void RefreshData(Ledger value)
        {
            ledger=value; var selected=(owners.SelectedItem as Choice)?.Id;
            rebinding=true; owners.Items.Clear(); owners.Items.Add(new Choice { Id="", Text=T("All") });
            foreach(var owner in ledger.Owners.OrderBy(x=>x.Kind).ThenBy(x=>x.Name)) owners.Items.Add(new Choice { Id=owner.Id, Text=owner.Name+" ["+T(owner.Kind)+"]" });
            owners.SelectedIndex=0;
            for(var i=0;i<owners.Items.Count;i++) if(((Choice)owners.Items[i]).Id==selected) owners.SelectedIndex=i;
            rebinding=false; Render();
        }
        private void Render(bool fetch = true)
        {
            if (rebinding) return;
            var selected=(owners.SelectedItem as Choice)?.Id ?? ""; var query=search.Text.Trim();
            Func<string,bool> matches = text => query.Length==0 || (text??"").IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0;
            output=new List<string[]>(); table.Rows.Clear(); table.Columns.Clear(); grid.BeginUpdate(); grid.Items.Clear(); images.Images.Clear();
            using(var g=Graphics.FromImage(missing)) { g.Clear(Color.FromArgb(55,65,82)); TextRenderer.DrawText(g,"?",Font,new Rectangle(0,0,40,40),Color.White,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter); } images.Images.Add("missing",missing);
            table.Visible=mode.SelectedIndex!=1; grid.Visible=!table.Visible;
            var ids=new HashSet<uint>(); long total=0;
            if(mode.SelectedIndex<=1)
            {
                Header("Owner","Location","Slot","Item","Quantity","HQ","Updated","State");
                foreach(var c in ledger.Containers.Where(x=>selected.Length==0 || x.OwnerId==selected).OrderBy(x=>x.OwnerId).ThenBy(x=>x.Id))
                {
                    var owner=ledger.FindOwner(c.OwnerId); if(owner==null)continue;
                    foreach(var item in c.Items.OrderBy(x=>x.Slot))
                    {
                        var info=catalog?.Get(item.Id,language); var name=info?.Name ?? "#"+item.Id;
                        if(!matches(name+" "+item.Id+" "+owner.Name+" "+T(Containers.Kind(c.Id))))continue;
                        ids.Add(item.Id); total+=item.Quantity;
                        var location=T(Containers.Kind(c.Id))+" · "+c.Id;
                        var row=new[]{owner.Name,location,(item.Slot+1).ToString(),name+" ["+item.Id+"]",item.Quantity.ToString(),item.Hq ? "HQ" : "",c.UpdatedUtc.ToLocalTime().ToString("g"),T(!c.Complete?"Partial":c.OwnershipInferred?"InferredOwner":"Cached")};
                        AddRow(row);
                        if(grid.Visible)
                        {
                            var key=item.Id.ToString(); if(info?.Icon!=null && !images.Images.ContainsKey(key)) images.Images.Add(key,info.Icon);
                            grid.Items.Add(new ListViewItem(name+"\n"+item.Quantity+(item.Hq?" HQ":""), images.Images.ContainsKey(key)?key:"missing") { ToolTipText=string.Join("\n",row) });
                        }
                    }
                }
                summary.Text=output.Count<=1?T("EmptyHint"):T("Count",output.Count-1,total);
            }
            else if(mode.SelectedIndex==2)
            {
                Header("Owner","Character","Level","Gil","Updated");
                foreach(var owner in ledger.Owners.Where(x=>x.Kind=="Retainer" && (selected.Length==0 || x.Id==selected || x.CharacterId==selected) && matches(x.Name)))
                    AddRow(new[]{owner.Name,ledger.FindOwner(owner.CharacterId)?.Name??owner.CharacterId,owner.Level.ToString(),owner.Gil.ToString(),owner.UpdatedUtc.ToLocalTime().ToString("g")});
                summary.Text=T("HistoryLimit");
            }
            else
            {
                Header("Updated","Owner","Amount","Reason");
                foreach(var entry in ledger.Entries.Where(x=>selected.Length==0 || x.OwnerId==selected).OrderByDescending(x=>x.Utc))
                {
                    var name=ledger.FindOwner(entry.OwnerId)?.Name??entry.OwnerId;
                    if(matches(name+" "+T(entry.Evidence))) AddRow(new[]{entry.Utc.ToLocalTime().ToString("g"),name,entry.Amount.ToString(CultureInfo.InvariantCulture),T(entry.Evidence)});
                }
                summary.Text=T("HistoryLimit");
            }
            grid.EndUpdate();
            if(fetch && catalog!=null && ids.Count>0) _ = catalog.LoadAsync(ids.ToArray(),language);
        }
        private void Header(params string[] keys) { var labels=keys.Select(x=>T(x)).ToArray(); output.Add(labels); foreach(var k in labels)table.Columns.Add("c"+table.Columns.Count,k); }
        private void AddRow(string[] row) { output.Add(row); table.Rows.Add(row); }
        private void Export(object sender,EventArgs e)
        {
            using(var dialog=new SaveFileDialog { Filter="CSV (*.csv)|*.csv",FileName="InventoryLedger-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".csv",OverwritePrompt=true })
            {
                if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                try { LedgerFiles.Csv(dialog.FileName,output); summary.Text=T("ExportDone"); } catch { summary.Text=T("ExportFailed"); }
            }
        }
        protected override void Dispose(bool disposing) { if(disposing) { if(catalog!=null)catalog.Changed-=CatalogChanged; images.Dispose(); missing.Dispose(); searchTip.Dispose(); } base.Dispose(disposing); }
    }
}
