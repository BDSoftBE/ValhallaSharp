namespace Valhalla.Bindings;

/// <summary>A WGS84 position in decimal degrees, in latitude/longitude order.</summary>
public sealed record GeoCoordinates(double Latitude, double Longitude);
