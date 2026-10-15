import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  Chart as ChartJS,
  CategoryScale,
  LinearScale,
  PointElement,
  LineElement,
  Tooltip,
  Legend,
} from "chart.js";
import { Line } from "react-chartjs-2";
import { getSensors, getSensorHealth } from "../api/sensorApi";
import { getTelemetry, getSensorTelemetry } from "../api/telemetryApi";
import Navbar from "../components/Navbar";

ChartJS.register(CategoryScale, LinearScale, PointElement, LineElement, Tooltip, Legend);

// Demonstration thresholds: move these to per-sensor server-side configuration
// before using Smart-X for production safety or operational decisions.
const THRESHOLDS = {
  temperature: { limit: 50, direction: "above", unit: "°C", label: "Temperature spike" },
  soilMoisture: { limit: 10, direction: "below", unit: "%", label: "Low soil moisture" },
  wattage: { limit: 6000, direction: "above", unit: "W", label: "Power surge" },
};

const METRICS = [
  { key: "temperature", label: "Temperature", unit: "°C", color: "#6ce7e0" },
  { key: "soilMoisture", label: "Soil moisture", unit: "%", color: "#c5e889" },
  { key: "wattage", label: "Power usage", unit: "W", color: "#b9a4ff" },
];

function isNumber(value) {
  return value !== null && value !== undefined && Number.isFinite(Number(value));
}

function isAbnormal(record, key) {
  const threshold = THRESHOLDS[key];
  if (!threshold || !isNumber(record[key])) return false;
  const value = Number(record[key]);
  return threshold.direction === "above" ? value > threshold.limit : value < threshold.limit;
}

function getAnomalies(records) {
  return records.flatMap((record) =>
    Object.entries(THRESHOLDS)
      .filter(([metric]) => isAbnormal(record, metric))
      .map(([metric, info]) => ({
        id: `${record.id ?? record.timestamp}-${record.sensorId}-${metric}`,
        sensorId: record.sensorId,
        title: info.label,
        detail: `${record[metric]} ${info.unit} (demo threshold: ${info.direction === "above" ? ">" : "<"} ${info.limit} ${info.unit})`,
        time: record.timestamp,
        severity: "critical",
      }))
  );
}

function formatTime(value) {
  if (!value) return "Never";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "Unknown" : date.toLocaleString();
}

function statusClass(status = "") {
  return String(status).toLowerCase();
}

function MetricCard({ label, value, note, tone = "neutral", onClick, active }) {
  const content = (
    <>
      <span className="sx-metric-label">{label}</span>
      <strong className="sx-metric-number">{value}</strong>
      <span className="sx-metric-note">{note}</span>
    </>
  );
  if (onClick) {
    return (
      <button
        type="button"
        className={`sx-metric sx-metric--${tone} sx-metric--button ${active ? "sx-metric--active" : ""}`}
        onClick={onClick}
        aria-pressed={Boolean(active)}
      >
        {content}
      </button>
    );
  }
  return <div className={`sx-metric sx-metric--${tone}`}>{content}</div>;
}

export default function TelemetryDashboard() {
  const [data, setData] = useState({ sensors: [], health: [], telemetry: [], updatedAt: null });
  const [initialLoading, setInitialLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const requestInFlight = useRef(false);
  const [error, setError] = useState("");
  const [metric, setMetric] = useState("temperature");
  const [sensorHistory, setSensorHistory] = useState({ sensorId: null, records: [], loading: false, error: "" });
  const [selectedSensorId, setSelectedSensorId] = useState("all");
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("All");
  const [categoryFilter, setCategoryFilter] = useState("All");
  const [guideVisible, setGuideVisible] = useState(() =>
    typeof window === "undefined" ? true : window.localStorage.getItem("smartx-guide-dismissed") !== "true"
  );

  const loadDashboard = useCallback(async (silent = false) => {
    if (requestInFlight.current) return;
    requestInFlight.current = true;
    if (silent) setRefreshing(true);
    else setInitialLoading(true);
    try {
      const [sensors, health, telemetry] = await Promise.all([
        getSensors(),
        getSensorHealth(),
        getTelemetry(),
      ]);
      if (!Array.isArray(sensors) || !Array.isArray(health) || !Array.isArray(telemetry)) {
        throw new Error("Unexpected response from Smart-X API.");
      }
      setData({ sensors, health, telemetry, updatedAt: new Date() });
      setError("");
    } catch (err) {
      setError("Unable to update Smart-X data. Check that the API is running and try again.");
      console.error("Smart-X dashboard refresh failed:", err);
    } finally {
      requestInFlight.current = false;
      setInitialLoading(false);
      setRefreshing(false);
    }
  }, []);

  useEffect(() => {
    loadDashboard();
    // Current implementation uses polling; SignalR push can replace this later.
    const interval = window.setInterval(() => loadDashboard(true), 10000);
    return () => window.clearInterval(interval);
  }, [loadDashboard]);

  const { sensors, health, telemetry, updatedAt } = data;
  useEffect(() => {
    if (selectedSensorId === "all") return;
    const id = Number(selectedSensorId);
    let cancelled = false;
    setSensorHistory((previous) => ({ ...previous, sensorId: id, loading: previous.sensorId !== id, error: "" }));
    getSensorTelemetry(id)
      .then((records) => {
        if (!cancelled) setSensorHistory({
          sensorId: id,
          records: Array.isArray(records) ? records : [],
          loading: false,
          error: "",
        });
      })
      .catch(() => {
        if (!cancelled) setSensorHistory({
          sensorId: id, records: [], loading: false,
          error: "Unable to retrieve history for this sensor.",
        });
      });
    return () => { cancelled = true; };
  }, [selectedSensorId, updatedAt]);
  const healthById = useMemo(
    () => new Map(health.map((sensor) => [Number(sensor.sensorId), sensor])),
    [health]
  );
  const sensorsById = useMemo(
    () => new Map(sensors.map((sensor) => [Number(sensor.id), sensor])),
    [sensors]
  );
  const anomalousSamples = useMemo(() => getAnomalies(telemetry), [telemetry]);
  const offline = health.filter((sensor) => sensor.status === "Offline");
  const delayed = health.filter((sensor) => sensor.status === "Delayed");
  const online = health.filter((sensor) => sensor.status === "Online");

  const events = useMemo(() => {
    const deviceEvents = health
      .filter((sensor) => sensor.status === "Offline" || sensor.status === "Delayed")
      .map((sensor) => ({
        id: `health-${sensor.sensorId}`,
        sensorId: sensor.sensorId,
        title: sensor.status === "Offline" ? "Sensor disconnected" : "Telemetry delayed",
        detail: sensor.lastTelemetry
          ? `Last message: ${formatTime(sensor.lastTelemetry)}`
          : "No telemetry received",
        time: sensor.lastTelemetry,
        severity: sensor.status === "Offline" ? "critical" : "warning",
      }));
    return [...deviceEvents, ...anomalousSamples]
      .sort((a, b) => {
        // Active disconnections remain prominent, then newer telemetry anomalies.
        if (a.id.startsWith("health-") !== b.id.startsWith("health-")) {
          return a.id.startsWith("health-") ? -1 : 1;
        }
        return new Date(b.time || 0) - new Date(a.time || 0);
      })
      .slice(0, 10);
  }, [health, anomalousSamples]);

  const metricInfo = METRICS.find((item) => item.key === metric);
  const sortedTelemetry = useMemo(
    () => [...telemetry].sort((a, b) => new Date(a.timestamp) - new Date(b.timestamp)),
    [telemetry]
  );
  const focusSensorId = useMemo(() => {
    if (selectedSensorId !== "all") return Number(selectedSensorId);
    const mostRecentWithMetric = [...sortedTelemetry]
      .reverse()
      .find((record) => isNumber(record[metric]));
    return mostRecentWithMetric ? Number(mostRecentWithMetric.sensorId) : null;
  }, [selectedSensorId, sortedTelemetry, metric]);
  const chartRecords = useMemo(() => {
    const source = selectedSensorId === "all" ? sortedTelemetry : (
      sensorHistory.sensorId === focusSensorId
        ? [...sensorHistory.records].sort((a, b) => new Date(a.timestamp) - new Date(b.timestamp))
        : []
    );
    return source
      .filter((record) => Number(record.sensorId) === focusSensorId && isNumber(record[metric]))
      .slice(-60);
  }, [sortedTelemetry, focusSensorId, metric, selectedSensorId, sensorHistory]);
  const focusSensor = focusSensorId === null ? null : sensorsById.get(focusSensorId);
  const chartData = {
    labels: chartRecords.map((record) => new Date(record.timestamp).toLocaleTimeString()),
    datasets: [{
      label: `${metricInfo.label} (${metricInfo.unit})`,
      data: chartRecords.map((record) => Number(record[metric])),
      borderColor: metricInfo.color,
      backgroundColor: metricInfo.color,
      pointBackgroundColor: chartRecords.map((record) =>
        isAbnormal(record, metric) ? "#ff7889" : metricInfo.color
      ),
      pointRadius: chartRecords.map((record) => isAbnormal(record, metric) ? 5 : 2),
      pointHoverRadius: 7,
      borderWidth: 2.5,
      tension: 0.22,
    }],
  };
  const chartOptions = {
    responsive: true,
    maintainAspectRatio: false,
    interaction: { mode: "index", intersect: false },
    plugins: {
      legend: { display: false },
      tooltip: {
        backgroundColor: "#18283c",
        titleColor: "#f6fbff",
        bodyColor: "#d9e7f4",
        callbacks: {
          title: (items) => formatTime(chartRecords[items[0]?.dataIndex]?.timestamp),
        },
      },
    },
    scales: {
      x: {
        ticks: { color: "#a4b7c9", maxTicksLimit: 6, maxRotation: 0 },
        grid: { color: "rgba(149, 174, 198, 0.08)" },
      },
      y: {
        ticks: { color: "#a4b7c9" },
        grid: { color: "rgba(149, 174, 198, 0.13)" },
      },
    },
  };

  const categories = [...new Set(sensors.map((sensor) => sensor.category).filter(Boolean))].sort();
  const alertingSensorIds = new Set([...offline, ...delayed].map((sensor) => Number(sensor.sensorId)));
  anomalousSamples.forEach((event) => alertingSensorIds.add(Number(event.sensorId)));
  const filteredSensors = sensors.filter((sensor) => {
    const searchMatch = `${sensor.macAddress} ${sensor.location} ${sensor.category}`
      .toLowerCase().includes(search.trim().toLowerCase());
    const healthStatus = healthById.get(Number(sensor.id))?.status ?? "Unknown";
    const statusMatch = statusFilter === "All"
      || (statusFilter === "Alerts" && alertingSensorIds.has(Number(sensor.id)))
      || healthStatus === statusFilter;
    return searchMatch && statusMatch && (categoryFilter === "All" || sensor.category === categoryFilter);
  });

  const onboarding = [
    { label: "Register at least one sensor", done: sensors.length > 0 },
    { label: "Assign a deployment location", done: sensors.some((s) => Boolean(s.location?.trim())) },
    { label: "Receive a telemetry sample", done: telemetry.length > 0 },
    { label: "Activate sensor health monitoring", done: health.length > 0 },
  ];
  const completed = onboarding.filter((item) => item.done).length;
  const progress = Math.round((completed / onboarding.length) * 100);

  const showSensorHealth = (status) => {
    setStatusFilter(status);
    document.getElementById("sensor-list")?.scrollIntoView({ behavior: "smooth", block: "start" });
  };

  const inspectSensor = (sensorId) => {
    setSelectedSensorId(String(sensorId));
    setSearch("");
    setStatusFilter("All");
    setCategoryFilter("All");
    document.getElementById("sx-live-chart")?.scrollIntoView({ behavior: "smooth", block: "start" });
  };
  const dismissGuide = () => {
    setGuideVisible(false);
    window.localStorage.setItem("smartx-guide-dismissed", "true");
  };

  return (
    <div className="sx-app">
      <a className="sx-skip" href="#sx-main">Skip to dashboard</a>
      <Navbar variant="sidebar" />
        <a className="sx-brand" href="/telemetry"><span className="sx-brand-mark">✳</span> Smart<span>X</span></a>
        <p className="sx-sidebar-label">WORKSPACE</p>
        <nav className="sx-nav">
          <a className="sx-nav-link sx-nav-link--active" href="/telemetry" aria-current="page">◫ &nbsp; Operations</a>
          <a className="sx-nav-link" href="/">▤ &nbsp; Sensor registry</a>
        </nav>
        <div className="sx-sidebar-foot">
          <div className="sx-live-dot" aria-hidden="true" />
          <div><strong>Gateway overview</strong><small>Auto-refresh · 10 seconds</small></div>
        </div>


      <main id="sx-main" className="sx-main">
        <header className="sx-topbar">
          <div className="sx-breadcrumb">SMART-X <span>/</span> OPERATIONS CENTRE</div>
          <div className="sx-top-actions">
            <span className="sx-freshness" role="status" aria-live="polite">
              {updatedAt ? `Updated ${updatedAt.toLocaleTimeString()}` : "Waiting for data"}
            </span>
            <button type="button" className="sx-button sx-button--secondary" onClick={() => loadDashboard(true)} disabled={refreshing}>
              {refreshing ? "Refreshing…" : "↻ Refresh data"}
            </button>
          </div>
        </header>

        <section className="sx-page-heading">
          <div>
            <div className="sx-eyebrow">DEVICE INTELLIGENCE / LIVE MONITORING</div>
            <h1>See the signal. <em>Spot the problem.</em></h1>
            <p>Monitor device health, isolate anomalous readings and investigate telemetry from one workspace.</p>
          </div>
          <span className={`sx-sync-status ${error ? "sx-sync-status--error" : ""}`}><span className="sx-live-dot" /> {error ? "CONNECTION ISSUE" : "POLLING ACTIVE"}</span>
        </section>

        {error && <div className="sx-banner sx-banner--error" role="alert">{error} {updatedAt && "Previously loaded data is still displayed."}</div>}
        {initialLoading ? (
          <div className="sx-panel sx-loading" role="status">Loading your Smart-X operations centre…</div>
        ) : (
          <>
            <section className="sx-metrics" aria-label="Operational overview">
              <MetricCard label="REGISTERED DEVICES" value={sensors.length} note="Across all deployments" />
              <MetricCard label="ONLINE NOW" value={online.length} note="Seen in the last 30 seconds" tone="good" onClick={() => showSensorHealth("Online")} active={statusFilter === "Online"} />
              <MetricCard label="OFFLINE" value={offline.length} note="No recent telemetry" tone="bad" onClick={() => showSensorHealth("Offline")} active={statusFilter === "Offline"} />
              <MetricCard label="RECENT ANOMALIES" value={anomalousSamples.length} note={`Across ${telemetry.length} recent samples`} tone="warning" onClick={() => showSensorHealth("Alerts")} active={statusFilter === "Alerts"} />
            </section>

            {guideVisible && (
              <section className="sx-panel sx-onboarding" aria-label="Getting started checklist">
                <div className="sx-onboard-copy">
                  <span className="sx-eyebrow">GUIDED SETUP</span>
                  <h2>Prepare your monitoring workspace</h2>
                  <p>Complete the core steps for reliable telemetry troubleshooting.</p>
                  <div className="sx-progress-track" role="progressbar" aria-label="Setup completion" aria-valuenow={progress} aria-valuemin="0" aria-valuemax="100">
                    <span style={{ width: `${progress}%` }} />
                  </div>
                  <strong className="sx-progress-label">{completed} of {onboarding.length} steps completed · {progress}%</strong>
                </div>
                <ul className="sx-onboard-checklist">
                  {onboarding.map((item) => (
                    <li key={item.label}><span className={item.done ? "sx-check sx-check--done" : "sx-check"}>{item.done ? "✓" : "○"}</span>{item.label}</li>
                  ))}
                </ul>
                <button type="button" className="sx-close" onClick={dismissGuide} aria-label="Dismiss setup guide">×</button>
              </section>
            )}

            <div className="sx-workspace-grid">
              <section id="sx-live-chart" className="sx-panel sx-chart-panel" aria-labelledby="sx-chart-heading">
                <div className="sx-panel-header">
                  <div><span className="sx-eyebrow">ANALYSE</span><h2 id="sx-chart-heading">Telemetry trends</h2></div>
                  <span className="sx-small-tag">Up to 100 recent samples per sensor</span>
                </div>
                <div className="sx-toolbar">
                  <div className="sx-segmented" role="group" aria-label="Select measurement">
                    {METRICS.map((item) => (
                      <button type="button" key={item.key} className={metric === item.key ? "sx-selected" : ""} aria-pressed={metric === item.key} onClick={() => setMetric(item.key)}>{item.label}</button>
                    ))}
                  </div>
                  <label className="sx-field sx-field--sensor">
                    <span>Sensor</span>
                    <select value={selectedSensorId} onChange={(e) => setSelectedSensorId(e.target.value)}>
                      <option value="all">Latest reporting sensor</option>
                      {sensors.map((sensor) => <option value={sensor.id} key={sensor.id}>{sensor.location} · {sensor.macAddress}</option>)}
                    </select>
                  </label>
                </div>
                <div className="sx-chart-context">
                  <div><strong>{focusSensor?.location || "No sensor selected"}</strong><span>{focusSensor?.macAddress || "Choose a device to inspect"}</span></div>
                  <div className="sx-chart-legend"><span style={{ backgroundColor: metricInfo.color }} /> {metricInfo.label}</div>
                </div>
                {selectedSensorId !== "all" && sensorHistory.loading ? (
                  <div className="sx-empty sx-chart-empty" role="status">Loading device history…</div>
                ) : selectedSensorId !== "all" && sensorHistory.error ? (
                  <div className="sx-empty sx-chart-empty" role="alert">{sensorHistory.error}</div>
                ) : chartRecords.length > 0 ? (
                  <div className="sx-chart-wrap" role="img" aria-label={`Line chart of ${metricInfo.label} for ${focusSensor?.location ?? "selected sensor"}, showing ${chartRecords.length} readings`}>
                    <Line data={chartData} options={chartOptions} />
                  </div>
                ) : (
                  <div className="sx-empty sx-chart-empty">No {metricInfo.label.toLowerCase()} readings for this sensor in the latest data window.</div>
                )}
                <details className="sx-help">
                  <summary>How do I investigate a spike?</summary>
                  <p>Choose a metric, select an affected sensor and inspect the red points. Compare the timestamp with the alert feed and the sensor's last reported status. Thresholds currently use demonstration values.</p>
                </details>
              </section>

              <section className="sx-panel sx-alert-panel" aria-labelledby="sx-alert-heading">
                <div className="sx-panel-header">
                  <div><span className="sx-eyebrow">PRIORITISE</span><h2 id="sx-alert-heading">Attention needed</h2></div>
                  <span className="sx-alert-count">{events.length} shown</span>
                </div>
                <p className="sx-panel-subtitle">Active connectivity problems and abnormal recent samples.</p>
                {events.length === 0 ? (
                  <div className="sx-empty"><span className="sx-empty-symbol">✓</span>No current health issues or threshold breaches in the loaded data.</div>
                ) : (
                  <div className="sx-alert-list">
                    {events.map((event) => (
                      <button key={event.id} type="button" className="sx-event" onClick={() => inspectSensor(event.sensorId)} aria-label={`Inspect sensor ${event.sensorId}: ${event.title}`}>
                        <span className={`sx-event-indicator sx-event-indicator--${event.severity}`} aria-hidden="true" />
                        <span className="sx-event-content"><strong>{event.title}</strong><span>{sensorsById.get(Number(event.sensorId))?.location || `Sensor ${event.sensorId}`}</span><small>{event.detail}</small></span>
                        <span className="sx-event-arrow" aria-hidden="true">↗</span>
                      </button>
                    ))}
                  </div>
                )}
                <details className="sx-help"><summary>What do these alerts mean?</summary><p>Offline means no telemetry within 120 seconds; delayed means 30–120 seconds. Temperature &gt; 50 °C, moisture &lt; 10% and wattage &gt; 6000 W are demonstration anomaly rules, not certified alarm settings.</p></details>
              </section>
            </div>

            <section id="sensor-list" className="sx-panel sx-sensor-panel" aria-labelledby="sx-sensor-heading">
              <div className="sx-panel-header">
                <div><span className="sx-eyebrow">INVESTIGATE</span><h2 id="sx-sensor-heading">Sensor health directory</h2></div>
                <span className="sx-small-tag">{delayed.length} delayed · {offline.length} offline</span>
              </div>
              <div className="sx-table-toolbar">
                <label className="sx-field sx-search"><span>Search devices</span><input type="search" placeholder="MAC address, location or category" value={search} onChange={(e) => setSearch(e.target.value)} /></label>
                <label className="sx-field"><span>Health</span><select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)}><option>All</option><option>Online</option><option>Delayed</option><option>Offline</option><option>Alerts</option></select></label>
                <label className="sx-field"><span>Category</span><select value={categoryFilter} onChange={(e) => setCategoryFilter(e.target.value)}><option>All</option>{categories.map((category) => <option key={category}>{category}</option>)}</select></label>
                <button type="button" className="sx-button sx-button--text" onClick={() => { setSearch(""); setStatusFilter("All"); setCategoryFilter("All"); setSelectedSensorId("all"); }}>Clear filters</button>
              </div>
              <div className="sx-table-scroll">
                <table className="sx-table">
                  <thead><tr><th>DEVICE / LOCATION</th><th>MAC ADDRESS</th><th>CATEGORY</th><th>HEALTH</th><th>LAST READING</th><th>ACTION</th></tr></thead>
                  <tbody>
                    {filteredSensors.slice(0, 20).map((sensor) => {
                      const status = healthById.get(Number(sensor.id));
                      return (
                        <tr key={sensor.id}>
                          <td data-label="Location"><strong>{sensor.location || "Unassigned"}</strong><small>Sensor #{sensor.id}</small></td>
                          <td data-label="MAC address"><code>{sensor.macAddress}</code></td>
                          <td data-label="Category">{sensor.category}</td>
                          <td data-label="Health"><span className={`sx-status sx-status--${statusClass(status?.status ?? "unknown")}`}><i />{status?.status || "Unknown"}</span></td>
                          <td data-label="Last reading">{formatTime(status?.lastTelemetry)}</td>
                          <td data-label="Action"><button className="sx-inline-link" type="button" onClick={() => inspectSensor(sensor.id)}>Inspect ↗</button></td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
              {filteredSensors.length === 0 && <div className="sx-empty">No sensors match your filters. Try clearing the search or health filter.</div>}
              {filteredSensors.length > 20 && <p className="sx-table-note">Showing 20 of {filteredSensors.length} matching devices. Narrow the search to locate a device. Server-side pagination is recommended for large deployments.</p>}
            </section>
            <footer className="sx-footer">Smart-X · IoT monitoring prototype <span>Data refreshes every 10 seconds; sensor statuses use the API's latest-received timestamps.</span></footer>
          </>
        )}
      </main>
    </div>
  );
}
