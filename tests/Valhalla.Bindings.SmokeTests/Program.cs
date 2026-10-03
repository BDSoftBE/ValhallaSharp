using System.Text.Json;
using Valhalla.Bindings;

Expect<ArgumentException>(() => new ValhallaActor(" "));
Expect<ArgumentException>(() => new ValhallaActor("[]"));
Expect<JsonException>(() => new ValhallaActor("{invalid}"));
Expect<ArgumentException>(() => new ValhallaActor("{\"value\":\"\0\"}"));
Console.WriteLine("Managed input validation passed.");

if (args.Length == 1 && args[0] == "--native")
{
    for (var i = 0; i < 10; i++)
        Expect<ValhallaException>(() => new ValhallaActor("{}"));
    Console.WriteLine("Native DLL loading and configuration error propagation passed.");
}
else if (args.Length == 2)
{
    using var actor = ValhallaActor.FromConfigurationFile(args[0]);
    using var result = JsonDocument.Parse(actor.Route(File.ReadAllText(args[1])));
    if (!result.RootElement.TryGetProperty("trip", out var trip) ||
        !trip.TryGetProperty("legs", out var legs) || legs.GetArrayLength() == 0)
        throw new Exception("Route response did not contain trip legs.");
    Expect<ValhallaException>(() => actor.Route("{\"locations\":[],\"costing\":\"auto\"}"));
    // A failed request must leave the actor usable for the next route.
    using var retry = JsonDocument.Parse(actor.Route(File.ReadAllText(args[1])));
    actor.Dispose();
    Expect<ObjectDisposedException>(() => actor.Status());
    Console.WriteLine("Native routing, error recovery, and disposal passed.");
}
else if (args.Length != 0)
    throw new ArgumentException("Provide configuration.json and route-request.json, --native, or no arguments.");
else
    Console.WriteLine("Native routing checks skipped: supply a configuration and route request to run them.");

static void Expect<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new Exception($"Expected {typeof(T).Name}.");
}
