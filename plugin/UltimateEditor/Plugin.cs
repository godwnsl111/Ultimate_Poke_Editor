using System.Text.Json;
using PKHeX.Core;

namespace UltimateEditor;

public sealed class Plugin : IPlugin
{
    public string Name => "Ultimate Editor";
    public int Priority => 0;
    public ISaveFileProvider SaveFileEditor { get; private set; } = null!;
    private Form? window;

    public void Initialize(params object[] args)
    {
        SaveFileEditor = args.OfType<ISaveFileProvider>().First();
        var menu = args.OfType<ToolStrip>().First();
        var parent = menu.Items.Find("Menu_Tools", true).OfType<ToolStripMenuItem>().FirstOrDefault();
        var item = new ToolStripMenuItem("Ultimate Editor — 입수 후보 목록");
        item.Click += (_, _) => ShowCatalog();
        if (parent is null) menu.Items.Add(item); else parent.DropDownItems.Add(item);
    }

    public void NotifySaveLoaded() { window?.Close(); }
    public bool TryLoadFile(string filePath) => false;

    private void ShowCatalog()
    {
        try
        {
            if (window is { IsDisposed: false }) { window.Activate(); return; }
            var save = SaveFileEditor.SAV;
            var report = EncounterCatalog.Build(save.Version);
            window = new Form { Text = "Ultimate Editor — 입수 후보 검토", Width = 1150, Height = 700,
                StartPosition = FormStartPosition.CenterScreen };
            var info = new Label { Dock = DockStyle.Top, Height = 76, Padding = new Padding(8) };
            var scopeBar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 38, Padding = new Padding(6) };
            scopeBar.Controls.Add(new Label { Text = "대상 범위", AutoSize = true, Padding = new Padding(0, 5, 6, 0) });
            var scopeChoice = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
            scopeChoice.Items.AddRange(["해당 지방", "해당 게임"]);
            scopeChoice.SelectedIndex = 0;
            scopeBar.Controls.Add(scopeChoice);
            var tabs = new TabControl { Dock = DockStyle.Fill };
            void RefreshReport()
            {
                info.Text = $"게임: {save.Version}   트레이너: {save.OT}   TID: {save.TID16}   SID: {save.SID16}\n" +
                $"선택 {report.Selected.Count}개 경로 / {report.Selected.Select(r => r.Species).Distinct().Count()}종. " +
                "교배 제외 · 야생 최저 레벨 · 야생 미진화체 우선\n" +
                "읽기 전용. 배포 검증·NPC 교환 요구 조건/최저 레벨 보정·개체 생성·세이브 주입은 아직 지원하지 않습니다.";
            while (tabs.TabPages.Count > 0) { var page = tabs.TabPages[0]; tabs.TabPages.Remove(page); page.Dispose(); }
            var selected = report.Selected.Select(r => new {
                번호 = r.Species, 이름 = ((Species)r.Species).ToString(), 경로 = r.Kind.ToString(),
                최저레벨 = r.MinLevel, 최고레벨 = r.MaxLevel, 장소ID = r.Location,
                방법 = r.Method, 이벤트필요 = r.RequiresEvent, r.Id }).ToArray();
            AddGrid(tabs, "선택된 경로", selected);
            AddGrid(tabs, "제외 근거", report.Excluded.Select(x => new {
                번호 = x.Route.Species, 이름 = ((Species)x.Route.Species).ToString(),
                경로 = x.Route.Kind.ToString(), 레벨 = x.Route.MinLevel,
                장소ID = x.Route.Location, 사유 = x.Reason }).ToArray());
            }
            RefreshReport();
            scopeChoice.SelectedIndexChanged += (_, _) => {
                try {
                    report = EncounterCatalog.Build(save.Version,
                        scopeChoice.SelectedIndex == 0 ? CatalogScope.Region : CatalogScope.Game);
                    RefreshReport();
                }
                catch (Exception ex) { MessageBox.Show(window, ex.Message, Name); }
            };
            var export = new Button { Text = "검토 보고서 JSON 저장", Dock = DockStyle.Bottom, Height = 36 };
            export.Click += (_, _) => {
                using var dialog = new SaveFileDialog { Filter = "JSON|*.json", FileName = $"ultimate-editor-{save.Version}-{report.Mode}.json" };
                if (dialog.ShowDialog(window) != DialogResult.OK) return;
                try { File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } })); }
                catch (Exception ex) { MessageBox.Show(window, ex.Message, Name); }
            };
            window.Controls.Add(tabs); window.Controls.Add(export); window.Controls.Add(scopeBar); window.Controls.Add(info);
            window.Show();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, Name, MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private static void AddGrid(TabControl tabs, string title, object data)
    {
        var page = new TabPage(title);
        page.Controls.Add(new DataGridView { Dock = DockStyle.Fill, ReadOnly = true,
            AutoGenerateColumns = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false, DataSource = data });
        tabs.TabPages.Add(page);
    }
}
