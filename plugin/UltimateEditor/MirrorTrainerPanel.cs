using PKHeX.Core;

namespace UltimateEditor;

public sealed class MirrorTrainerPanel : FlowLayoutPanel
{
    private readonly TextBox ot = new() { Width = 90, MaxLength = 7 };
    private readonly TextBox tid = new() { Width = 65, MaxLength = 5 };
    private readonly TextBox sid = new() { Width = 65, MaxLength = 5 };
    private readonly ComboBox gender = new() { Width = 64, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox language = new() { Width = 110, DropDownStyle = ComboBoxStyle.DropDownList };
    public event EventHandler? LanguageChanged;
    public int SelectedLanguage => languages[language.SelectedIndex];
    private readonly int[] languages;
    private readonly byte generation;
    private IRegionOriginReadOnly? origin;
    private readonly Label importStatus = new() { AutoSize = true, ForeColor = Color.DarkGoldenrod };
    public MirrorTrainerPanel(GameVersion partner)
    {
        Dock = DockStyle.Top; Height = 66; Padding = new Padding(6); WrapContents = true;
        var profile = partner is GameVersion.COLO or GameVersion.XD ? new GameProfile(3, 386, EntityContext.Gen3) : GameProfile.For(partner);
        generation = profile.Generation;
        ot.MaxLength = EntityBlank.GetBlank(profile.Context).MaxStringLengthTrainer;
        languages = generation >= 7 ? [1, 2, 3, 4, 5, 7, 8, 9, 10]
            : generation >= 4 || generation == 2 ? [1, 2, 3, 4, 5, 7, 8] : [1, 2, 3, 4, 5, 7];
        gender.Items.AddRange(["남", "여"]); gender.SelectedIndex = 0;
        language.Items.AddRange(["일본어", "영어", "프랑스어", "이탈리아어", "독일어", "스페인어"]);
        if (languages.Contains(8)) language.Items.Add("한국어");
        if (languages.Contains(9)) language.Items.AddRange(["중국어 간체", "중국어 번체"]);
        language.SelectedIndex = 1;
        language.SelectedIndexChanged += (_, _) => LanguageChanged?.Invoke(this, EventArgs.Empty);
        if (partner == GameVersion.BU) { language.SelectedIndex = 0; language.Enabled = false; }
        if (generation <= 2) { sid.Text = "0"; sid.Enabled = false; }
        tid.MaxLength = generation >= 7 ? 6 : 5;
        sid.MaxLength = generation >= 7 ? 4 : 5;
        if (partner is GameVersion.COLO or GameVersion.XD) gender.Enabled = false;
        if (generation == 1 || partner is GameVersion.GD or GameVersion.SI) gender.Enabled = false;
        Add($"{GameProfile.DisplayName(partner)} OT", ot);
        Add("TID", tid); Add("SID", sid); Add("성별", gender); Add("언어", language);
        var import = new Button { Text = "상대 세이브에서 가져오기", AutoSize = true };
        import.Click += (_, _) => Import(partner);
        Controls.Add(import);
        SetFlowBreak(import, true);
        importStatus.Text = "PKHeX 표시값 입력. 7세대 이후 TID는 6자리, SID도 필요합니다. SID를 모르면 상대 세이브에서 가져오세요.";
        importStatus.Padding = new Padding(0, 5, 0, 0);
        Controls.Add(importStatus);
    }
    private void Add(string name, Control field)
    {
        Controls.Add(new Label { Text = name, AutoSize = true, Padding = new Padding(0, 5, 0, 0) });
        Controls.Add(field);
    }
    public TrainerProfile Read(GameVersion version)
    {
        var name = ot.Text.Trim();
        if (name.Length == 0) throw new ArgumentException("상대 버전의 OT를 입력하세요.");
        if (generation <= 5 && languages[language.SelectedIndex] is 1 or 8 && name.Length > 5)
            throw new ArgumentException("일본어·한국어 OT는 5자 이하여야 합니다.");
        var (t, s) = TrainerIdCodec.FromDisplay(tid.Text, sid.Text, generation >= 7);
        // Character encoding legality is checked when generation is implemented.
        return new(version.ToString(), name, t, s, gender.SelectedIndex, languages[language.SelectedIndex],
            origin?.Country, origin?.Region, origin?.ConsoleRegion);
    }

    private void Import(GameVersion version)
    {
        using var picker = new OpenFileDialog { Title = $"{GameProfile.DisplayName(version)} 세이브 선택", Filter = "세이브 파일|*.*" };
        if (picker.ShowDialog() != DialogResult.OK) return;
        try
        {
            var save = SaveUtil.GetSaveFile(File.ReadAllBytes(picker.FileName))
                ?? throw new ArgumentException("지원하는 세이브 파일로 인식되지 않습니다.");
            if (!GameProfile.ImportCandidates(save).Contains(version))
                throw new ArgumentException($"{GameProfile.DisplayName(version)} 세이브가 아닙니다 ({save.Version}).");
            var resolved = TrainerLanguage.Resolve(save, languages, languages[language.SelectedIndex]);
            var index = Array.IndexOf(languages, resolved.Language);
            if (version == GameVersion.BU && resolved.Language != 1) throw new ArgumentException("일본 블루는 일본어 세이브가 필요합니다.");
            ot.Text = save.OT;
            tid.Text = (generation >= 7 ? save.ID32 % 1_000_000 : save.TID16).ToString();
            sid.Text = (generation >= 7 ? save.ID32 / 1_000_000 : generation <= 2 ? 0u : save.SID16).ToString();
            gender.SelectedIndex = save.Gender == 1 ? 1 : 0;
            language.SelectedIndex = index;
            origin = save as IRegionOriginReadOnly;
            importStatus.Text = resolved.NeedsConfirmation ? "정보 가져옴 · 해외판 언어는 파일만으로 판별 불가. 언어 선택을 확인하세요." : "정보 가져옴";
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "트레이너 가져오기"); }
    }
}
