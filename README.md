---
codedBy: Leo Gurdian with assisted AI tools.
lastUpdated: 9/29/2026
project: Brillio's Coding Assestment
---

# MLS Listing Search

A small full-stack MLS property search application built for Brillio's .NET coding assessment. The backend is an ASP.NET Core Minimal API over the supplied `sample_listings.json`; the frontend is React with TypeScript and Vite.

**Application version:** 1.0.0.0

## Technologies Used

- **Backend:** C#, .NET 10, ASP.NET Core Minimal API, and OpenAPI
- **Frontend:** React, TypeScript, Vite, and Lucide React
- **Testing:** xUnit and the .NET test SDK
- **Data:** JSON sample listings with in-memory filtering and ranking

### Versions

- .NET 10 SDK
- `Microsoft.AspNetCore.OpenApi` 10.0.12
- React and React DOM 19.3.0
- TypeScript 7.0.2
- Vite 8.3.1 with `@vitejs/plugin-react` 6.1.1
- Lucide React 1.48.0
- `@types/react` and `@types/react-dom` 19.3.0
- `Microsoft.NET.Test.Sdk` 18.3.0
- xUnit 2.9.3 with `xunit.runner.visualstudio` 3.1.5

## Run it

The easiest option on Windows is to restart both services with one command from the repository root:

```powershell
.\start.ps1
```

This stops only processes listening on ports `7030` and `5173`, then opens the API and client in separate PowerShell windows. To stop both services without starting them again:

```powershell
.\start.ps1 -StopOnly
```

You can also run the launcher from the `MLS-Property-Search-UI` directory with `npm run start:all`.

### Manual startup

If you prefer separate terminals, start the API with the HTTPS launch profile:

```powershell
dotnet run --project .\MLS-Property-Search-API\MLS-Property-Search.csproj --launch-profile https
```

In a second terminal:

```powershell
Set-Location .\MLS-Property-Search-UI
npm ci
npm run dev
```

Open `http://localhost:5173`. Vite runs over HTTP and proxies `/api` requests to the HTTPS API at `https://localhost:7030`.

Check that the API is running with:

```powershell
Invoke-RestMethod 'https://localhost:7030/api/health' -SkipCertificateCheck
```

If startup reports that port `7030` or `5173` is already in use, an earlier service instance is still running. Use the launcher to stop those listeners and restart both services:

```powershell
.\start.ps1
```

Or stop them without restarting:

```powershell
.\start.ps1 -StopOnly
```

## API

### Health check

`GET /api/health` returns `{ "status": "healthy" }` when the API is available.

PowerShell:

```powershell
Invoke-RestMethod 'https://localhost:7030/api/health' -SkipCertificateCheck
```

Expected response:

```text
status
------
healthy
```

### Search

`GET /api/listings/search` accepts `minPrice`, `maxPrice`, `minBedrooms`, `city`, `keyword`, `targetBudget`, `page`, and `pageSize`.

Example:

```text
https://localhost:7030/api/listings/search?city=Springfield&targetBudget=450000&page=1&pageSize=5
```

The response includes the current page, page size, total count, total pages, and ranked items. Invalid values return HTTP 400 with a validation-problem response. A valid query with no matches returns an empty item list and HTTP 200.

Each item also includes a derived `hasParking` flag. The API reads the listing description and sets this flag to `true` when it contains `has parking` or `with parking`, case-insensitively. An explicit `no parking` description takes precedence and sets it to `false`; descriptions without a recognized parking phrase also return `false`.

For example, a listing description containing `Has parking available.` returns:

```json
{
  "id": "example",
  "hasParking": true
}
```

## Ranking approach

Filtering is applied first. Each remaining listing receives a score from 0 to 100:

- Up to 60 points for price proximity to `targetBudget`. An exact match receives 60, with a linear reduction based on relative difference. If no target budget is supplied, this component contributes 0.
- Up to 40 points for recency. The newest listing in the loaded dataset receives 40 and the oldest receives 0.

When `targetBudget` is provided, listings are ordered by:

1. Closest absolute price to the target budget
2. Relevance score
3. Newest listing date
4. Stable source and ID tie-breakers

Without a target budget, results sort by relevance score followed by the newest listing date and the same stable source and ID tie-breakers. The score is intentionally a transparent ranking aid, not a valuation model.

## Tests

```powershell
dotnet test .\MLS-Property-Search.Tests\MLS-Search.Tests.csproj
Set-Location .\MLS-Property-Search-UI
npm run build
```

The service tests cover case-insensitive filters, no matches, tied scores, pagination boundaries, invalid values, and parking derivation for positive, negative, and unspecified descriptions.

## Authorization and production considerations

No end-user authorization is enabled in this demo because the app is read-only, uses sample data, and has no user or tenant boundary to protect. For production, use an OIDC/OAuth2 provider with short-lived bearer tokens and a scope such as `listings:read`; enforce tenant/source access on the server rather than trusting client roles.

Other appropriate production controls include restrictive CORS, HTTPS, rate limiting, structured logs that exclude sensitive data, health checks, request limits, configuration validation, and a persistent repository with indexes for searchable fields. The in-memory collection is deliberate for the assessment and is not intended for concurrent production ingestion.