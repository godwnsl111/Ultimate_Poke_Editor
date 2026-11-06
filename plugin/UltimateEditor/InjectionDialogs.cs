using PKHeX.Core;

namespace UltimateEditor;

internal static class InjectionDialogs
{
    public static bool IsSupported(Route r) => r.Kind is RouteKind.Wild or RouteKind.Gift or RouteKind.GiftEgg or RouteKind.Trade or RouteKind.Event or RouteKind.Distribution or RouteKind.Linked
        && r.Form < EncounterUtil.FormDynamic;

    public static InjectionPlan? Prepare(IWin32Window owner, Func<CancellationToken, InjectionPlan> build)
    {
        using var dialog = new Form { Text = "개체 생성·복제본 검증", Width = 530, Height = 165,
            ControlBox = false, StartPosition = FormStartPosition.CenterParent };
        using var cancel = new CancellationTokenSource();
        var label = new Label { Text = "생성 및 검증 중입니다. 열린 세이브는 변경하지 않습니다.", Dock = DockStyle.Top, Height = 45 };
        var button = new Button { Text = "취소", Dock = DockStyle.Bottom };
        button.Click += (_, _) => { cancel.Cancel(); button.Enabled = false; label.Text = "현재 개체 생성이 끝나면 취소합니다."; };
        dialog.Controls.Add(new ProgressBar { Dock = DockStyle.Fill, Style = ProgressBarStyle.Marquee });
        dialog.Controls.Add(label); dialog.Controls.Add(button);
        InjectionPlan? result = null;
        Exception? failure = null;
        dialog.Shown += async (_, _) => {
            try { result = await Task.Run(() => build(cancel.Token)); if (cancel.IsCancellationRequested) result = null; }
            catch (OperationCanceledException) { }
            catch (Exception ex) { failure = ex; }
            dialog.Close();
        };
        dialog.ShowDialog(owner);
        if (failure is not null) MessageBox.Show(owner, failure.Message, "반영하지 않았습니다", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return result;
    }

    public static bool Confirm(IWin32Window owner, InjectionPlan plan)
    {
        using var dialog = new Form { Text = "주입 미리보기 — 아직 반영 전", Width = 1050, Height = 550,
            StartPosition = FormStartPosition.CenterParent };
        dialog.Controls.Add(new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
            DataSource = plan.Slots.Select(s => new {
                박스 = s.Box + 1, 슬롯 = s.Slot + 1, 번호 = s.Pokemon.Species,
                이름 = s.Pokemon.Nickname, 레벨 = s.Pokemon.CurrentLevel, 입수게임 = s.Route.Version, 개체출신 = s.Pokemon.Version,
                경로 = s.Route.Kind.ToString(), 배포명 = s.Route.Kind == RouteKind.Distribution ? s.Route.Description : "",
                장소 = s.Route.LocationName, 알 = s.Pokemon.IsEgg, 이로치 = s.Pokemon.IsShiny,
                OT = s.Pokemon.OriginalTrainerName, TID = s.Pokemon.GetDisplayTID(), SID = s.Pokemon.GetDisplaySID(),
                검사 = "개체·체크섬·기존 슬롯 보존 통과" }).ToArray() });
        dialog.Controls.Add(new Label { Dock = DockStyle.Top, Height = 62,
            Text = $"{plan.Slots.Count}마리 / 아래 빈 슬롯에 배치합니다. 기존 개체는 이동·덮어쓰기하지 않습니다.\n" +
            "기본 입수값으로 생성했습니다. 선물 알은 템플릿의 생성 상태를 표시합니다.\n" +
            "적법성 통과와 선행 입수 조건 충족은 별개입니다. 반영 후 파일 저장은 PKHeX에서 수행하세요." });
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.Add(new Button { Text = "열린 세이브에 반영", AutoSize = true, DialogResult = DialogResult.OK });
        buttons.Controls.Add(new Button { Text = "취소", DialogResult = DialogResult.Cancel });
        dialog.Controls.Add(buttons);
        return dialog.ShowDialog(owner) == DialogResult.OK;
    }
}
