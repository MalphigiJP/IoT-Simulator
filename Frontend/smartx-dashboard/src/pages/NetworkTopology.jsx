
import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import Navbar from "../components/Navbar";
import {
  getMeshMinimumSpanningTree,
  getMeshNodeById,
  getMeshNodeByMac,
  getMeshPreview,
  getMeshRoute,
} from "../api/meshApi";
import "./NetworkTopology.css";
import MeshHistoryPanel from "../components/MeshHistoryPanel";

const POLL_INTERVAL = 20000;
const MAX_VISIBLE_NODES = 60;
const VIEW_BOX = "0 0 1000 720";

function connectionKey(first, second) {
  return `${Math.min(first, second)}:${Math.max(first, second)}`;
}

function formatTime(value) {
  if (!value) return "No telemetry received";
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? "Not available" : date.toLocaleString();
}

function formatCost(value) {
  return typeof value === "number" ? value.toFixed(1) : "—";
}

function buildPositions(nodes, gatewayId) {
  const result = new Map();
  result.set(gatewayId, { x: 500, y: 360 });

  const sensors = nodes.filter((node) => node.id !== gatewayId);
  const capacities = [12, 20, 28];
  const radii = [110, 205, 305];
  let offset = 0;

  for (let ring = 0; ring < capacities.length && offset < sensors.length; ring++) {
    const count = Math.min(capacities[ring], sensors.length - offset);

    for (let position = 0; position < count; position++) {
      const angle =
        -Math.PI / 2 +
        (2 * Math.PI * (position + (ring % 2 ? 0.4 : 0))) / count;

      result.set(sensors[offset + position].id, {
        x: 500 + Math.cos(angle) * radii[ring] * 1.25,
        y: 360 + Math.sin(angle) * radii[ring],
      });
    }

    offset += count;
  }

  return result;
}

function MeshDiagram({ snapshot, mst, route, selectedId, mode, onSelect }) {
  const graph = useMemo(() => {
    const nodes = snapshot?.nodes || [];
    const gatewayId = snapshot?.gatewayId ?? 0;
    const routeIds = Array.isArray(route?.path) ? route.path : [];
    const required = new Set([gatewayId, selectedId, ...routeIds]);
    const priority = [
      ...nodes.filter((node) => required.has(node.id)),
      ...nodes.filter((node) => !required.has(node.id) && node.isOnline),
      ...nodes.filter((node) => !required.has(node.id) && !node.isOnline),
    ];
    const unique = new Map();

    for (const node of priority) {
      if (!unique.has(node.id) && unique.size < MAX_VISIBLE_NODES) {
        unique.set(node.id, node);
      }
    }

    const visible = [...unique.values()];
    const positions = buildPositions(visible, gatewayId);
    const health = new Map(
      (snapshot?.deviceHealth || []).map((item) => [item.sensorId, item])
    );
    const routeLinks = new Set(
      (route?.hops || []).map((hop) => connectionKey(hop.fromId, hop.toId))
    );
    const links = mode === "mst" ? mst?.edges || [] : snapshot?.links || [];

    return {
      visible,
      positions,
      health,
      routeLinks,
      links: links.filter(
        (link) => positions.has(link.fromId) && positions.has(link.toId)
      ),
      omitted: Math.max(0, nodes.length - visible.length),
      gatewayId,
    };
  }, [snapshot, mst, route, selectedId, mode]);

  if (!snapshot) {
    return <div className="sx-mesh-graph-empty">Waiting for topology data...</div>;
  }

  return (
    <div className="sx-mesh-graph-area">
      <svg
        className="sx-mesh-svg"
        viewBox={VIEW_BOX}
        preserveAspectRatio="xMidYMid meet"
        role="group"
        aria-label="Interactive simulated Smart-X mesh network"
      >
        <defs>
          <pattern id="sx-mesh-grid" width="35" height="35" patternUnits="userSpaceOnUse">
            <path d="M 35 0 L 0 0 0 35" fill="none" stroke="#263a4c" strokeWidth="0.65" />
          </pattern>
        </defs>
        <rect width="1000" height="720" rx="14" fill="url(#sx-mesh-grid)" />
        {graph.links.map((link) => {
          const start = graph.positions.get(link.fromId);
          const end = graph.positions.get(link.toId);
          const onRoute = graph.routeLinks.has(connectionKey(link.fromId, link.toId));
          const highlight = mode === "route" && onRoute;
          const gatewayLink = link.fromId === graph.gatewayId || link.toId === graph.gatewayId;

          return (
            <line
              key={connectionKey(link.fromId, link.toId)}
              x1={start.x}
              y1={start.y}
              x2={end.x}
              y2={end.y}
              className={
                highlight
                  ? "sx-mesh-edge sx-mesh-edge-route"
                  : mode === "mst"
                    ? "sx-mesh-edge sx-mesh-edge-mst"
                    : gatewayLink
                      ? "sx-mesh-edge sx-mesh-edge-gateway"
                      : "sx-mesh-edge"
              }
            >
              <title>
                {`Node ${link.fromId} ↔ Node ${link.toId} • Simulated cost ${link.cost}`}
              </title>
            </line>
          );
        })}
        {graph.visible.map((node) => {
          const position = graph.positions.get(node.id);
          const status = node.isGateway
            ? "gateway"
            : (graph.health.get(node.id)?.status || "Unknown").toLowerCase();
          const selected = node.id === selectedId;
          const pathNode = (route?.path || []).includes(node.id);
          const label = node.isGateway ? "GW" : `#${node.id}`;

          return (
            <g
              key={node.id}
              className={`sx-mesh-node sx-mesh-node-${status}${selected ? " sx-mesh-node-selected" : ""}${mode === "route" && pathNode ? " sx-mesh-node-on-route" : ""}`}
              transform={`translate(${position.x},${position.y})`}
              role={node.isGateway ? "img" : "button"}
              tabIndex={node.isGateway ? undefined : 0}
              aria-label={
                node.isGateway
                  ? "Central Smart-X gateway"
                  : `Select sensor ${node.id}, ${status}, ${node.location || "unknown location"}`
              }
              onClick={node.isGateway ? undefined : () => onSelect(node.id)}
              onKeyDown={
                node.isGateway
                  ? undefined
                  : (event) => {
                      if (event.key === "Enter" || event.key === " ") {
                        event.preventDefault();
                        onSelect(node.id);
                      }
                    }
              }
            >
              {selected && <circle r="34" className="sx-mesh-node-selection-ring" />}
              <circle r={node.isGateway ? "31" : "22"} className="sx-mesh-node-circle" />
              <text className="sx-mesh-node-label" textAnchor="middle" dy="4">
                {label}
              </text>
              <title>
                {node.isGateway
                  ? "Central gateway"
                  : `Sensor ${node.id} • ${node.macAddress} • ${node.location || "No location"} • ${status}`}
              </title>
            </g>
          );
        })}
      </svg>
      <div className="sx-mesh-graph-caption">
        <span>Gateway: node {graph.gatewayId} · Click any sensor to inspect it</span>
        {graph.omitted > 0 && (
          <span>
            Showing {graph.visible.length} of {snapshot.nodes.length} nodes. Search for any hidden sensor by ID or MAC.
          </span>
        )}
      </div>
    </div>
  );
}

function HealthPill({ status }) {
  const value = status || "Unknown";
  const className = ["Online", "Delayed", "Offline", "Unknown"].includes(value)
    ? value.toLowerCase()
    : "unknown";

  return <span className={`sx-mesh-pill sx-mesh-pill-${className}`}>{value}</span>;
}

export default function NetworkTopology() {
  const [snapshot, setSnapshot] = useState(null);
  const [mst, setMst] = useState(null);
  const [selectedId, setSelectedId] = useState(null);
  const [route, setRoute] = useState(null);
  const [mode, setMode] = useState("all");
  const [query, setQuery] = useState("");
  const [healthFilter, setHealthFilter] = useState("all");
  const [refreshVersion, setRefreshVersion] = useState(0);
  const [initialLoading, setInitialLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [lookupLoading, setLookupLoading] = useState(false);
  const [routeLoading, setRouteLoading] = useState(false);
  const [error, setError] = useState("");
  const [lookupError, setLookupError] = useState("");
  const [routeError, setRouteError] = useState("");

  useEffect(() => {
    const timer = setInterval(() => {
      setRefreshVersion((value) => value + 1);
    }, POLL_INTERVAL);

    return () => clearInterval(timer);
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    let active = true;

    async function refresh() {
      setRefreshing(true);

      try {
        const [nextSnapshot, nextMst] = await Promise.all([
          getMeshPreview(controller.signal),
          getMeshMinimumSpanningTree(controller.signal),
        ]);

        if (!active) return;

        setSnapshot(nextSnapshot);
        setMst(nextMst);
        setError("");
        setSelectedId((previous) => {
          if (nextSnapshot.nodes.some((node) => node.id === previous && !node.isGateway)) {
            return previous;
          }
          const candidate = nextSnapshot.nodes.find((node) => !node.isGateway && node.isOnline)
            || nextSnapshot.nodes.find((node) => !node.isGateway);
          return candidate?.id ?? null;
        });
      } catch (cause) {
        if (active && cause.name !== "AbortError") {
          setError(cause.message || "Could not load the mesh network.");
        }
      } finally {
        if (active) {
          setRefreshing(false);
          setInitialLoading(false);
        }
      }
    }

    refresh();

    return () => {
      active = false;
      controller.abort();
    };
  }, [refreshVersion]);

  useEffect(() => {
    if (selectedId == null || !snapshot) {
      setRoute(null);
      setRouteError("");
      setRouteLoading(false);
      return;
    }

    const controller = new AbortController();
    let active = true;

    async function loadRoute() {
      setRouteLoading(true);
      setRouteError("");
      setRoute(null);

      try {
        const result = await getMeshRoute(selectedId, controller.signal);
        if (active) setRoute(result);
      } catch (cause) {
        if (active && cause.name !== "AbortError") {
          setRouteError(cause.message || "Could not calculate the selected route.");
        }
      } finally {
        if (active) setRouteLoading(false);
      }
    }

    loadRoute();

    return () => {
      active = false;
      controller.abort();
    };
  }, [selectedId, snapshot?.generatedAtUtc]);

  const health = snapshot?.deviceHealth || [];
  const selectedNode = snapshot?.nodes?.find((node) => node.id === selectedId) || null;
  const selectedHealth = health.find((item) => item.sensorId === selectedId) || null;
  const visibleHealth = useMemo(() => {
    return health
      .filter((item) => healthFilter === "all" || item.status === healthFilter)
      .sort((a, b) => a.sensorId - b.sensorId);
  }, [health, healthFilter]);

  async function handleSearch(event) {
    event.preventDefault();
    const value = query.trim();
    if (!value) {
      setLookupError("Enter a sensor ID or MAC address.");
      return;
    }

    setLookupLoading(true);
    setLookupError("");

    try {
      const result = /^\d+$/.test(value)
        ? await getMeshNodeById(Number(value))
        : await getMeshNodeByMac(value);

      setSelectedId(result.node.id);
      setMode("route");
    } catch (cause) {
      setLookupError(cause.message || "Sensor lookup failed.");
    } finally {
      setLookupLoading(false);
    }
  }

  return (
    <div className="sx-topology-app">
      <Navbar variant="top" />

      <main className="sx-topology-page" id="main-content">
        <header className="sx-topology-header">
          <div>
            <p className="sx-topology-eyebrow">SMART-X / NETWORK INFRASTRUCTURE</p>
            <h1>Network Topology <span>& Mesh Routing</span></h1>
            <p>
              Explore the simulated IoT network, inspect device connectivity and trace
              routes from registered sensors to the central gateway.
            </p>
          </div>
          <div className="sx-topology-header-actions">
            <span className="sx-topology-poll-label">Updates every 20 seconds</span>
            <button
              type="button"
              className="sx-topology-button sx-topology-button-primary"
              onClick={() => setRefreshVersion((value) => value + 1)}
              disabled={refreshing}
            >
              {refreshing ? "Refreshing..." : "↻ Refresh network"}
            </button>
          </div>
        </header>

        <div className="sx-topology-notice">
          <strong>Simulated mesh network</strong>
          <span>
            Nodes come from registered SQL sensors. Network links and costs are simulated;
            node health is inferred from the age of received telemetry.
          </span>
        </div>

        {error && <div className="sx-topology-error" role="alert">{error}</div>}

        <section className="sx-topology-stats" aria-label="Mesh network summary">
          {[
            ["Registered sensors", snapshot?.registeredSensorCount ?? "—"],
            ["Online", snapshot?.onlineCount ?? "—"],
            ["Delayed", snapshot?.delayedCount ?? "—"],
            ["Offline / Unknown", snapshot ? snapshot.offlineCount + snapshot.unknownCount : "—"],
            ["Gateway reachable", snapshot?.reachableSensorCount ?? "—"],
          ].map(([label, value]) => (
            <div className="sx-topology-stat" key={label}>
              <span>{label}</span>
              <strong>{value}</strong>
            </div>
          ))}
        </section>

        <form className="sx-topology-search" onSubmit={handleSearch}>
          <label htmlFor="sx-topology-sensor-search">Find a sensor and trace its route</label>
          <div className="sx-topology-search-row">
            <input
              id="sx-topology-sensor-search"
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              placeholder="Sensor ID or MAC address (e.g. 02:53:58:00:00:01)"
              aria-describedby={lookupError ? "sx-topology-lookup-error" : undefined}
            />
            <button className="sx-topology-button sx-topology-button-primary" type="submit" disabled={lookupLoading}>
              {lookupLoading ? "Finding..." : "Find & trace"}
            </button>
          </div>
          {lookupError && <p id="sx-topology-lookup-error" className="sx-topology-field-error" role="alert">{lookupError}</p>}
        </form>

        <div className="sx-topology-workspace">
          <section className="sx-topology-card sx-topology-graph-card" aria-label="Network diagram">
            <div className="sx-topology-card-head">
              <div>
                <h2>Interactive mesh map</h2>
                <p>Choose a view and click any sensor node to inspect its route.</p>
              </div>
              <span className="sx-topology-chip">{snapshot?.links?.length ?? 0} logical links</span>
            </div>

            <div className="sx-topology-modes" role="group" aria-label="Network diagram mode">
              {[
                ["all", "All links"],
                ["route", "Shortest route"],
                ["mst", "MST backbone"],
              ].map(([value, label]) => (
                <button
                  type="button"
                  key={value}
                  className={mode === value ? "sx-topology-mode active" : "sx-topology-mode"}
                  aria-pressed={mode === value}
                  onClick={() => setMode(value)}
                >
                  {label}
                </button>
              ))}
            </div>

            <div className="sx-topology-legend">
              {[
                ["gateway", "Gateway"],
                ["online", "Online"],
                ["delayed", "Delayed"],
                ["offline", "Offline"],
                ["unknown", "Unknown"],
              ].map(([key, label]) => (
                <span key={key}><i className={`sx-topology-legend-dot ${key}`} />{label}</span>
              ))}
            </div>

            <MeshDiagram
              snapshot={snapshot}
              mst={mst}
              route={route}
              selectedId={selectedId}
              mode={mode}
              onSelect={(id) => {
                setSelectedId(id);
                setMode("route");
              }}
            />

            <p className="sx-topology-map-note">
              {mode === "mst"
                ? "MST mode displays Prim's minimum-total-cost backbone, not individual shortest paths."
                : mode === "route"
                  ? "The turquoise links show the lowest-cost path for the selected sensor."
                  : "The diagram shows all simulated connections among the displayed nodes."}
            </p>
          </section>

          <aside className="sx-topology-sidebar" aria-label="Sensor and route details">
            <section className="sx-topology-card">
              <div className="sx-topology-card-head">
                <div>
                  <h2>Selected sensor</h2>
                  <p>Device identity and telemetry-based status</p>
                </div>
              </div>
              {!selectedNode ? (
                <p className="sx-topology-empty">{initialLoading ? "Loading network..." : "Select a sensor from the graph or search box."}</p>
              ) : (
                <div className="sx-topology-details">
                  <div className="sx-topology-selected-title">
                    <strong>Sensor #{selectedNode.id}</strong>
                    <HealthPill status={selectedHealth?.status} />
                  </div>
                  <dl>
                    <dt>MAC address</dt><dd>{selectedNode.macAddress || "Not specified"}</dd>
                    <dt>Location</dt><dd>{selectedNode.location || "Not specified"}</dd>
                    <dt>Category</dt><dd>{selectedHealth?.category || "Not specified"}</dd>
                    <dt>Last telemetry</dt><dd>{formatTime(selectedHealth?.lastTelemetryAtUtc)}</dd>
                  </dl>
                  <Link className="sx-topology-inline-link" to="/telemetry">View telemetry dashboard →</Link>
                </div>
              )}
            </section>

            <section className="sx-topology-card">
              <div className="sx-topology-card-head">
                <div>
                  <h2>Shortest route to gateway</h2>
                  <p>Dijkstra's lowest-cost active path</p>
                </div>
              </div>
              {routeLoading ? (
                <p className="sx-topology-empty">Calculating route...</p>
              ) : routeError ? (
                <p className="sx-topology-field-error" role="alert">{routeError}</p>
              ) : !route ? (
                <p className="sx-topology-empty">Select a sensor to calculate a route.</p>
              ) : !route.reachable ? (
                <div className="sx-topology-unreachable">
                  <strong>Gateway unreachable</strong>
                  <p>{route.reason || "No active route exists for this sensor."}</p>
                </div>
              ) : (
                <div className="sx-topology-route-result">
                  <div className="sx-topology-route-metrics">
                    <div><span>Hops</span><strong>{route.hopCount}</strong></div>
                    <div><span>Simulated cost</span><strong>{formatCost(route.totalCost)}</strong></div>
                  </div>
                  <div className="sx-topology-route-path" aria-label="Route nodes in order">
                    {route.path.map((id, index) => (
                      <span key={`${id}-${index}`}>
                        {index > 0 && <b aria-hidden="true">→</b>}
                        <em>{id === route.gatewayId ? "Gateway" : `#${id}`}</em>
                      </span>
                    ))}
                  </div>
                  <button
                    type="button"
                    className="sx-topology-button sx-topology-button-secondary"
                    onClick={() => setMode("route")}
                  >
                    Highlight route on map
                  </button>
                </div>
              )}
            </section>

            <section className="sx-topology-card">
              <div className="sx-topology-card-head">
                <div>
                  <h2>Minimum Spanning Tree</h2>
                  <p>Prim's network backbone</p>
                </div>
              </div>
              <div className="sx-topology-route-metrics">
                <div><span>Connected sensors</span><strong>{mst?.connectedSensorCount ?? "—"}</strong></div>
                <div><span>Total link cost</span><strong>{formatCost(mst?.totalCost)}</strong></div>
              </div>
              <p className="sx-topology-muted">
                {mst?.unreachableNodeIds?.length ?? "—"} sensors currently unreachable.
              </p>
              <button
                type="button"
                className="sx-topology-button sx-topology-button-secondary"
                onClick={() => setMode("mst")}
                disabled={!mst}
              >
                Show MST backbone
              </button>
            </section>
          </aside>
        </div>


        <MeshHistoryPanel
          sensorId={selectedId}
          route={route}
          snapshotKey={snapshot?.generatedAtUtc}
        />


        <section className="sx-topology-card sx-topology-device-directory">
          <div className="sx-topology-card-head">
            <div>
              <h2>Network device directory</h2>
              <p>All registered sensors, including nodes not drawn on the map</p>
            </div>
            <select
              value={healthFilter}
              onChange={(event) => setHealthFilter(event.target.value)}
              aria-label="Filter sensors by network health"
              className="sx-topology-select"
            >
              <option value="all">All statuses</option>
              <option value="Online">Online</option>
              <option value="Delayed">Delayed</option>
              <option value="Offline">Offline</option>
              <option value="Unknown">Unknown</option>
            </select>
          </div>
          <div className="sx-topology-directory-scroll">
            <table className="sx-topology-table">
              <thead><tr><th>Sensor</th><th>MAC address</th><th>Location</th><th>Health</th><th>Last seen</th><th>Action</th></tr></thead>
              <tbody>
                {visibleHealth.map((item) => (
                  <tr key={item.sensorId} className={item.sensorId === selectedId ? "selected" : ""}>
                    <td>#{item.sensorId}</td>
                    <td>{item.macAddress}</td>
                    <td>{item.location || "—"}</td>
                    <td><HealthPill status={item.status} /></td>
                    <td>{formatTime(item.lastTelemetryAtUtc)}</td>
                    <td>
                      <button
                        type="button"
                        className="sx-topology-table-button"
                        onClick={() => {
                          setSelectedId(item.sensorId);
                          setMode("route");
                          window.scrollTo({ top: 0, behavior: "smooth" });
                        }}
                      >
                        Trace route
                      </button>
                    </td>
                  </tr>
                ))}
                {visibleHealth.length === 0 && (
                  <tr><td colSpan={6}>No registered sensors match this health filter.</td></tr>
                )}
              </tbody>
            </table>
          </div>
        </section>
        <footer className="sx-topology-footer">
          <span>Smart-X IoT Ecosystem · Simulated Network Topology</span>
          <span>Last snapshot: {formatTime(snapshot?.generatedAtUtc)}</span>
        </footer>
      </main>
    </div>
  );
}
