using Microsoft.AspNetCore.Http;
using MLS_Search.Models;

namespace MLS_Search.Services;

public sealed record SearchQuery(
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    int? MinBedrooms = null,
    string? City = null,
    string? Keyword = null,
    decimal? TargetBudget = null,
    int Page = 1,
    int PageSize = 5);

public sealed record ListingResult(
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
    string Status,
    DateOnly ListedDate,
    bool HasParking,
    double RelevanceScore);

public sealed record SearchResponse(
    IReadOnlyList<ListingResult> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public interface IListingSearchService
{
    SearchResponse Search(SearchQuery query);
}

public sealed class ListingSearchService : IListingSearchService
{
    private readonly IReadOnlyList<Listing> listings;
    private readonly DateOnly newestDate;
    private readonly DateOnly oldestDate;

    public ListingSearchService(IReadOnlyList<Listing> listings)
    {
        this.listings = listings;
        newestDate = listings.Count == 0 ? DateOnly.MinValue : listings.Max(listing => listing.ListedDate);
        oldestDate = listings.Count == 0 ? DateOnly.MinValue : listings.Min(listing => listing.ListedDate);
    }

    public SearchResponse Search(SearchQuery query)
    {
        var matches = listings
            .Where(listing => !query.MinPrice.HasValue || listing.Price >= query.MinPrice)
            .Where(listing => !query.MaxPrice.HasValue || listing.Price <= query.MaxPrice)
            .Where(listing => !query.MinBedrooms.HasValue || listing.Bedrooms >= query.MinBedrooms)
            .Where(listing => string.IsNullOrWhiteSpace(query.City) || listing.City.Contains(query.City.Trim(), StringComparison.OrdinalIgnoreCase))
            .Where(listing => string.IsNullOrWhiteSpace(query.Keyword) || listing.Description.Contains(query.Keyword.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(listing => new ListingResult(
                listing.Id,
                listing.Source,
                listing.Address,
                listing.City,
                listing.State,
                listing.Zip,
                listing.Price,
                listing.Bedrooms,
                listing.Bathrooms,
                listing.Sqft,
                listing.Status,
                listing.ListedDate,
                HasParking(listing.Description),
                CalculateScore(listing, query.TargetBudget)))
            .OrderBy(result => query.TargetBudget.HasValue
                ? Math.Abs(result.Price - query.TargetBudget.Value)
                : decimal.MaxValue)
            .ThenByDescending(result => result.RelevanceScore)
            .ThenByDescending(result => result.ListedDate)
            .ThenBy(result => result.Source, StringComparer.Ordinal)
            .ThenBy(result => result.Id, StringComparer.Ordinal)
            .ToList();

        var totalPages = matches.Count == 0 ? 0 : (int)Math.Ceiling(matches.Count / (double)query.PageSize);
        var pageItems = matches.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToList();
        return new SearchResponse(pageItems, query.Page, query.PageSize, matches.Count, totalPages);
    }

    private static bool HasParking(string description)
    {
        return !description.Contains("no parking", StringComparison.OrdinalIgnoreCase)
            && (description.Contains("has parking", StringComparison.OrdinalIgnoreCase)
                || description.Contains("with parking", StringComparison.OrdinalIgnoreCase));
    }

    private double CalculateScore(Listing listing, decimal? targetBudget)
    {
        var budgetScore = targetBudget.HasValue
            ? Math.Max(0, 60 * (1 - (double)Math.Abs(listing.Price - targetBudget.Value) / (double)targetBudget.Value))
            : 0;

        var recencyScore = newestDate == oldestDate
            ? 40
            : 40 * (listing.ListedDate.DayNumber - oldestDate.DayNumber) / (double)(newestDate.DayNumber - oldestDate.DayNumber);

        return Math.Round(budgetScore + recencyScore, 2);
    }
}

public sealed record ParsedSearchQuery(SearchQuery? Value, Dictionary<string, string[]> Errors)
{
    public static ParsedSearchQuery Success(SearchQuery query) => new(query, new());
}

public static class SearchQueryParser
{
    public static ParsedSearchQuery Parse(IQueryCollection query)
    {
        var errors = new Dictionary<string, string[]>();
        var minPrice = ParseDecimal(query, "minPrice", errors);
        var maxPrice = ParseDecimal(query, "maxPrice", errors);
        var targetBudget = ParseDecimal(query, "targetBudget", errors);
        var minBedrooms = ParseInt(query, "minBedrooms", errors);
        var page = ParseInt(query, "page", errors) ?? 1;
        var pageSize = ParseInt(query, "pageSize", errors) ?? 5;

        if (minPrice < 0) AddError(errors, "minPrice", "minPrice must be zero or greater.");
        if (maxPrice < 0) AddError(errors, "maxPrice", "maxPrice must be zero or greater.");
        if (targetBudget <= 0) AddError(errors, "targetBudget", "targetBudget must be greater than zero.");
        if (minBedrooms < 0) AddError(errors, "minBedrooms", "minBedrooms must be zero or greater.");
        if (minPrice.HasValue && maxPrice.HasValue && minPrice > maxPrice) AddError(errors, "price", "minPrice cannot be greater than maxPrice.");
        if (page < 1) AddError(errors, "page", "page must be at least 1.");
        if (pageSize < 1 || pageSize > 100) AddError(errors, "pageSize", "pageSize must be between 1 and 100.");

        return errors.Count > 0
            ? new ParsedSearchQuery(null, errors)
            : ParsedSearchQuery.Success(new SearchQuery(minPrice, maxPrice, minBedrooms, query["city"], query["keyword"], targetBudget, page, pageSize));
    }

    private static decimal? ParseDecimal(IQueryCollection query, string key, Dictionary<string, string[]> errors)
    {
        if (!query.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value)) return null;
        return decimal.TryParse(value, out var result) ? result : AddParseError<decimal>(errors, key, "must be a valid number.");
    }

    private static int? ParseInt(IQueryCollection query, string key, Dictionary<string, string[]> errors)
    {
        if (!query.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value)) return null;
        return int.TryParse(value, out var result) ? result : AddParseError<int>(errors, key, "must be a valid integer.");
    }

    private static T? AddParseError<T>(Dictionary<string, string[]> errors, string key, string message) where T : struct
    {
        AddError(errors, key, message);
        return null;
    }

    private static void AddError(Dictionary<string, string[]> errors, string key, string message) => errors[key] = errors.TryGetValue(key, out var existing) ? existing.Append(message).ToArray() : [message];
}