using System.Text.Json;
using UltimateEditor;

try
{
    var request = JsonSerializer.Deserialize<GenerationRequest>(Console.ReadLine() ?? "")
        ?? throw new ArgumentException("요청이 없습니다.");
    Console.WriteLine(JsonSerializer.Serialize(GenerationEngine.Generate(request)));
}
catch (Exception ex) { Console.WriteLine(JsonSerializer.Serialize(new GenerationReply(Error: ex.Message))); }
