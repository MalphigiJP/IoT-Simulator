import { useMemo, useState } from "react";
import "./SensorList.css";

const PAGE_SIZE = 20;

/**
 * Reusable sensor registry list. The parent Dashboard owns the API requests
 * and passes sensors plus edit/delete callbacks.
 */
export default function SensorList({
  sensors = [],
  onEdit,
  onDelete,
  deletingId = null,
}) {
  const [query, setQuery] = useState("");
  const [category, setCategory] = useState("All");
  const [page, setPage] = useState(1);

  const categories = useMemo(
    () => [...new Set(sensors.map((s) => s.category).filter(Boolean))].sort(),
    [sensors]
  );

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    return sensors.filter((sensor) => {
      const matchesText = `${sensor.id} ${sensor.macAddress ?? ""} ${sensor.location ?? ""} ${sensor.category ?? ""}`
        .toLowerCase()
        .includes(q);
      const matchesCategory = category === "All" || sensor.category === category;
      return matchesText && matchesCategory;
    });
  }, [sensors, query, category]);

  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));
  const currentPage = Math.min(page, totalPages);
  const visible = filtered.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE);

  function updateQuery(value) {
    setQuery(value);
    setPage(1);
  }

  function updateCategory(value) {
    setCategory(value);
    setPage(1);
  }

  return (
    <section className="sx-registry-list" aria-labelledby="sensor-list-title">
      <div className="sx-registry-list-heading">
        <div>
          <h2 id="sensor-list-title">Registered sensors</h2>
          <p>Find and manage devices across your Smart-X deployments.</p>
        </div>
        <span className="sx-registry-list-count">{sensors.length} devices</span>
      </div>

      <div className="sx-registry-filters">
        <label>
          <span>Search sensors</span>
          <input
            type="search"
            placeholder="Search MAC, location, category or ID"
            value={query}
            onChange={(event) => updateQuery(event.target.value)}
          />
        </label>
        <label>
          <span>Category</span>
          <select value={category} onChange={(event) => updateCategory(event.target.value)}>
            <option value="All">All categories</option>
            {categories.map((value) => (
              <option key={value} value={value}>{value}</option>
            ))}
          </select>
        </label>
      </div>

      {filtered.length === 0 ? (
        <div className="sx-registry-empty">
          {sensors.length === 0
            ? "No sensors registered yet. Use the form above to add your first device."
            : "No sensors match your search. Try different filters."}
        </div>
      ) : (
        <>
          <div className="sx-registry-table-wrap">
            <table className="sx-registry-table">
              <thead>
                <tr>
                  <th>ID</th>
                  <th>MAC address</th>
                  <th>Location</th>
                  <th>Category</th>
                  <th>Registered</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                {visible.map((sensor) => (
                  <tr key={sensor.id}>
                    <td data-label="ID">#{sensor.id}</td>
                    <td data-label="MAC address"><code>{sensor.macAddress}</code></td>
                    <td data-label="Location">{sensor.location || "Unassigned"}</td>
                    <td data-label="Category">{sensor.category || "Uncategorised"}</td>
                    <td data-label="Registered">
                      {sensor.registeredAt && !Number.isNaN(new Date(sensor.registeredAt).getTime())
                        ? new Date(sensor.registeredAt).toLocaleString()
                        : "Unknown"}
                    </td>
                    <td data-label="Actions">
                      <div className="sx-registry-actions">
                        {onEdit && (
                          <button type="button" onClick={() => onEdit(sensor)}>Edit</button>
                        )}
                        {onDelete && (
                          <button
                            type="button"
                            className="sx-registry-delete"
                            disabled={deletingId === sensor.id}
                            onClick={() => onDelete(sensor.id)}
                          >
                            {deletingId === sensor.id ? "Deleting…" : "Delete"}
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {totalPages > 1 && (
            <div className="sx-registry-pagination">
              <span>Page {currentPage} of {totalPages} · {filtered.length} matching sensors</span>
              <div>
                <button type="button" disabled={currentPage === 1} onClick={() => setPage(currentPage - 1)}>
                  Previous
                </button>
                <button type="button" disabled={currentPage === totalPages} onClick={() => setPage(currentPage + 1)}>
                  Next
                </button>
              </div>
            </div>
          )}
        </>
      )}
    </section>
  );
}
