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
            var scopeBar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 64, Padding = new Padding(6) };
            scopeBar.Controls.Add(new Label { Text = "게임 전체 입수 후보", AutoSize = true, Padding = new Padding(0, 5, 6, 0) });
            var mirrorChoice = new CheckBox { Text = $"거울 버전 포함 ({string.Join(" / ", partners.Select(GameProfile.DisplayName))})",
                AutoSize = true, Padding = new Padding(10, 2, 0, 0) };
            if (partners.Length == 0) { mirrorChoice.Text = "거울 버전 없음 (단일 작품)"; mirrorChoice.Enabled = false; }
            scopeBar.Controls.Add(mirrorChoice);
            var showDistributions = new CheckBox { Text = "직접 배포 표시 (중복 허용)", Checked = true, AutoSize = true };
            if (GameProfile.For(version).Generation < 4) { showDistributions.Text = "직접 배포: 카드 DB는 4세대 이후"; showDistributions.Enabled = false; }
            scopeBar.Controls.Add(showDistributions);
            var distributionLanguage = new ComboBox { Width = 205, DropDownStyle = ComboBoxStyle.DropDownList, Enabled = showDistributions.Enabled };
            int[] distributionLanguages = [0, -1, 1, 2, 3, 4, 5, 7, 8, 9, 10];
            distributionLanguage.Items.AddRange(["배포: 현재 수령 언어 (공통 포함)", "배포: 모든 언어 (검토용)", "배포: 일본어 + 공통", "배포: 영어 + 공통", "배포: 프랑스어 + 공통", "배포: 이탈리아어 + 공통", "배포: 독일어 + 공통", "배포: 스페인어 + 공통", "배포: 한국어 + 공통", "배포: 중국어 간체 + 공통", "배포: 중국어 번체 + 공통"]);
            distributionLanguage.SelectedIndex = 0;
            scopeBar.Controls.Add(distributionLanguage);
            var linkedChoice = new CheckBox { Text = LinkedCatalog.HasRadar(version) ? "드림 AR 서처 포함" : "콜로세움 / XD 포함", AutoSize = true, Enabled = LinkedCatalog.IsSupported(version) };
            scopeBar.Controls.Add(linkedChoice);
            GameVersion[] linkedSources = LinkedCatalog.HasGameCube(version) ? [GameVersion.COLO, GameVersion.XD] : [];
            var linkedInputs = linkedSources.ToDictionary(v => v, v => new MirrorTrainerPanel(v));
            var linkedPanel = new Panel { Dock = DockStyle.Top, Height = linkedSources.Length * 70, Visible = false };
            foreach (var source in linkedSources.Reverse()) linkedPanel.Controls.Add(linkedInputs[source]);
            scopeBar.Controls.Add(new Label { Text = "파란색: 상대 버전 / 금색: 직접 배포 / 보라색: 연동", AutoSize = true,
                ForeColor = Color.DarkBlue, Padding = new Padding(10, 5, 0, 0) });
            var trainerPanel = new Panel { Dock = DockStyle.Top, Height = partners.Length * 70, Visible = false };
            var trainerInputs = partners.ToDictionary(v => v, v => new MirrorTrainerPanel(v));
            foreach (var partner in partners.Reverse()) trainerPanel.Controls.Add(trainerInputs[partner]);
            var tabs = new TabControl { Dock = DockStyle.Fill };
            DataGridView? selectedGrid = null;
            InjectionPlan? lastPlan = null;
            var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40 };
            var selectAll = new Button { Text = "가능 항목 선택 (종/폼당 1)", AutoSize = true };
            var clear = new Button { Text = "선택 해제", AutoSize = true };
            var preview = new Button { Text = "선택 개체 생성·주입 미리보기", AutoSize = true };
            var undo = new Button { Text = "이번 주입 취소", AutoSize = true, Enabled = false };
            var reviewed = new CheckBox { Text = "교환·연동 선행 조건 확인", AutoSize = true };
            actions.Controls.AddRange([selectAll, clear, preview, undo, reviewed]);
            void RefreshReport()
            {
                reviewed.Checked = false;
                int ReceivingLanguage(Route r) => r.Version == version.ToString() ? save.Language : trainerInputs[Enum.Parse<GameVersion>(r.Version)].SelectedLanguage;
                bool VisibleRoute(Route r)
                {
                    if (r.Kind != RouteKind.Distribution) return true;
                    if (!showDistributions.Checked) return false;
                    int filter = distributionLanguages[distributionLanguage.SelectedIndex];
                    if (filter == -1) return true;
                    if (filter == 0) filter = ReceivingLanguage(r);
                    return DistributionCatalog.MatchesLanguage(r, filter);
                }
                var visibleRoutes = report.Selected.Where(VisibleRoute).ToArray();
                info.Text = $"게임: {GameProfile.DisplayName(version)}   트레이너: {save.OT}   TID: {save.GetDisplayTID()}   SID: {save.GetDisplaySID()}\n" +
                $"표시 {visibleRoutes.Length}개 경로 / {visibleRoutes.Select(r => r.Species).Distinct().Count()}종. " +
                "교배 제외 · 야생 최저 레벨 · 야생 미진화체 우선\n" +
                "이벤트 포획은 선행 조건 확인 없이 선택 가능. 직접 배포는 수령 언어별 표시·카드별 중복 가능. 동적 폼 미지원. 파일 저장은 PKHeX에서 수행하세요.";
            while (tabs.TabPages.Count > 0) { var page = tabs.TabPages[0]; tabs.TabPages.Remove(page); page.Dispose(); }
            var selected = visibleRoutes.Select(r => new {
                번호 = r.Species, 이름 = ((Species)r.Species).ToString(), 경로 = r.Kind.ToString(),
                입수버전 = r.Version, 연동출처 = r.LinkedSource, 폼 = r.Form, 구분 = r.Version == version.ToString() ? "기본 버전" : "상대 버전 전용",
                주입지원 = InjectionDialogs.IsSupported(r) && AcquisitionRequirements.LanguageIssue(r, ReceivingLanguage(r)) is null,
                입수언어안내 = AcquisitionRequirements.LanguageIssue(r, ReceivingLanguage(r)) ?? "",
                배포명 = r.Kind == RouteKind.Distribution ? r.Description : "", 배포수령언어 = DistributionCatalog.ReceiverLanguageLabel(r), 선행조건 = r.Requirement,
                최저레벨 = r.MinLevel, 최고레벨 = r.MaxLevel, 장소 = r.LocationName, 장소ID = r.Location,
                알수령장소ID = r.EggLocation,
                방법 = r.Method, 이벤트필요 = r.RequiresEvent, r.Id }).ToArray();
            selectedGrid = AddGrid(tabs, "선택된 경로", selected);
            selectedGrid.ReadOnly = false;
            foreach (DataGridViewColumn column in selectedGrid.Columns) column.ReadOnly = true;
            selectedGrid.Columns.Insert(0, new DataGridViewCheckBoxColumn { Name = "추가", HeaderText = "추가", ReadOnly = false });
            selectedGrid.Columns.Insert(1, new DataGridViewTextBoxColumn { Name = "교환레벨", HeaderText = "교환 레벨", ReadOnly = false });
            selectedGrid.DataBindingComplete += (_, _) => {
                foreach (DataGridViewRow row in selectedGrid.Rows)
                {
                    var route = report.Selected.Single(r => r.Id == row.Cells["Id"].Value?.ToString());
                    if (route.Kind == RouteKind.Trade && route.TradeLevelKnown) row.Cells["교환레벨"].Value = route.MinLevel;
                }
            };
            selectedGrid.CellBeginEdit += (_, e) => {
                var id = selectedGrid.Rows[e.RowIndex].Cells["Id"].Value?.ToString();
                var route = report.Selected.Single(r => r.Id == id);
                if (!InjectionDialogs.IsSupported(route) || selectedGrid.Columns[e.ColumnIndex].Name == "교환레벨" && route.Kind != RouteKind.Trade) e.Cancel = true;
            };
            selectedGrid.CellFormatting += (_, e) => {
                if (e.RowIndex < 0) return;
                var row = selectedGrid.Rows[e.RowIndex];
                var route = report.Selected.Single(r => r.Id == row.Cells["Id"].Value?.ToString());
                if (route.Kind == RouteKind.Linked) { e.CellStyle!.BackColor = Color.FromArgb(237, 222, 255); return; }
                if (route.Kind == RouteKind.Distribution)
                { e.CellStyle!.BackColor = Color.FromArgb(255, 242, 195); e.CellStyle.ForeColor = Color.Black; return; }
                if (row.Cells["입수버전"].Value?.ToString() == version.ToString()) return;
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
            void Mark(bool value)
            {
                if (selectedGrid is null) return;
                var seen = new HashSet<(ushort, byte)>();
                foreach (DataGridViewRow row in selectedGrid.Rows)
                {
                    var route = report.Selected.Single(r => r.Id == row.Cells["Id"].Value?.ToString());
                    row.Cells["추가"].Value = value && route.Kind != RouteKind.Distribution && !route.NeedsReview && InjectionDialogs.IsSupported(route) && seen.Add((route.Species, route.Form));
                }
            }
            selectAll.Click += (_, _) => Mark(true);
            clear.Click += (_, _) => Mark(false);
            preview.Click += (_, _) => {
                try
                {
                    if (!ReferenceEquals(SaveFileEditor.SAV, save)) throw new InvalidOperationException("열린 세이브가 변경됐습니다. 창을 다시 여세요.");
                    selectedGrid!.EndEdit();
                    var ids = selectedGrid.Rows.Cast<DataGridViewRow>().Where(r => r.Cells["추가"].Value is true)
                        .Select(r => r.Cells["Id"].Value?.ToString()).ToHashSet();
                    var chosen = report.Selected.Where(r => ids.Contains(r.Id)).ToArray();
                    if (chosen.Length == 0) throw new ArgumentException("추가할 포켓몬의 체크박스를 선택하세요.");
                    var tradeLevels = new Dictionary<string, byte>();
                    foreach (var route in chosen.Where(r => r.Kind == RouteKind.Trade))
                    {
                        var row = selectedGrid.Rows.Cast<DataGridViewRow>().Single(r => r.Cells["Id"].Value?.ToString() == route.Id);
                        var text = row.Cells["교환레벨"].Value?.ToString();
                        if (string.IsNullOrWhiteSpace(text) && route.TradeLevelKnown) tradeLevels[route.Id] = route.MinLevel;
                        else if (byte.TryParse(text, out var level) && level >= route.MinLevel && level <= route.MaxLevel) tradeLevels[route.Id] = level;
                        else throw new ArgumentException($"{route.Species}: 교환 레벨을 {route.MinLevel}~{route.MaxLevel} 범위로 입력하세요.");
                    }
                    var trainerData = chosen.Select(r => Enum.Parse<GameVersion>(r.Version)).Distinct().Where(v => v != version)
                        .ToDictionary(v => v, v => trainerInputs[v].Read(v));
                    var linkedTrainerData = chosen.Where(r => r.LinkedSource is "COLO" or "XD").Select(r => Enum.Parse<GameVersion>(r.LinkedSource)).Distinct()
                        .ToDictionary(v => v, v => linkedInputs[v].Read(v));
                    var snapshot = InjectionPlan.CloneSave(save);
                    int startBox = SaveFileEditor.CurrentBox;
                    var reviewConfirmed = reviewed.Checked;
                    var plan = InjectionDialogs.Prepare(window, token => InjectionPlan.Prepare(save, version, chosen, trainerData, startBox, token, snapshot, reviewConfirmed, tradeLevels, linkedTrainerData));
                    if (plan is null || !InjectionDialogs.Confirm(window, plan)) return;
                    plan.Commit(SaveFileEditor.SAV);
                    lastPlan = plan; undo.Enabled = true;
                    SaveFileEditor.ReloadSlots();
                    Mark(false);
                    MessageBox.Show(window, $"{plan.Slots.Count}마리를 열린 세이브에 반영했습니다. 파일 저장은 PKHeX에서 수행하세요.", Name);
                }
                catch (Exception ex) { MessageBox.Show(window, ex.Message, Name, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            };
            undo.Click += (_, _) => {
                try { lastPlan!.Undo(SaveFileEditor.SAV); SaveFileEditor.ReloadSlots(); undo.Enabled = false; }
                catch (Exception ex) { MessageBox.Show(window, ex.Message, Name); }
            };
            void Rebuild()
            {
                try {
                    report = MirrorCatalog.Build(version, mirrorChoice.Checked, linkedChoice.Checked);
                    linkedPanel.Visible = linkedChoice.Checked && linkedSources.Length > 0;
                    trainerPanel.Visible = mirrorChoice.Checked;
                    RefreshReport();
                }
                catch (Exception ex) { MessageBox.Show(window, ex.Message, Name); }
            }
            linkedChoice.CheckedChanged += (_, _) => Rebuild();
            mirrorChoice.CheckedChanged += (_, _) => Rebuild();
            showDistributions.CheckedChanged += (_, _) => RefreshReport();
            distributionLanguage.SelectedIndexChanged += (_, _) => RefreshReport();
            foreach (var input in trainerInputs.Values) input.LanguageChanged += (_, _) => RefreshReport();
            var export = new Button { Text = "검토 보고서 JSON 저장", Dock = DockStyle.Bottom, Height = 36 };
            export.Click += (_, _) => {
                TrainerProfile[] mirrorTrainers;
                TrainerProfile[] linkedTrainers;
                try { mirrorTrainers = mirrorChoice.Checked ? partners.Select(v => trainerInputs[v].Read(v)).ToArray() : [];
                    linkedTrainers = linkedChoice.Checked ? linkedSources.Select(v => linkedInputs[v].Read(v)).ToArray() : []; }
                catch (ArgumentException ex) { MessageBox.Show(window, ex.Message, Name); return; }
                using var dialog = new SaveFileDialog { Filter = "JSON|*.json", FileName = $"ultimate-editor-{version}{(mirrorChoice.Checked ? "-mirror" : "")}.json" };
                if (dialog.ShowDialog(window) != DialogResult.OK) return;
                try { File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(new { Catalog = report, DistributionLanguageFilter = distributionLanguage.Text, MirrorTrainers = mirrorTrainers, LinkedTrainers = linkedTrainers }, new JsonSerializerOptions { WriteIndented = true,
                    Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() } })); }
                catch (Exception ex) { MessageBox.Show(window, ex.Message, Name); }
            };
            window.Controls.Add(tabs); window.Controls.Add(export); window.Controls.Add(actions); window.Controls.Add(trainerPanel); window.Controls.Add(linkedPanel); window.Controls.Add(scopeBar); window.Controls.Add(info);
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
