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
            var candidates = GameProfile.ResolveCandidates(save.Version);
            var version = candidates.Length == 1 ? candidates[0] : ChooseVersion(candidates);
            if (version == GameVersion.Invalid) return;
            var report = MirrorCatalog.Build(version, false);
            var partners = MirrorCatalog.Partners(version);
            window = new Form { Text = "Ultimate Editor — 입수 후보 검토", Width = 1150, Height = 700,
                StartPosition = FormStartPosition.CenterScreen };
            var info = new Label { Dock = DockStyle.Top, Height = 76, Padding = new Padding(8) };
            var scopeBar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 38, Padding = new Padding(6) };
            scopeBar.Controls.Add(new Label { Text = "게임 전체 입수 후보", AutoSize = true, Padding = new Padding(0, 5, 6, 0) });
            var mirrorChoice = new CheckBox { Text = $"거울 버전 포함 ({string.Join(" / ", partners.Select(GameProfile.DisplayName))})",
                AutoSize = true, Padding = new Padding(10, 2, 0, 0) };
            if (partners.Length == 0) { mirrorChoice.Text = "거울 버전 없음 (단일 작품)"; mirrorChoice.Enabled = false; }
            scopeBar.Controls.Add(mirrorChoice);
            scopeBar.Controls.Add(new Label { Text = "파란 행: 상대 버전 전용", AutoSize = true,
                ForeColor = Color.DarkBlue, Padding = new Padding(10, 5, 0, 0) });
            var trainerPanel = new Panel { Dock = DockStyle.Top, Height = partners.Length * 70, Visible = false };
            var trainerInputs = partners.ToDictionary(v => v, v => new MirrorTrainerPanel(v));
            foreach (var partner in partners.Reverse()) trainerPanel.Controls.Add(trainerInputs[partner]);
            var tabs = new TabControl { Dock = DockStyle.Fill };
            void RefreshReport()
            {
                info.Text = $"게임: {GameProfile.DisplayName(version)}   트레이너: {save.OT}   TID: {save.GetDisplayTID()}   SID: {save.GetDisplaySID()}\n" +
                $"선택 {report.Selected.Count}개 경로 / {report.Selected.Select(r => r.Species).Distinct().Count()}종. " +
                "교배 제외 · 야생 최저 레벨 · 야생 미진화체 우선\n" +
                "읽기 전용. 배포 검증·NPC 교환 요구 조건/최저 레벨 보정·개체 생성·세이브 주입은 아직 지원하지 않습니다.";
            while (tabs.TabPages.Count > 0) { var page = tabs.TabPages[0]; tabs.TabPages.Remove(page); page.Dispose(); }
            var selected = report.Selected.Select(r => new {
                번호 = r.Species, 이름 = ((Species)r.Species).ToString(), 경로 = r.Kind.ToString(),
                출신버전 = r.Version, 폼 = r.Form, 구분 = r.Version == version.ToString() ? "기본 버전" : "상대 버전 전용",
                최저레벨 = r.MinLevel, 최고레벨 = r.MaxLevel, 장소 = r.LocationName, 장소ID = r.Location,
                알수령장소ID = r.EggLocation,
                방법 = r.Method, 이벤트필요 = r.RequiresEvent, r.Id }).ToArray();
            var selectedGrid = AddGrid(tabs, "선택된 경로", selected);
            selectedGrid.CellFormatting += (_, e) => {
                if (e.RowIndex < 0) return;
                var row = selectedGrid.Rows[e.RowIndex];
                if (row.Cells["출신버전"].Value?.ToString() == version.ToString()) return;
                e.CellStyle!.BackColor = Color.FromArgb(220, 238, 255);
                e.CellStyle.ForeColor = Color.Black;
                e.CellStyle.SelectionBackColor = Color.SteelBlue;
                e.CellStyle.SelectionForeColor = Color.White;
            };
            AddGrid(tabs, "제외 근거", report.Excluded.Select(x => new {
                번호 = x.Route.Species, 이름 = ((Species)x.Route.Species).ToString(),
                경로 = x.Route.Kind.ToString(), 레벨 = x.Route.MinLevel,
                장소 = x.Route.LocationName, 장소ID = x.Route.Location, 알수령장소ID = x.Route.EggLocation, 사유 = x.Reason }).ToArray());
            }
            RefreshReport();
            void Rebuild()
            {
                try {
                    report = MirrorCatalog.Build(version, mirrorChoice.Checked);
                    trainerPanel.Visible = mirrorChoice.Checked;
                    RefreshReport();
                }
                catch (Exception ex) { MessageBox.Show(window, ex.Message, Name); }
            }
            mirrorChoice.CheckedChanged += (_, _) => Rebuild();
            var export = new Button { Text = "검토 보고서 JSON 저장", Dock = DockStyle.Bottom, Height = 36 };
            export.Click += (_, _) => {
                TrainerProfile[] mirrorTrainers;
                try { mirrorTrainers = mirrorChoice.Checked ? partners.Select(v => trainerInputs[v].Read(v)).ToArray() : []; }
                catch (ArgumentException ex) { MessageBox.Show(window, ex.Message, Name); return; }
                using var dialog = new SaveFileDialog { Filter = "JSON|*.json", FileName = $"ultimate-editor-{version}{(mirrorChoice.Checked ? "-mirror" : "")}.json" };
                if (dialog.ShowDialog(window) != DialogResult.OK) return;
                try { File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(new { Catalog = report, MirrorTrainers = mirrorTrainers }, new JsonSerializerOptions { WriteIndented = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } })); }
                catch (Exception ex) { MessageBox.Show(window, ex.Message, Name); }
            };
            window.Controls.Add(tabs); window.Controls.Add(export); window.Controls.Add(trainerPanel); window.Controls.Add(scopeBar); window.Controls.Add(info);
            window.Show();
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, Name, MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private static GameVersion ChooseVersion(GameVersion[] candidates)
    {
        if (candidates.Length == 0) throw new NotSupportedException("이 세이브의 본가 게임 출현 조회를 지원하지 않습니다.");
        using var picker = new Form { Text = "세이브 세부 버전 확인", Width = 470, Height = 190, StartPosition = FormStartPosition.CenterScreen };
        var label = new Label { Text = "세이브만으로 정확한 버전을 구분할 수 없습니다.\n기본 게임을 선택하세요. 세이브의 버전 값은 변경하지 않습니다.", Dock = DockStyle.Top, Height = 55 };
        var choice = new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
        choice.Items.AddRange(candidates.Select(GameProfile.DisplayName).Cast<object>().ToArray());
        choice.SelectedIndex = 0;
        var confirm = new Button { Text = "적용", Dock = DockStyle.Bottom, DialogResult = DialogResult.OK };
        picker.Controls.Add(choice); picker.Controls.Add(label); picker.Controls.Add(confirm);
        picker.AcceptButton = confirm;
        return picker.ShowDialog() == DialogResult.OK ? candidates[choice.SelectedIndex] : GameVersion.Invalid;
    }

    private static DataGridView AddGrid(TabControl tabs, string title, object data)
    {
        var page = new TabPage(title);
        var grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true,
            AutoGenerateColumns = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false, DataSource = data };
        page.Controls.Add(grid);
        tabs.TabPages.Add(page);
        return grid;
    }
}
