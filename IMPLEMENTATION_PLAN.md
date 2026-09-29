---
codedBy: Leo Gurdian with assisted AI tools.
lastUpdated: 9/29/2026
project: Brillio's Coding Assestment
---

# MLS Listing Search Implementation Plan

## Goal

Create a ASP.NET Core full-stack listing search application. The backend will expose a validated, paginated search endpoint over the supplied `sample_listings.json` file, and a React + TypeScript client will provide the complete search workflow.

## Backend design

- Use ASP.NET Core Minimal APIs on .NET 10.
- Model listings with a strongly typed `Listing` record and load the JSON once at startup into an in-memory `IReadOnlyList<Listing>`.
- Keep search behavior in a testable `ListingSearchService`, separate from HTTP concerns.
- Expose `GET /api/listings/search` with `minPrice`, `maxPrice`, `minBedrooms`, `city`, `keyword`, `targetBudget`, `page`, and `pageSize` query parameters.
- Return a stable response containing `items`, `page`, `pageSize`, `totalCount`, and `totalPages`.
- Return HTTP 400 with a structured problem response for invalid values, including negative numbers, `minPrice > maxPrice`, `page < 1`, and `pageSize < 1`.
- Return HTTP 200 with an empty `items` array for valid filters that have no matches; the UI will present a clear no-results state.
- Normalize city and keyword comparisons by trimming and using case-insensitive matching. Keyword matching will search `description` only, as required.

## Relevance scoring

Each filtered listing receives a transparent score from 0 to 100:

- Budget fit: up to 60 points. A listing at the target budget receives 60; the score decreases linearly with the relative price difference. When no target budget is provided, this component is 0.
- Recency: up to 40 points. The newest listing in the loaded dataset receives 40 and the oldest receives 0, using UTC date-only values.
- Results are ordered by descending score, then newest listed date, then stable `source` and `id` keys. This makes tied scores deterministic and easy to test.

The score is a ranking aid rather than a claim of market value. Filtering happens before scoring, and pagination happens after ordering so page boundaries are consistent.

## Frontend design

- Use a Vite React + TypeScript app in `frontend/`.
- Provide inputs for every search parameter, including target budget and pagination controls.
- Call the API through a Vite development proxy and render address, city/state, price, bedrooms, status, listed date, and relevance score.
- Include loading, empty, validation-error, API-error, and retry states.
- Reset to page 1 when filters change and preserve the current query in component state.
- Keep the UI responsive and deliberately simple so the API behavior remains easy to inspect during an interview.

## Tests

Create a focused xUnit project covering the search service for:

- all supported filters and case-insensitive text matching;
- no matches;
- invalid filter combinations and pagination values;
- deterministic ordering when scores tie;
- first, middle, and past-the-end pagination boundaries.

## Authorization and operational best practices

For this read-only, in-memory assessment app, I recommend **no end-user authorization in the demo build**. Adding a login flow would add identity plumbing without protecting a meaningful mutation or tenant boundary. The API should still be designed so authorization can be added at the endpoint boundary later.

For a production version, use an OIDC/OAuth2 identity provider with short-lived bearer tokens, require a read/search scope such as `listings:read`, enforce tenant/source authorization server-side, and never trust client-supplied roles. Also add HTTPS, restrictive CORS, rate limiting, structured logging with sensitive data excluded, request-size/time limits, health checks, configuration validation, and a real repository/database with indexes for the searchable fields.

## Validation checklist

1. Run backend unit tests and `dotnet build`.
2. Run `npm install` and `npm run build` in `frontend/`.
3. Start the API and frontend locally and verify a real search request, an empty result, invalid input, and pagination from the browser.
4. Document the commands and scoring trade-offs in `README.md`.