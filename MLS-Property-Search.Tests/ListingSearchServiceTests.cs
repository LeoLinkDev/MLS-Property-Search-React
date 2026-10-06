using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using MLS_Search.Models;
using MLS_Search.Services;
using Xunit;

namespace MLS_Search.Tests;

public sealed class ListingSearchServiceTests
{
    private static readonly IReadOnlyList<Listing> Listings =
    [
        Listing("A", "MLS_A", "1 Main St", "Springfield", 450000, 2, new DateOnly(2026, 9, 1), "Bright condo near transit."),
        Listing("B", "MLS_B", "2 Main St", "Springfield", 500000, 3, new DateOnly(2026, 8, 1), "Updated kitchen and yard."),
        Listing("C", "MLS_A", "3 Oak St", "Fairfax", 450000, 4, new DateOnly(2026, 9, 1), "Family home near schools.")
    ];

    [Fact]
    public void AppliesFiltersAndKeywordCaseInsensitively()
    {
        var response = Service().Search(new SearchQuery(MinPrice: 400000, MaxPrice: 475000, MinBedrooms: 2, City: " spring", Keyword: "CONDO"));

        Assert.Single(response.Items);
        Assert.Equal("A", response.Items[0].Id);
    }

    [Fact]
    public void ReturnsEmptyPageWhenThereAreNoMatches()
    {
        var response = Service().Search(new SearchQuery(City: "Nowhere"));

        Assert.Empty(response.Items);
        Assert.Equal(0, response.TotalCount);
        Assert.Equal(0, response.TotalPages);
    }

    [Fact]
    public void RanksTiedScoresWithStableKeys()
    {
        var response = Service().Search(new SearchQuery(TargetBudget: 450000));

        Assert.Equal(["A", "C", "B"], response.Items.Select(item => item.Id));
    }

    [Fact]
    public void PrioritizesPriceProximityOverRecencyForTargetBudget()
    {
        var listings = new[]
        {
            Listing("Nearer", "MLS_A", "1 Main St", "Springfield", 399000, 2, new DateOnly(2026, 8, 1), "Near target."),
            Listing("Farther", "MLS_B", "2 Main St", "Springfield", 470000, 2, new DateOnly(2026, 9, 2), "Far from target."),
            Listing("Closest", "MLS_C", "3 Main St", "Springfield", 399500, 2, new DateOnly(2026, 8, 1), "Closest to target.")
        };

        var response = new ListingSearchService(listings).Search(new SearchQuery(TargetBudget: 400000));

        Assert.Equal(["Closest", "Nearer", "Farther"], response.Items.Select(item => item.Id));
    }

    [Fact]
    public void PaginatesFirstAndPastEndBoundaries()
    {
        var first = Service().Search(new SearchQuery(Page: 1, PageSize: 2));
        var pastEnd = Service().Search(new SearchQuery(Page: 3, PageSize: 2));

        Assert.Equal(["A", "C"], first.Items.Select(item => item.Id));
        Assert.Empty(pastEnd.Items);
        Assert.Equal(2, pastEnd.TotalPages);
    }

    [Fact]
    public void DerivesHasParkingFromPositiveDescription()
    {
        var response = new ListingSearchService(
            [Listing("P", "MLS_P", "4 Park St", "Springfield", 475000, 2, new DateOnly(2026, 9, 2), "HAS PARKING included.")])
            .Search(new SearchQuery());

        Assert.True(response.Items.Single().HasParking);
    }

    [Fact]
    public void DerivesHasParkingAsFalseForExplicitNoParkingDescription()
    {
        var response = new ListingSearchService(
            [Listing("P", "MLS_P", "4 Park St", "Springfield", 475000, 2, new DateOnly(2026, 9, 2), "No parking available.")])
            .Search(new SearchQuery());

        Assert.False(response.Items.Single().HasParking);
    }

    [Fact]
    public void TreatsUnspecifiedParkingAsFalse()
    {
        var response = Service().Search(new SearchQuery());

        Assert.All(response.Items, item => Assert.False(item.HasParking));
    }

    [Fact]
    public void RejectsInvalidPriceAndPageSize()
    {
        var query = new QueryCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["minPrice"] = "500000",
            ["maxPrice"] = "400000",
            ["pageSize"] = "0"
        });

        var parsed = SearchQueryParser.Parse(query);

        Assert.Null(parsed.Value);
        Assert.Contains("price", parsed.Errors.Keys);
        Assert.Contains("pageSize", parsed.Errors.Keys);
    }

    private static ListingSearchService Service() => new(Listings);

    private static Listing Listing(string id, string source, string address, string city, decimal price, int bedrooms, DateOnly date, string description) =>
        new(id, source, address, city, "VA", "00000", price, bedrooms, 2, 1000, 0, 0, date, "active", description);
}