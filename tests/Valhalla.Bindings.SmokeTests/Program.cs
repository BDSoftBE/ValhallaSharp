using System.Text.Json;
using Valhalla.Bindings;

Expect<ArgumentException>(() => new ValhallaActor(" "));
Expect<ArgumentException>(() => new ValhallaActor("[]"));
Expect<JsonException>(() => new ValhallaActor("{invalid}"));
Expect<ArgumentException>(() => new ValhallaActor("{\"value\":\"\0\"}"));
Console.WriteLine("Managed input validation passed.");

var routeJson = """
    {
      "id": "test", "future_field": { "enabled": true },
      "trip": {
        "status": 0, "status_message": "Found route between points",
        "units": "kilometers", "language": "en-US",
        "locations": [{ "lat": 50.85, "lon": 4.35, "original_index": 0 }],
        "summary": { "time": 120.5, "length": 1.25, "cost": 110, "has_toll": true, "time_truck": 150 },
        "legs": [{
          "summary": { "time": 120.5, "length": 1.25 }, "shape": "encoded-polyline",
          "maneuvers": [{
            "type": 10, "instruction": "Turn right", "begin_shape_index": 2, "end_shape_index": 5,
            "verbal_pre_transition_instruction": "Turn right onto Main Street",
            "sign": { "exit_number_elements": [{ "text": "12", "consecutive_count": 1 }] },
            "lanes": [{ "directions": 8, "active": 8 }],
            "transit_info": { "short_name": "10", "transit_stops": [{ "name": "Central", "lat": 50.85, "lon": 4.35 }] },
            "future_maneuver": [1, 2]
          }]
        }],
        "warnings": [{ "code": 201, "text": "Test warning" }]
      },
      "alternates": [{ "trip": { "summary": { "time": 140 }, "legs": [{ "summary": { "time": 140 } }] } }]
    }
    """;
var typedRoute = RouteResponse.FromJson(routeJson);
var maneuver = typedRoute.Trip.Legs[0].Maneuvers[0];
if (typedRoute.Trip.Summary.Time != 120.5 || typedRoute.Trip.Summary.Length != 1.25 ||
    !typedRoute.Trip.Summary.HasToll || typedRoute.Trip.Locations[0].Lat != 50.85 ||
    maneuver.BeginShapeIndex != 2 || maneuver.EndShapeIndex != 5 ||
    maneuver.Sign!.ExitNumberElements[0].Text != "12" || maneuver.Lanes[0].Active != 8 ||
    maneuver.TransitInfo!.TransitStops[0].Name != "Central" ||
    typedRoute.Trip.Warnings[0].Text != "Test warning" || typedRoute.Alternates[0].Trip.Summary.Time != 140 ||
    typedRoute.AdditionalData["future_field"].GetProperty("enabled").GetBoolean() != true ||
    typedRoute.Trip.Summary.AdditionalData["time_truck"].GetDouble() != 150 ||
    maneuver.AdditionalData["future_maneuver"].GetArrayLength() != 2 || typedRoute.RawJson != routeJson)
    throw new Exception("Typed route response lost or incorrectly mapped fields.");
Expect<JsonException>(() => RouteResponse.FromJson("{}"));
Expect<JsonException>(() => RouteResponse.FromJson("{\"trip\":null}"));
Expect<JsonException>(() => RouteResponse.FromJson("{\"trip\":{\"summary\":{},\"legs\":[]}}"));
Expect<ArgumentNullException>(() => new ValhallaRoutingEngine((ValhallaActor)null!));
if (new GeoCoordinates(50.85, 4.35) != new GeoCoordinates(50.85, 4.35))
    throw new Exception("Coordinates must have record value equality.");
Console.WriteLine("Typed route deserialization and field preservation passed.");

if (args.Length == 1 && args[0] == "--native")
{
    for (var i = 0; i < 10; i++)
        Expect<ValhallaException>(() => new ValhallaActor("{}"));
    Console.WriteLine("Native DLL loading and configuration error propagation passed.");
}
else if (args.Length == 2)
{
    using var actor = ValhallaActor.FromConfigurationFile(args[0]);
    using var engine = new ValhallaRoutingEngine(actor);
    using var request = JsonDocument.Parse(File.ReadAllText(args[1]));
    var locations = request.RootElement.GetProperty("locations");
    var origin = new GeoCoordinates(locations[0].GetProperty("lat").GetDouble(), locations[0].GetProperty("lon").GetDouble());
    var destination = new GeoCoordinates(locations[1].GetProperty("lat").GetDouble(), locations[1].GetProperty("lon").GetDouble());
    Expect<ArgumentOutOfRangeException>(() => engine.CalculateRoute(new GeoCoordinates(double.NaN, 0), destination));
    Expect<ArgumentOutOfRangeException>(() => engine.CalculateRoute(origin, new GeoCoordinates(0, 181)));
    Expect<ArgumentNullException>(() => engine.CalculateRoute(null!, destination));
    Expect<ArgumentException>(() => engine.CalculateRoute(origin, destination, new RouteOptions { Units = "invalid" }));
    var route = engine.CalculateRoute(origin, destination);
    if (route.Trip.Legs.Count == 0 || string.IsNullOrEmpty(route.Trip.Legs[0].Shape))
        throw new Exception("Typed route did not contain geometry.");
    engine.Dispose();
    Expect<ObjectDisposedException>(() => engine.CalculateRoute(origin, destination));
    // Disposing a borrowed wrapper must leave its actor usable.
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
