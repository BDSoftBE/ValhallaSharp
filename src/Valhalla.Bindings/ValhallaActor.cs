using System.Text.Json;
using System.Text.Json.Nodes;

namespace Valhalla.Bindings;

/// <summary>An in-process Valhalla routing actor. Calls on one instance are serialized.</summary>
public sealed class ValhallaActor : IDisposable
{
    private readonly object gate = new();
    private readonly ActorHandle handle;
    private bool disposed;

    /// <param name="configurationJson">Full Valhalla configuration JSON, including mjolnir tile paths.</param>
    public ValhallaActor(string configurationJson)
    {
        ValidateJson(configurationJson);
        var status = NativeMethods.Create(configurationJson, out var actor, out var error);
        var message = NativeMethods.TakeString(error);
        if (status != 0) throw new ValhallaException(message);
        if (actor == 0) throw new ValhallaException("Native actor creation returned an empty handle.");
        handle = new ActorHandle(actor);
    }

    /// <summary>Loads a full configuration file. Relative tile paths are resolved against that file.</summary>
    public static ValhallaActor FromConfigurationFile(string path, string? tileExtractPath = null)
    {
        var fullPath = Path.GetFullPath(path);
        var config = JsonNode.Parse(File.ReadAllText(fullPath)) as JsonObject
            ?? throw new ArgumentException("Configuration must be a JSON object.", nameof(path));
        if (config["mjolnir"] is not JsonObject mjolnir)
            throw new ArgumentException("Configuration must include a mjolnir object.", nameof(path));
        var directory = Path.GetDirectoryName(fullPath)!;
        foreach (var key in new[] { "tile_dir", "tile_extract", "traffic_extract" })
        {
            if (mjolnir[key] is JsonValue value && value.TryGetValue<string>(out var configured) && !string.IsNullOrWhiteSpace(configured))
                mjolnir[key] = Path.GetFullPath(configured, directory);
        }
        if (tileExtractPath is not null)
            mjolnir["tile_extract"] = Path.GetFullPath(tileExtractPath);
        return new ValhallaActor(config.ToJsonString());
    }

    public string Route(string requestJson) => Execute(ValhallaAction.Route, requestJson);
    public string Locate(string requestJson) => Execute(ValhallaAction.Locate, requestJson);
    public string Matrix(string requestJson) => Execute(ValhallaAction.Matrix, requestJson);
    public string OptimizedRoute(string requestJson) => Execute(ValhallaAction.OptimizedRoute, requestJson);
    public string Isochrone(string requestJson) => Execute(ValhallaAction.Isochrone, requestJson);
    public string TraceRoute(string requestJson) => Execute(ValhallaAction.TraceRoute, requestJson);
    public string TraceAttributes(string requestJson) => Execute(ValhallaAction.TraceAttributes, requestJson);
    public string Status(string requestJson = "{}") => Execute(ValhallaAction.Status, requestJson);

    /// <summary>Executes a JSON request and returns JSON. Binary protobuf output is not supported.</summary>
    public string Execute(ValhallaAction action, string requestJson)
    {
        if (!Enum.IsDefined(action)) throw new ArgumentOutOfRangeException(nameof(action));
        ValidateJson(requestJson);
        using (var json = JsonDocument.Parse(requestJson))
        {
            if (json.RootElement.TryGetProperty("format", out var format) && format.GetString() is "pbf" or "protobuf")
                throw new ArgumentException("Only text responses are supported.", nameof(requestJson));
        }
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var status = NativeMethods.Request(handle, (int)action, requestJson, out var result, out var error);
            var response = NativeMethods.TakeString(result);
            var message = NativeMethods.TakeString(error);
            if (status != 0) throw new ValhallaException(message);
            return response;
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            handle.Dispose();
        }
    }

    private static void ValidateJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        if (json.Contains('\0')) throw new ArgumentException("JSON cannot contain a literal null character.", nameof(json));
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("JSON must be an object.", nameof(json));
    }
}

public enum ValhallaAction
{
    Route, Locate, Matrix, OptimizedRoute, Isochrone, TraceRoute, TraceAttributes, Status
}

public sealed class ValhallaException(string message) : Exception(message);
