
import { useEffect, useMemo, useState } from "react";
import { getMeshSensorTelemetry } from "../api/meshHistoryApi";
import "./MeshHistoryPanel.css";

const SESSION_KEY = "smartx.mesh.route-observations.v1";
const MAX_ROUTE_OBSERVATIONS = 30;

function restoreObservations() {
  try {
    const value = JSON.parse(sessionStorage.getItem(SESSION_KEY) || "{}");
    if (!value || typeof value !== "object" || Array.isArray(value)) return {};

    return Object.fromEntries(
      Object.entries(value).slice(0, 1000).map(([id, rows]) => [
        id,
        Array.isArray(rows) ? rows.slice(0, MAX_ROUTE_OBSERVATIONS) : [],
      ])
    );
  } catch {
    return {};
  }
}

function dateLabel(value) {
  if (!value) return "Not available";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "Not available" : date.toLocaleString();
}

function numeric(value) {
  if (value == null || value === "") return null;
  const number = Number(value);
  return Number.isFinite(number) ? number : null;
}

function measurement(value, unit, digits = 1) {
  const number = numeric(value);
  return number == null ? "—" : `${number.toFixed(digits)}${unit}`;
}

function anomalies(record) {
  const result = [];
  const temperature = numeric(record.temperature);
  const soilMoisture = numeric(record.soilMoisture);
  const wattage = numeric(record.wattage);

  if (temperature != null && temperature > 50) result.push("High temperature");
  if (soilMoisture != null && soilMoisture < 10) result.push("Low moisture");
  if (wattage != null && wattage > 6000) result.push("High wattage");

  return result;
}

function normalizeRecord(record, index) {
  return {
    ...record,
    key: record.id ?? `${record.timestamp ?? "unknown"}-${index}`,
    flags: anomalies(record),
  };
}

function routeSignature(route) {
  if (!route.reachable) return `offline:${route.reason || "No route"}`;
  return `online:${(route.path || []).join(">")}:${route.totalCost ?? "?"}`;
}

function HealthState({ observation }) {
  return (
    <span className={`sx-mh-state ${observation.reachable ? "reachable" : "unreachable"}`}>
      {observation.reachable ? "Reachable" : "Unreachable"}
    </span>
  );
}

export default function MeshHistoryPanel({ sensorId, route, snapshotKey }) {
  const [telemetry, setTelemetry] = useState([]);
  const [loadedSensorId, setLoadedSensorId] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [loadedAt, setLoadedAt] = useState(null);
  const [refreshKey, setRefreshKey] = useState(0);
  const [filter, setFilter] = useState("all");
  const [limit, setLimit] = useState(25);
  const [routeObservations, setRouteObservations] = useState(restoreObservations);

  useEffect(() => {
    try {
      sessionStorage.setItem(SESSION_KEY, JSON.stringify(routeObservations));
    } catch {
      return;
    }
  }, [routeObservations]);

  useEffect(() => {
    if (sensorId == null || !route || Number(route.sourceId) !== Number(sensorId)) {
      return;
    }

    const signature = routeSignature(route);
    const observedAt = route.generatedAtUtc || snapshotKey || new Date().toISOString();
    const observation = {
      signature,
      observedAt,
      reachable: Boolean(route.reachable),
      path: Array.isArray(route.path) ? [...route.path] : [],
      hopCount: route.hopCount ?? 0,
      totalCost: route.totalCost ?? null,
      reason: route.reason || null,
    };

    setRouteObservations((previous) => {
      const key = String(sensorId);
      const entries = previous[key] || [];
      if (entries[0]?.signature === signature) return previous;

      return {
        ...previous,
        [key]: [observation, ...entries].slice(0, MAX_ROUTE_OBSERVATIONS),
      };
    });
  }, [sensorId, route, snapshotKey]);

  useEffect(() => {
    setFilter("all");
    setLimit(25);
  }, [sensorId]);

  useEffect(() => {
    if (sensorId == null) {
      setTelemetry([]);
      setLoadedSensorId(null);
      setLoadedAt(null);
      setError("");
      setLoading(false);
      return;
    }

    const controller = new AbortController();
    let active = true;
    setLoading(true);
    setError("");

    async function load() {
      try {
        const records = await getMeshSensorTelemetry(sensorId, controller.signal);
        if (!active) return;
        setTelemetry(records.map(normalizeRecord));
        setLoadedSensorId(sensorId);
        setLoadedAt(new Date().toISOString());
      } catch (cause) {
        if (active && cause.name !== "AbortError") {
          setError(cause.message || "Could not retrieve the selected sensor's telemetry.");
        }
      } finally {
        if (active) setLoading(false);
      }
    }

    load();
    return () => {
      active = false;
      controller.abort();
    };
  }, [sensorId, snapshotKey, refreshKey]);

  const selectedTelemetry = loadedSensorId === sensorId ? telemetry : [];
  const filtered = useMemo(() => {
    return selectedTelemetry.filter((record) => filter === "all" || record.flags.length > 0);
  }, [selectedTelemetry, filter]);

  const visible = filtered.slice(0, limit);
  const routeLog = routeObservations[String(sensorId)] || [];
  const anomalyCount = selectedTelemetry.filter((item) => item.flags.length > 0).length;

  function clearRouteHistory() {
    if (sensorId == null) return;
    setRouteObservations((previous) => {
      const next = { ...previous };
      delete next[String(sensorId)];
      return next;
    });
  }

  return (
    <section className="sx-mh-section" aria-label="Selected sensor history">
      <div className="sx-mh-heading">
        <div>
          <p className="sx-mh-eyebrow">HISTORICAL ANALYSIS</p>
          <h2>Sensor telemetry & routing history</h2>
          <p>
            {sensorId == null
              ? "Select a sensor to view its saved telemetry and observed routing changes."
              : `Sensor #${sensorId} · SQL Server telemetry history and observed mesh-route changes`}
          </p>
        </div>
        <button
          type="button"
          className="sx-mh-button"
          onClick={() => setRefreshKey((value) => value + 1)}
          disabled={sensorId == null || loading}
        >
          {loading ? "Refreshing..." : "↻ Refresh history"}
        </button>
      </div>

      <div className="sx-mh-layout">
        <div className="sx-mh-panel">
          <div className="sx-mh-panel-header">
            <div>
              <h3>Historical telemetry log</h3>
              <p>Latest 100 saved SQL Server readings for the selected sensor</p>
            </div>
            <span className="sx-mh-count">{selectedTelemetry.length} records</span>
          </div>

          <div className="sx-mh-stats">
            <div><span>Returned readings</span><strong>{selectedTelemetry.length}</strong></div>
            <div><span>Threshold exceedances</span><strong>{anomalyCount}</strong></div>
            <div><span>Latest reading</span><strong className="sx-mh-small-value">{dateLabel(selectedTelemetry[0]?.timestamp)}</strong></div>
          </div>

          <div className="sx-mh-controls">
            <label>
              Show
              <select value={filter} onChange={(event) => setFilter(event.target.value)} disabled={sensorId == null}>
                <option value="all">All readings</option>
                <option value="anomalies">Threshold exceedances</option>
              </select>
            </label>
            <label>
              Rows
              <select value={limit} onChange={(event) => setLimit(Number(event.target.value))} disabled={sensorId == null}>
                <option value={25}>25</option>
                <option value={50}>50</option>
                <option value={100}>100</option>
              </select>
            </label>
          </div>

          {error && <div className="sx-mh-error" role="alert">{error}</div>}
          {loading && <p className="sx-mh-caption" role="status">Loading telemetry history...</p>}

          <div className="sx-mh-table-wrap">
            <table className="sx-mh-table">
              <thead>
                <tr>
                  <th>Timestamp</th>
                  <th>Temperature</th>
                  <th>Moisture</th>
                  <th>Power</th>
                  <th>Valve</th>
                  <th>Threshold check</th>
                </tr>
              </thead>
              <tbody>
                {visible.map((item) => (
                  <tr key={item.key}>
                    <td>{dateLabel(item.timestamp)}</td>
                    <td>{measurement(item.temperature, " °C")}</td>
                    <td>{measurement(item.soilMoisture, "%")}</td>
                    <td>{measurement(item.wattage, " W", 0)}</td>
                    <td>{item.valveOpen == null ? "—" : item.valveOpen ? "Open" : "Closed"}</td>
                    <td>
                      {item.flags.length ? (
                        <span className="sx-mh-anomaly">{item.flags.join(", ")}</span>
                      ) : (
                        <span className="sx-mh-normal">
                          {[item.temperature, item.soilMoisture, item.wattage].some((value) => numeric(value) != null)
                            ? "Within thresholds"
                            : "No monitored metrics"}
                        </span>
                      )}
                    </td>
                  </tr>
                ))}
                {!visible.length && !loading && (
                  <tr><td colSpan={6} className="sx-mh-empty">
                    {sensorId == null
                      ? "Select a sensor above to inspect telemetry."
                      : filter === "anomalies" && selectedTelemetry.length
                        ? "No threshold exceedances among the returned readings."
                        : "No stored telemetry readings available for this sensor."}
                  </td></tr>
                )}
              </tbody>
            </table>
          </div>

          <p className="sx-mh-caption">
            {filtered.length} matching rows · Loaded: {dateLabel(loadedSensorId === sensorId ? loadedAt : null)} · Thresholds: temperature &gt; 50 °C,
            moisture &lt; 10%, power &gt; 6000 W
          </p>
        </div>

        <div className="sx-mh-panel">
          <div className="sx-mh-panel-header">
            <div>
              <h3>Observed route changes</h3>
              <p>Changes in the selected sensor's route to the gateway</p>
            </div>
            <span className="sx-mh-count">{routeLog.length} changes</span>
          </div>

          <div className="sx-mh-route-notice">
            This is browser-session history, not persisted SQL Server routing history.
            The first observed route and subsequent changes are recorded while this tab is open.
          </div>

          <div className="sx-mh-route-list">
            {routeLog.map((item, index) => (
              <article className="sx-mh-route-item" key={`${item.observedAt}-${index}`}>
                <div className="sx-mh-route-item-head">
                  <HealthState observation={item} />
                  <time>{dateLabel(item.observedAt)}</time>
                </div>
                <div className="sx-mh-route-path">
                  {item.reachable ? item.path.map((node) => node === 0 ? "Gateway" : `#${node}`).join(" → ") : "No active route"}
                </div>
                <div className="sx-mh-route-meta">
                  <span>Hops: {item.reachable ? item.hopCount : "—"}</span>
                  <span>Cost: {item.totalCost == null ? "—" : Number(item.totalCost).toFixed(1)}</span>
                </div>
                {!item.reachable && item.reason && <p className="sx-mh-route-reason">{item.reason}</p>}
              </article>
            ))}

            {!routeLog.length && (
              <p className="sx-mh-empty">
                {sensorId == null
                  ? "Select a sensor to observe its route."
                  : "Waiting for the first calculated route. Route changes appear as the network refreshes."}
              </p>
            )}
          </div>

          <div className="sx-mh-route-footer">
            <span>Up to {MAX_ROUTE_OBSERVATIONS} changes per sensor, saved in this tab session.</span>
            <button type="button" className="sx-mh-clear" disabled={!routeLog.length} onClick={clearRouteHistory}>
              Clear route history
            </button>
          </div>
        </div>
      </div>
    </section>
  );
}
