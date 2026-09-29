import { FormEvent, useEffect, useState } from "react";
import { ArrowLeft, ArrowRight, Search, SlidersHorizontal } from "lucide-react";

const APP_VERSION = "1.0.0.0";

type Listing = { id: string; source: string; address: string; city: string; state: string; zip: string; price: number; bedrooms: number; bathrooms: number; sqft: number; status: string; listedDate: string; relevanceScore: number };
type SearchResponse = { items: Listing[]; page: number; pageSize: number; totalCount: number; totalPages: number };
type ApiErrorResponse = { errors?: Record<string, string[]> };

export function App() {
  const [filters, setFilters] = useState({ minPrice: "", maxPrice: "", minBedrooms: "", city: "", keyword: "", targetBudget: "" });
  const [results, setResults] = useState<SearchResponse | null>(null);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  async function search(nextPage = 1) {
    setLoading(true); setError("");
    const params = new URLSearchParams({ page: String(nextPage), pageSize: "5" });

    Object.entries(filters).forEach(([key, value]) => { if (value.trim()) params.set(key, value.trim()); });

    try {
      const response = await fetch(`/api/listings/search?${params}`);
      const responseText = await response.text();
      let body: SearchResponse | ApiErrorResponse;

      try {
        body = JSON.parse(responseText) as SearchResponse | ApiErrorResponse;
      } catch {
        throw new Error(responseText.trim() ? `Search failed (HTTP ${response.status}).` : "Search failed: the API returned an empty response.");
      }

      if (!response.ok) throw new Error(Object.values((body as ApiErrorResponse).errors ?? {}).flat().join(" ") || "Search failed.");
      setResults(body as SearchResponse); setPage(nextPage);
    } 
    catch (reason) { setError(reason instanceof Error ? reason.message : "Search failed."); }
    finally { setLoading(false); }
  }

  useEffect(() => { void search(); }, []);

  function submit(event: React.SubmitEvent<HTMLFormElement>) { event.preventDefault(); void search(1); }

  function update(key: string, value: string) { setFilters(current => ({ ...current, [key]: value })); }

  return <main>
    <header className="masthead"><div className="eyebrow">PROPERTY INTELLIGENCE / 2026 / VERSION {APP_VERSION}</div><h1>Find the place<br /><em>that fits.</em></h1><p>Search a live-feeling MLS sample across Northern Virginia. Tune the filters, then let budget fit and recency surface the strongest matches.</p></header>
    
    <section className="search-panel"><div className="section-label"><SlidersHorizontal size={16} /> SEARCH PARAMETERS</div>
    
    <form onSubmit={submit}>
      <label>City<input value={filters.city} onChange={event => update("city", event.target.value)} placeholder="Any city" /></label>
      <label>Keyword<input value={filters.keyword} onChange={event => update("keyword", event.target.value)} placeholder="e.g. transit, yard" /></label>
      <label>Min Bedrooms<input type="number" min="0" value={filters.minBedrooms} onChange={event => update("minBedrooms", event.target.value)} placeholder="Any" /></label>
      <label>Min Price<input type="number" min="0" value={filters.minPrice} onChange={event => update("minPrice", event.target.value)} placeholder="$ 0" /></label>
      <label>Max Price<input type="number" min="0" value={filters.maxPrice} onChange={event => update("maxPrice", event.target.value)} placeholder="$ 0" /></label>
      <label>Target Budget<input type="number" min="1" value={filters.targetBudget} onChange={event => update("targetBudget", event.target.value)} placeholder="$ 0" /></label>
      <button className="search-button" type="submit"><Search size={18} /> Search listings</button>
    </form></section>

    <section className="results"><div className="results-heading"><div><div className="section-label">CURATED RESULTS</div>
      <h2>{results?.totalCount ?? 0} properties found</h2>
      {results && results.totalPages > 0 && <div className="pagination"><button disabled={page <= 1 || loading} onClick={() => void search(page - 1)}><ArrowLeft size={16} /> Previous</button><span>{page} / {results.totalPages}</span><button disabled={page >= results.totalPages || loading} onClick={() => void search(page + 1)}>Next <ArrowRight size={16} /></button></div>}
      </div>{results && results.totalCount > 0 && <span className="page-note">Page {page} of {results.totalPages}</span>}</div>
      {loading && <div className="state">Searching the collection...</div>}
      {!loading && error && <div className="state error">{error}<button onClick={() => void search(page)}>Try again</button></div>}
      {!loading && !error && results?.items.length === 0 && <div className="state">No listings match those parameters. Try widening your search.</div>}
      {!loading && !error && results?.items.map(listing => <article className="listing" key={`${listing.source}-${listing.id}`}><div className="score"><strong>{listing.relevanceScore.toFixed(0)}</strong><span>FIT</span></div><div className="listing-main"><div className="listing-top"><h3>{listing.address}</h3><span className={`status ${listing.status}`}>{listing.status}</span></div><p>{listing.city}, {listing.state} {listing.zip}</p><div className="details"><strong>{listing.price.toLocaleString("en-US", { style: "currency", currency: "USD", maximumFractionDigits: 0 })}</strong><span>{listing.bedrooms} bd</span><span>{listing.bathrooms} ba</span><span>{listing.sqft.toLocaleString()} sq ft</span><span>Listed {listing.listedDate}</span></div></div></article>)}
    </section>
  </main>;
}