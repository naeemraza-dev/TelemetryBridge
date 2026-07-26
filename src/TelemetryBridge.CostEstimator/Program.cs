using System.Text.Json;
using TelemetryBridge.Core;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: dotnet run --project src/TelemetryBridge.CostEstimator -- <inputs.json>");
    return 2;
}

await using var stream = File.OpenRead(args[0]);
var inputs = await JsonSerializer.DeserializeAsync<TelemetryCostInputs>(
    stream,
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
if (inputs is null)
{
    Console.Error.WriteLine("The input document is empty or invalid.");
    return 2;
}

var estimate = TelemetryCostEstimator.Estimate(inputs);
Console.WriteLine(JsonSerializer.Serialize(estimate, new JsonSerializerOptions { WriteIndented = true }));
return 0;
