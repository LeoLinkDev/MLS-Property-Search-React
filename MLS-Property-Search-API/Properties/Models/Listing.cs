namespace MLS_Search.Models;

public sealed record Listing(
    string Id,
    string Source,
    string Address,
    string City,
    string State,
    string Zip,
    decimal Price,
    int Bedrooms,
    decimal Bathrooms,
    int Sqft,
    double Latitude,
    double Longitude,
    DateOnly ListedDate,
    string Status,
    string Description);