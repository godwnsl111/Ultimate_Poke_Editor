using PKHeX.Core;

namespace UltimateEditor;

internal static class GenerationDialog
{
    public static GenerationSettings? Show(IWin32Window owner, int count, GenerationSettings initial, Route? single = null)
    {
        using var dialog = new Form { Text = $"선택한 {count}개 경로의 생성 조건", Width = 770, Height = 390,
            StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), AutoScroll = true };
        ComboBox Choice(string title, string[] items, int index, int width)
        {
            layout.Controls.Add(new Label { Text = title, AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
            var box = new ComboBox { Width = width, DropDownStyle = ComboBoxStyle.DropDownList };
            box.Items.AddRange(items); box.SelectedIndex = index; layout.Controls.Add(box); return box;
        }
        var shiny = Choice("이로치", ["무관", "아니오", "예"], initial.Shiny + 1, 85);
        var nature = Choice("성격", new[] { "무관" }.Concat(GameInfo.Strings.natures).ToArray(), initial.Nature + 1, 110);
        string[] abilityNames = ["무관", "일반 특성 1", "일반 특성 2", "숨겨진 특성"];
        if (single is not null)
        {
            var version = Enum.Parse<GameVersion>(single.Version);
            var blank = EntityBlank.GetBlank(GameProfile.For(version).Context);
            blank.Version = version; blank.Species = single.Species; blank.Form = single.Form;
            for (int i = 0; i < Math.Min(3, blank.PersonalInfo.AbilityCount); i++)
            {
                int id = blank.PersonalInfo.GetAbilityAtIndex(i);
                abilityNames[i + 1] += " · " + GameInfo.Strings.abilitylist[id];
            }
        }
        var ability = Choice("특성", abilityNames, initial.Ability + 1, 180);
        layout.SetFlowBreak(ability, true);
        var gender = Choice("포켓몬 성별", ["무관", "수컷", "암컷", "무성"], initial.Gender + 1, 100);
        layout.Controls.Add(new Label { Text = "만난 날짜 (체크 해제: 자동)", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        var metDate = new DateTimePicker { ShowCheckBox = true, Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd", Width = 145,
            MinDate = new DateTime(2000, 1, 1), MaxDate = new DateTime(2099, 12, 31),
            Value = (initial.MetDate ?? DateOnly.FromDateTime(DateTime.Today)).ToDateTime(TimeOnly.MinValue) };
        metDate.Checked = initial.MetDate is not null; layout.Controls.Add(metDate); layout.SetFlowBreak(metDate, true);
        var explanation = new Label { Text = "개체값 순서: HP / 공격 / 방어 / 특수공격 / 특수방어 / 스피드", AutoSize = true, Padding = new Padding(0, 12, 0, 6) };
        layout.Controls.Add(explanation); layout.SetFlowBreak(explanation, true);
        var ivs = new List<ComboBox>();
        foreach (int iv in initial.IVs) ivs.Add(Choice("", ["무관", "Z(0)", "V(31)"], iv switch { 0 => 1, 31 => 2, _ => 0 }, 88));
        layout.SetFlowBreak(ivs[^1], true);
        var all31 = new Button { Text = "모두 V(31)", AutoSize = true }; all31.Click += (_, _) => ivs.ForEach(i => i.SelectedIndex = 2);
        var random = new Button { Text = "IV 모두 무관", AutoSize = true }; random.Click += (_, _) => ivs.ForEach(i => i.SelectedIndex = 0);
        layout.Controls.Add(all31); layout.Controls.Add(random);
        layout.Controls.Add(new Label { Text = "개체당 검색 제한(초)", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        var timeout = new NumericUpDown { Minimum = 1, Maximum = 120, Value = initial.TimeoutSeconds, Width = 65 };
        layout.Controls.Add(timeout); layout.SetFlowBreak(timeout, true);
        layout.Controls.Add(new Label { Text = "특성 번호는 해당 종·폼의 특성 슬롯입니다.\n원본 TID/SID, 입수 경로, 이로치 잠금·고정 배포값을 유지합니다.\n3·4세대의 IV 3개 이상 지정은 PokeFinder IV→PID 역산을 사용합니다.\n가능한 조합이 없거나 시간 내 못 찾으면 반영하지 않습니다. IV는 원래 개체값입니다.",
            AutoSize = true, Padding = new Padding(0, 8, 0, 0) });
        dialog.Controls.Add(layout);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 42, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.Add(new Button { Text = "선택 경로에 적용", AutoSize = true, DialogResult = DialogResult.OK });
        buttons.Controls.Add(new Button { Text = "취소", DialogResult = DialogResult.Cancel });
        dialog.Controls.Add(buttons);
        if (dialog.ShowDialog(owner) != DialogResult.OK) return null;
        int[] values = ivs.Select(i => i.SelectedIndex switch { 1 => 0, 2 => 31, _ => -1 }).ToArray();
        return new(shiny.SelectedIndex - 1, nature.SelectedIndex - 1, ability.SelectedIndex - 1,
            values[0], values[1], values[2], values[3], values[4], values[5], (int)timeout.Value, gender.SelectedIndex - 1, metDate.Checked ? DateOnly.FromDateTime(metDate.Value) : null);
    }
}
