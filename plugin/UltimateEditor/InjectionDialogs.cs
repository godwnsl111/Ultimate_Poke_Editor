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
        button.Click += (_, _) => { cancel.Cancel(); button.Enabled = false; label.Text = "검색을 취소하고 있습니다."; };
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

    public static InjectionPlan? Confirm(IWin32Window owner, InjectionPlan plan,
        Func<IWin32Window, IReadOnlyDictionary<string, GenerationSettings>, InjectionPlan?> search) =>
        PreviewSearchDialog.Show(owner, plan, search);
}
