using PKHeX.Core;

namespace UltimateEditor;

internal static class PreviewSearchDialog
{
    private sealed record Choice(int Value, string Text);
    public static InjectionPlan? Show(IWin32Window owner, InjectionPlan initial,
        Func<IWin32Window, IReadOnlyDictionary<string, GenerationSettings>, InjectionPlan?> search)
    {
        using var dialog = new Form { Text = "주입 미리보기 — 조건 지정·검색", Width = 1370, Height = 780,
            StartPosition = FormStartPosition.CenterParent, MinimumSize = new Size(960, 600) };
        var area = Screen.FromHandle(owner.Handle).WorkingArea;
        dialog.Size = new Size(Math.Min(dialog.Width, area.Width - 24), Math.Min(dialog.Height, area.Height - 24));
        dialog.MinimumSize = new Size(Math.Min(960, dialog.Width), Math.Min(600, dialog.Height));
        var session = new PreviewSearchSession(initial);
        var conditions = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = true, RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells };
        void TextColumn(string name, string title, bool editable = false) => conditions.Columns.Add(new DataGridViewTextBoxColumn {
            Name = name, HeaderText = title, ReadOnly = !editable, SortMode = DataGridViewColumnSortMode.NotSortable });
        void ChoiceColumn(string name, string title, Choice[] choices) => conditions.Columns.Add(new DataGridViewComboBoxColumn {
            Name = name, HeaderText = title, DataSource = choices, ValueMember = nameof(Choice.Value), DisplayMember = nameof(Choice.Text),
            ValueType = typeof(int), SortMode = DataGridViewColumnSortMode.NotSortable, FlatStyle = FlatStyle.Flat });
        TextColumn("Species", "번호"); TextColumn("Name", "이름"); TextColumn("Version", "입수게임");
        ChoiceColumn("Gender", "요청 성별", [new(-1, "무관"), new(0, "수컷"), new(1, "암컷"), new(2, "무성")]);
        ChoiceColumn("Shiny", "요청 이로치", [new(-1, "무관"), new(0, "아니오"), new(1, "예")]);
        ChoiceColumn("Nature", "요청 성격", new[] { new Choice(-1, "무관") }.Concat(GameInfo.Strings.natures.Select((n, i) => new Choice(i, n))).ToArray());
        TextColumn("MetDate", "요청 날짜 (자동/입력)", true);
        Choice[] ivChoices = [new(-1, "무관"), new(0, "Z(0)"), new(31, "V(31)")];
        string[] ivColumns = ["HP", "Attack", "Defense", "SpAttack", "SpDefense", "Speed"];
        string[] ivTitles = ["HP IV", "공격 IV", "방어 IV", "특공 IV", "특방 IV", "스피드 IV"];
        for (int i = 0; i < 6; i++) ChoiceColumn(ivColumns[i], ivTitles[i], ivChoices);
        ChoiceColumn("Ability", "요청 특성번호", [new(-1, "무관"), new(0, "1"), new(1, "2"), new(2, "4 (숨겨진)")]);
        TextColumn("Seconds", "검색 초", true);
        TextColumn("Period", "배포 허용기간"); TextColumn("CardDate", "카드 기록일"); TextColumn("DateSource", "날짜 근거");
        foreach (var slot in initial.Slots)
        {
            var s = slot.Settings ?? new GenerationSettings();
            var dates = DistributionCatalog.DateInfo(slot.Route);
            int index = conditions.Rows.Add(slot.Route.Species, slot.Pokemon.Nickname, slot.Route.Version,
                s.Gender, s.Shiny, s.Nature, s.MetDate?.ToString("yyyy-MM-dd") ?? "자동", s.HP, s.Attack, s.Defense, s.SpAttack, s.SpDefense, s.Speed, s.Ability, s.TimeoutSeconds, dates?.Period ?? "", dates?.CardDateText ?? "", dates?.Source ?? "");
            conditions.Rows[index].Tag = slot.Route;
        }
        var results = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells };
        var status = new Label { Dock = DockStyle.Top, Height = 62, Padding = new Padding(8) };
        var resultLabel = new Label { Dock = DockStyle.Top, Height = 26, Text = "검증된 검색 결과", Padding = new Padding(5) };
        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
        split.Panel1.Controls.Add(conditions);
        split.Panel1.Controls.Add(new Label { Dock = DockStyle.Top, Height = 27, Text = "검색 조건 — 날짜는 자동 또는 yyyy-MM-dd (카드 기록일과 공식 배포 기간은 구분)", Padding = new Padding(5) });
        split.Panel2.Controls.Add(results); split.Panel2.Controls.Add(resultLabel);
        var dateBar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 68, Padding = new Padding(6), WrapContents = true };
        dateBar.Controls.Add(new Label { Text = "만난 날짜 직접 입력", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        var dateInput = new TextBox { Width = 125, PlaceholderText = "yyyy-MM-dd", MaxLength = 10, AccessibleName = "만난 날짜 직접 입력" };
        var setDate = new Button { Text = "선택 행에 날짜 적용", AutoSize = true };
        var allDates = new Button { Text = "전체 행에 날짜 적용", AutoSize = true };
        var autoDate = new Button { Text = "선택 행 날짜 자동", AutoSize = true };
        dateBar.Controls.AddRange([dateInput, setDate, allDates, autoDate]);
        dateBar.SetFlowBreak(autoDate, true);
        dateBar.Controls.Add(new Label { Text = "위 조건 표에서 행 선택 → 날짜 입력 → 적용 → 하단 '지정 조건으로 검색'. 날짜는 yyyy-MM-dd 형식입니다.", AutoSize = true });
        dialog.Controls.Add(split); dialog.Controls.Add(dateBar); dialog.Controls.Add(status);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 46, FlowDirection = FlowDirection.RightToLeft };
        var apply = new Button { Text = "열린 세이브에 반영", AutoSize = true };
        var find = new Button { Text = "지정 조건으로 검색", AutoSize = true };
        var batch = new Button { Text = "선택 행 조건 일괄 설정", AutoSize = true };
        buttons.Controls.Add(new Button { Text = "취소", DialogResult = DialogResult.Cancel });
        buttons.Controls.Add(apply); buttons.Controls.Add(find); buttons.Controls.Add(batch);
        dialog.Controls.Add(buttons);

        void DisplayResults()
        {
            results.DataSource = session.Plan.Slots.Select(s => new {
                박스 = s.Box + 1, 슬롯 = s.Slot + 1, 번호 = s.Pokemon.Species, 이름 = s.Pokemon.Nickname,
                만난날짜 = s.Settings?.MetDate is null && DistributionCatalog.DateInfo(s.Route)?.NeedsExplicitDate == true ? "미정 (날짜 입력 필요)" : s.Pokemon.MetDate?.ToString("yyyy-MM-dd") ?? "", 알수령일 = s.Pokemon.EggMetDate?.ToString("yyyy-MM-dd") ?? "",
                날짜근거 = s.Settings?.MetDate is not null ? "직접 지정" : DistributionCatalog.DateInfo(s.Route)?.Source ?? "기본 생성값",
                성별 = s.Pokemon.Gender switch { 0 => "수컷", 1 => "암컷", _ => "무성" }, 이로치 = s.Pokemon.IsShiny, 성격 = (uint)(int)s.Pokemon.Nature < 25 ? GameInfo.Strings.natures[(int)s.Pokemon.Nature] : "-",
                특성번호 = s.Pokemon.AbilityNumber, 특성 = GameInfo.Strings.abilitylist[s.Pokemon.Ability],
                HP = s.Pokemon.IV_HP, 공격 = s.Pokemon.IV_ATK, 방어 = s.Pokemon.IV_DEF,
                특공 = s.Pokemon.IV_SPA, 특방 = s.Pokemon.IV_SPD, 스피드 = s.Pokemon.IV_SPE,
                PID = s.Pokemon.PID.ToString("X8"), 검색 = s.Generator, 역산시드 = s.Seed?.ToString("X8") ?? "",
                레벨 = s.Pokemon.CurrentLevel, 입수게임 = s.Route.Version, 개체출신 = s.Pokemon.Version,
                장소 = s.Route.LocationName, 알 = s.Pokemon.IsEgg, 경로 = s.Route.Kind.ToString(),
                배포명 = s.Route.Kind == RouteKind.Distribution ? s.Route.Description : "",
                OT = s.Pokemon.OriginalTrainerName, TID = s.Pokemon.GetDisplayTID(), SID = s.Pokemon.GetDisplaySID(),
                검사 = "적법성·체크섬·기존 슬롯 보존 통과" }).ToArray();
            status.Text = $"{session.Plan.Slots.Count}마리 / 위 표에서 성별·성격·IV·이로치·특성번호를 지정하고 ‘지정 조건으로 검색’을 누르세요.\n" +
                "아래 표는 실제 생성 결과입니다. 조건 변경 후 다시 검색해야 반영할 수 있습니다. 파일 저장은 PKHeX에서 수행하세요.";
            status.ForeColor = SystemColors.ControlText; apply.Enabled = !session.RequiresSearch;
            if (session.Plan.HasUnconfirmedDates)
            {
                status.Text = "원본 날짜가 없는 배포 카드가 있습니다. 오늘 날짜는 배포일로 사용하지 않습니다.\n위쪽 '만난 날짜 직접 입력' 칸에 수령일 입력 → 적용 → 검색 순서로 진행하세요. 날짜가 미정인 결과는 주입할 수 없습니다.";
                status.ForeColor = Color.DarkOrange;
            }
            resultLabel.Text = session.Plan.HasUnconfirmedDates ? "날짜 미확정 — 요청 날짜 입력 후 검색 필요" : "검증된 검색 결과";
        }
        void Invalidate()
        {
            session.Invalidate(); apply.Enabled = false;
            status.Text = "검색 조건이 변경되었습니다. ‘지정 조건으로 검색’을 눌러 결과를 갱신하세요.\n아래 이전 결과는 변경한 조건으로 생성된 결과가 아니므로 반영할 수 없습니다.";
            status.ForeColor = Color.DarkOrange; resultLabel.Text = "이전 검색 결과 — 변경한 조건 미적용";
        }
        void SetDates(bool all, bool automatic)
        {
            try
            {
                // Escape also allows correcting an invalid date cell from this visible toolbar.
                conditions.CancelEdit();
                var rows = all ? conditions.Rows.Cast<DataGridViewRow>().ToArray() : conditions.SelectedRows.Cast<DataGridViewRow>().ToArray();
                if (rows.Length == 0) throw new ArgumentException("위 조건 표에서 날짜를 적용할 행을 먼저 선택하세요.");
                var date = automatic ? null : DistributionDates.ParseInput(dateInput.Text);
                if (!automatic && date is null) throw new ArgumentException("날짜 입력란에 yyyy-MM-dd 형식으로 날짜를 입력하세요. 예: 2018-06-01");
                foreach (var row in rows)
                {
                    row.Cells["MetDate"].Value = date?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) ?? "자동";
                    row.ErrorText = "";
                }
                Invalidate();
            }
            catch (Exception ex) { MessageBox.Show(dialog, ex.Message, "날짜 입력 확인", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }
        setDate.Click += (_, _) => SetDates(false, false);
        allDates.Click += (_, _) => SetDates(true, false);
        autoDate.Click += (_, _) => SetDates(false, true);
        int Value(DataGridViewRow row, string name) => row.Cells[name].Value is { } value ? Convert.ToInt32(value) : throw new ArgumentException("검색 조건을 선택하세요.");
        GenerationSettings ReadRow(DataGridViewRow row)
        {
            var s = new GenerationSettings(Value(row, "Shiny"), Value(row, "Nature"), Value(row, "Ability"),
                Value(row, "HP"), Value(row, "Attack"), Value(row, "Defense"), Value(row, "SpAttack"), Value(row, "SpDefense"), Value(row, "Speed"), Value(row, "Seconds"), Value(row, "Gender"), DistributionDates.ParseInput(row.Cells["MetDate"].Value?.ToString()));
            s.Validate(); return s;
        }
        Dictionary<string, GenerationSettings> ReadAll()
        {
            if (!conditions.EndEdit()) throw new ArgumentException("입력 중인 검색 조건을 확인하세요.");
            return conditions.Rows.Cast<DataGridViewRow>().ToDictionary(r => ((Route)r.Tag!).Id, ReadRow);
        }
        conditions.CurrentCellDirtyStateChanged += (_, _) => {
            if (!conditions.IsCurrentCellDirty) return;
            Invalidate();
            if (conditions.CurrentCell is DataGridViewComboBoxCell) conditions.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        conditions.CellValueChanged += (_, e) => { if (e.RowIndex >= 0) Invalidate(); };
        conditions.DataError += (_, e) => { e.ThrowException = false; Invalidate(); if (e.RowIndex >= 0) conditions.Rows[e.RowIndex].ErrorText = "검색 조건을 확인하세요. 검색 초는 1~120 사이 정수입니다."; };
        conditions.CellValidating += (_, e) => {
            if (e.ColumnIndex == conditions.Columns["MetDate"]!.Index)
            {
                try { DistributionDates.ParseInput(e.FormattedValue?.ToString()); conditions.Rows[e.RowIndex].ErrorText = ""; }
                catch (ArgumentException ex) { e.Cancel = true; conditions.Rows[e.RowIndex].ErrorText = ex.Message; }
                return;
            }
            if (e.ColumnIndex != conditions.Columns["Seconds"]!.Index) return;
            e.Cancel = !int.TryParse(e.FormattedValue?.ToString(), out int seconds) || seconds is < 1 or > 120;
            conditions.Rows[e.RowIndex].ErrorText = e.Cancel ? "검색 초는 1~120 사이 정수로 입력하세요." : "";
        };
        find.Click += (_, _) => {
            try
            {
                var requested = ReadAll(); Invalidate();
                var next = search(dialog, requested);
                if (next is null) { status.Text = "검색을 완료하지 못했습니다. 조건을 조정해 다시 검색하세요. 세이브는 변경하지 않았습니다."; return; }
                session.Accept(next, requested); DisplayResults();
            }
            catch (Exception ex) { MessageBox.Show(dialog, ex.Message, "검색 조건 확인", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        };
        batch.Click += (_, _) => {
            try
            {
                if (!conditions.EndEdit()) return;
                var selected = conditions.SelectedRows.Cast<DataGridViewRow>().OrderBy(r => r.Index).ToArray();
                if (selected.Length == 0) throw new ArgumentException("조건을 바꿀 행을 선택하세요. Ctrl/Shift로 여러 행을 선택할 수 있습니다.");
                var updated = GenerationDialog.Show(dialog, selected.Length, ReadRow(selected[0]), selected.Length == 1 ? (Route)selected[0].Tag! : null);
                if (updated is null) return;
                foreach (var row in selected)
                {
                    row.Cells["Gender"].Value = updated.Gender; row.Cells["Shiny"].Value = updated.Shiny; row.Cells["Nature"].Value = updated.Nature; row.Cells["Ability"].Value = updated.Ability;
                    for (int i = 0; i < 6; i++) row.Cells[ivColumns[i]].Value = updated.IVs[i];
                    row.Cells["Seconds"].Value = updated.TimeoutSeconds;
                    row.Cells["MetDate"].Value = updated.MetDate?.ToString("yyyy-MM-dd") ?? "자동";
                }
                Invalidate();
            }
            catch (Exception ex) { MessageBox.Show(dialog, ex.Message, "조건 설정"); }
        };
        InjectionPlan? accepted = null;
        apply.Click += (_, _) => {
            try { accepted = session.Confirm(ReadAll()); dialog.DialogResult = DialogResult.OK; }
            catch (Exception ex) { MessageBox.Show(dialog, ex.Message, "다시 검색 필요"); }
        };
        dialog.Shown += (_, _) => split.SplitterDistance = Math.Max(150, split.Height / 2);
        DisplayResults();
        return dialog.ShowDialog(owner) == DialogResult.OK ? accepted : null;
    }
}
