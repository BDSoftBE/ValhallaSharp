using System.Text.Json;
using System.Text.Json.Serialization;

namespace Valhalla.Bindings;

/// <summary>Preserves additional response fields, including dynamic recosting values.</summary>
public abstract class RouteData
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; init; } = [];
}

/// <summary>A complete native Valhalla JSON route response.</summary>
public sealed class RouteResponse : RouteData
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public required RouteTrip Trip { get; init; }
    public List<RouteResponse> Alternates { get; init; } = [];
    public string? Id { get; init; }

    /// <summary>The exact native response, including fields from later Valhalla versions.</summary>
    [JsonIgnore]
    public string RawJson { get; private set; } = string.Empty;

    public static RouteResponse FromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        var response = JsonSerializer.Deserialize<RouteResponse>(json, SerializerOptions)
            ?? throw new JsonException("Route response must be an object.");
        ValidateTrip(response);
        foreach (var alternate in response.Alternates)
            ValidateTrip(alternate);
        response.RawJson = json;
        return response;
    }

    private static void ValidateTrip(RouteResponse response)
    {
        if (response is null || response.Trip is null || response.Trip.Summary is null ||
            response.Trip.Legs is null || response.Trip.Legs.Count == 0 ||
            response.Trip.Legs.Any(leg => leg is null || leg.Summary is null))
            throw new JsonException("Route response must contain a trip, summary, and legs with summaries.");
    }
}

public sealed class RouteTrip : RouteData
{
    public required RouteSummary Summary { get; init; }
    public List<RouteLocation> Locations { get; init; } = [];
    public List<RouteLeg> Legs { get; init; } = [];
    public int Status { get; init; }
    public string? StatusMessage { get; init; }
    public string? Units { get; init; }
    public string? Language { get; init; }
    public List<string> LinearReferences { get; init; } = [];
    public List<RouteWarning> Warnings { get; init; } = [];
}

public sealed class RouteSummary : RouteData
{
    /// <summary>Travel time in seconds.</summary>
    public double Time { get; init; }
    /// <summary>Distance in the returned trip units.</summary>
    public double Length { get; init; }
    public double Cost { get; init; }
    public double MinLat { get; init; }
    public double MinLon { get; init; }
    public double MaxLat { get; init; }
    public double MaxLon { get; init; }
    public bool HasTimeRestrictions { get; init; }
    public bool HasToll { get; init; }
    public bool HasHighway { get; init; }
    public bool HasFerry { get; init; }
    public List<RouteAdmin> Admins { get; init; } = [];
    public List<RouteAdminCrossing> AdminCrossings { get; init; } = [];
    public List<List<double>> LevelChanges { get; init; } = [];
}

public sealed class RouteLocation : RouteData
{
    public double Lat { get; init; }
    public double Lon { get; init; }
    public string? Type { get; init; }
    public string? Name { get; init; }
    public string? Street { get; init; }
    public uint? Heading { get; init; }
    public string? DateTime { get; init; }
    public string? TimeZoneOffset { get; init; }
    public string? TimeZoneName { get; init; }
    public double? Waiting { get; init; }
    public string? SideOfStreet { get; init; }
    public uint OriginalIndex { get; init; }
}

public sealed class RouteLeg : RouteData
{
    public required RouteSummary Summary { get; init; }
    public List<RouteManeuver> Maneuvers { get; init; } = [];
    /// <summary>Encoded polyline6 geometry.</summary>
    public string? Shape { get; init; }
    public double? ElevationInterval { get; init; }
    public List<double> Elevation { get; init; } = [];
}

public sealed class RouteManeuver : RouteData
{
    public int Type { get; init; }
    public string? Instruction { get; init; }
    public string? VerbalTransitionAlertInstruction { get; init; }
    public string? VerbalSuccinctTransitionInstruction { get; init; }
    public string? VerbalPreTransitionInstruction { get; init; }
    public string? VerbalPostTransitionInstruction { get; init; }
    public List<string> StreetNames { get; init; } = [];
    public List<string> BeginStreetNames { get; init; } = [];
    public uint? BearingBefore { get; init; }
    public uint? BearingAfter { get; init; }
    /// <summary>Travel time in seconds.</summary>
    public double Time { get; init; }
    /// <summary>Distance in the returned trip units.</summary>
    public double Length { get; init; }
    public double Cost { get; init; }
    public uint BeginShapeIndex { get; init; }
    public uint EndShapeIndex { get; init; }
    public bool Toll { get; init; }
    public bool Highway { get; init; }
    public bool Ferry { get; init; }
    public bool Rough { get; init; }
    public bool HasTimeRestrictions { get; init; }
    public RouteSign? Sign { get; init; }
    public uint? RoundaboutExitCount { get; init; }
    public string? DepartInstruction { get; init; }
    public string? VerbalDepartInstruction { get; init; }
    public string? ArriveInstruction { get; init; }
    public string? VerbalArriveInstruction { get; init; }
    public RouteTransitInfo? TransitInfo { get; init; }
    public bool VerbalMultiCue { get; init; }
    public string? TravelMode { get; init; }
    public string? TravelType { get; init; }
    public List<RouteLane> Lanes { get; init; } = [];
}

public sealed class RouteSign : RouteData
{
    public List<RouteSignElement> ExitNumberElements { get; init; } = [];
    public List<RouteSignElement> ExitBranchElements { get; init; } = [];
    public List<RouteSignElement> ExitTowardElements { get; init; } = [];
    public List<RouteSignElement> ExitNameElements { get; init; } = [];
}

public sealed class RouteSignElement : RouteData
{
    public string? Text { get; init; }
    public uint ConsecutiveCount { get; init; }
}

public sealed class RouteLane : RouteData
{
    public uint Directions { get; init; }
    public uint? Active { get; init; }
    public uint? Valid { get; init; }
}

public sealed class RouteTransitInfo : RouteData
{
    public string? OnestopId { get; init; }
    public string? ShortName { get; init; }
    public string? LongName { get; init; }
    public string? Headsign { get; init; }
    public uint Color { get; init; }
    public uint TextColor { get; init; }
    public string? Description { get; init; }
    public string? OperatorOnestopId { get; init; }
    public string? OperatorName { get; init; }
    public string? OperatorUrl { get; init; }
    public List<RouteTransitStop> TransitStops { get; init; } = [];
}

public sealed class RouteTransitStop : RouteData
{
    public string? Type { get; init; }
    public string? OnestopId { get; init; }
    public string? Name { get; init; }
    public string? ArrivalDateTime { get; init; }
    public string? DepartureDateTime { get; init; }
    public bool AssumedSchedule { get; init; }
    public double? Lat { get; init; }
    public double? Lon { get; init; }
}

public sealed class RouteAdmin : RouteData
{
    public string? CountryCode { get; init; }
    public string? CountryText { get; init; }
    public string? StateCode { get; init; }
    public string? StateText { get; init; }
}

public sealed class RouteAdminCrossing : RouteData
{
    public uint FromAdminIndex { get; init; }
    public uint ToAdminIndex { get; init; }
    public uint BeginShapeIndex { get; init; }
    public uint EndShapeIndex { get; init; }
}

public sealed class RouteWarning : RouteData
{
    public int Code { get; init; }
    public string? Text { get; init; }
}

