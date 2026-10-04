using System.Text.Json;

namespace Valhalla.Bindings;

/// <summary>Calculates typed routes using an in-process Valhalla actor.</summary>
public sealed class ValhallaRoutingEngine : IDisposable
{
    private readonly ValhallaActor actor;
    private readonly bool ownsActor;
    private readonly object gate = new();
    private bool disposed;

    /// <summary>Wraps an existing actor. By default the caller retains ownership.</summary>
    public ValhallaRoutingEngine(ValhallaActor actor, bool leaveOpen = true)
    {
        ArgumentNullException.ThrowIfNull(actor);
        this.actor = actor;
        ownsActor = !leaveOpen;
    }

    /// <summary>Creates and owns an actor from full Valhalla configuration JSON.</summary>
    public ValhallaRoutingEngine(string configurationJson)
        : this(new ValhallaActor(configurationJson), leaveOpen: false) { }

    /// <summary>Creates and owns an actor, resolving tile paths relative to the configuration file.</summary>
    public static ValhallaRoutingEngine FromConfigurationFile(string path, string? tileExtractPath = null)
        => new(ValhallaActor.FromConfigurationFile(path, tileExtractPath), leaveOpen: false);

    /// <summary>Calculates a route between two positions. Defaults to driving, kilometers, and English.</summary>
    /// <remarks>Times are seconds; lengths use the returned trip units. Leg shapes are encoded polyline6.</remarks>
    public RouteResponse CalculateRoute(GeoCoordinates origin, GeoCoordinates destination, RouteOptions? options = null)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            ValidateCoordinates(origin, nameof(origin));
            ValidateCoordinates(destination, nameof(destination));
            options ??= new RouteOptions();
            ArgumentException.ThrowIfNullOrWhiteSpace(options.Costing);
            ArgumentException.ThrowIfNullOrWhiteSpace(options.Language);
            if (options.Units is not ("kilometers" or "miles"))
                throw new ArgumentException("Units must be kilometers or miles.", nameof(options));

            var request = JsonSerializer.Serialize(new
            {
                locations = new[]
                {
                    new { lat = origin.Latitude, lon = origin.Longitude, type = "break" },
                    new { lat = destination.Latitude, lon = destination.Longitude, type = "break" }
                },
                costing = options.Costing,
                units = options.Units,
                language = options.Language,
                format = "json",
                shape_format = "polyline6",
                directions_type = "instructions",
                turn_lanes = true
            });
            return RouteResponse.FromJson(actor.Route(request));
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            if (ownsActor) actor.Dispose();
        }
    }

    private static void ValidateCoordinates(GeoCoordinates coordinates, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(coordinates, parameterName);
        if (!double.IsFinite(coordinates.Latitude) || coordinates.Latitude is < -90 or > 90)
            throw new ArgumentOutOfRangeException(parameterName, "Latitude must be finite and between -90 and 90 degrees.");
        if (!double.IsFinite(coordinates.Longitude) || coordinates.Longitude is < -180 or > 180)
            throw new ArgumentOutOfRangeException(parameterName, "Longitude must be finite and between -180 and 180 degrees.");
    }
}

public sealed class RouteOptions
{
    /// <summary>Valhalla costing name, for example auto, bicycle, pedestrian, or truck.</summary>
    public string Costing { get; init; } = "auto";
    public string Units { get; init; } = "kilometers";
    public string Language { get; init; } = "en-US";
}
