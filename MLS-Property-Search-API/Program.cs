using System.Text.Json;
using MLS_Search.Models;
using MLS_Search.Services;

var DOMAIN  = "localhost";
var PORT = 5173;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddCors(options => options.AddPolicy("AllowGetOnly", policy =>
    policy.WithOrigins($"http://{DOMAIN}:{PORT}").WithMethods("GET", "POST"))); // Permits HTTP GET requests

var listingsPath = Path.Combine(AppContext.BaseDirectory, "sample_listings.json");
var listings = await LoadListingsAsync(listingsPath);
builder.Services.AddSingleton<IListingSearchService>(new ListingSearchService(listings));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowGetOnly");

app.MapGet("/api/health", () => Results.Ok(new { status = "healthy" }));

app.MapGet("/api/listings/search", (HttpRequest request, IListingSearchService service) =>
{
    var query = SearchQueryParser.Parse(request.Query);
    if (query.Errors.Count > 0)
    {
        return Results.ValidationProblem(query.Errors);
    }

    return Results.Ok(service.Search(query.Value!));
})
.WithName("SearchListings");

app.Run();

static async Task<IReadOnlyList<Listing>> LoadListingsAsync(string path)
{
    await using var stream = File.OpenRead(path);
    return await JsonSerializer.DeserializeAsync<List<Listing>>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidOperationException("The listing dataset is empty or invalid.");
}
