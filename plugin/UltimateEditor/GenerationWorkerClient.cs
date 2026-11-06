using System.Diagnostics;
using System.Text.Json;
using PKHeX.Core;

namespace UltimateEditor;

public static class GenerationWorkerClient
{
    public static string? WorkerPathOverride { get; set; }
    public static (PKM Pokemon, string Method, uint? Seed) Generate(Route route, ITrainerInfo tr, GenerationSettings settings, CancellationToken token)
    {
        settings.Validate();
        string folder = Path.GetDirectoryName(typeof(GenerationWorkerClient).Assembly.Location)!;
        string[] candidates = [Path.Combine(folder, "UltimateEditor.Generator", "UltimateEditor.Generator.exe"),
            Path.GetFullPath(Path.Combine(folder, "..", "ultimate_editor", "dist", "UltimateEditor.Generator", "UltimateEditor.Generator.exe")),
            Path.GetFullPath(Path.Combine(folder, "..", "UltimateEditor.Generator", "UltimateEditor.Generator.exe"))];
        string worker = WorkerPathOverride ?? candidates.FirstOrDefault(File.Exists) ?? candidates[0];
        if (!File.Exists(worker)) throw new FileNotFoundException("생성 검색기가 없습니다. ultimate_editor/dist/UltimateEditor.Generator 폴더를 유지하거나 검색기 폴더를 DLL 옆에 함께 설치하세요.", worker);
        var request = new GenerationRequest(route, new(route.Version, tr.OT, tr.TID16, tr.SID16, tr.Gender, tr.Language,
            tr is IRegionOriginReadOnly geo ? geo.Country : null,
            tr is IRegionOriginReadOnly geo2 ? geo2.Region : null,
            tr is IRegionOriginReadOnly geo3 ? geo3.ConsoleRegion : null), settings);
        using var process = new Process { StartInfo = new(worker) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true } };
        token.ThrowIfCancellationRequested();
        process.Start();
        try
        {
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            process.StandardInput.WriteLine(JsonSerializer.Serialize(request)); process.StandardInput.Close();
            var timer = Stopwatch.StartNew();
            while (!process.WaitForExit(50))
            {
                token.ThrowIfCancellationRequested();
                if (timer.Elapsed.TotalSeconds > settings.TimeoutSeconds)
                    throw new TimeoutException($"{route.Species}: {settings.TimeoutSeconds}초 안에 지정 조건을 만족하는 결과를 찾지 못했습니다. 검색 시간이 부족하거나 가능한 조합이 없을 수 있습니다. 조건/검색 시간을 조정하세요.");
            }
            token.ThrowIfCancellationRequested();
            var text = output.GetAwaiter().GetResult();
            if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("생성 검색기를 실행하지 못했습니다: " + error.GetAwaiter().GetResult());
            var reply = JsonSerializer.Deserialize<GenerationReply>(text)
                ?? throw new InvalidOperationException("생성 검색기 응답이 없습니다.");
            if (reply.Error is not null) throw new ArgumentException(reply.Error);
            if (process.ExitCode != 0) throw new InvalidOperationException("생성 검색기가 정상 종료하지 않았습니다.");
            var type = typeof(PKM).Assembly.GetType("PKHeX.Core." + reply.EntityType);
            if (type is null || !typeof(PKM).IsAssignableFrom(type)) throw new InvalidOperationException("검색 결과의 개체 형식이 잘못됐습니다.");
            var pk = (PKM)Activator.CreateInstance(type, new Memory<byte>(Convert.FromBase64String(reply.Data)))!;
            if (!settings.Matches(pk)) throw new InvalidOperationException("생성 검색기의 결과가 요청한 조건과 다릅니다.");
            return (pk, reply.Method, reply.Seed);
        }
        finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); process.WaitForExit(); } }
    }
}
