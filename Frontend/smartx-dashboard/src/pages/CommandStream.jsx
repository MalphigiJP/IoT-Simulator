
import { useEffect, useMemo, useState } from "react";
import {
  getCommandSensors,
  getDeviceControl,
  getDeviceCommands,
  getRecentCommandEvents,
  getCommandErrorMessage,
} from "../api/commandApi";
import "./CommandStream.css";

const PAGE_SIZE = 8;

const DEVICE_TYPES = {
  smartplug: {
    label: "Smart Plug",
    target: "Relay",
    symbol: "◈",
    description: "Electrical relay control",
  },
  irrigationvalve: {
    label: "Irrigation Valve",
    target: "Valve",
    symbol: "◉",
    description: "Water flow control",
  },
  pumpcontroller: {
    label: "Pump Controller",
    target: "Pump",
    symbol: "◆",
    description: "Water pump control",
  },
};

function getDeviceType(category) {
  return DEVICE_TYPES[
    String(category || "").trim().toLowerCase()
  ];
}

function formatState(target, value) {
  if (value === null || value === undefined) {
    return "Unknown";
  }

  const states = {
    Pump: value ? "Running" : "Stopped",
    Valve: value ? "Open" : "Closed",
    Relay: value ? "On" : "Off",
  };

  return states[target] || (value ? "On" : "Off");
}

function getSyncStatus(control) {
  if (
    !control ||
    control.desiredState == null ||
    control.reportedState == null
  ) {
    return {
      label: "Unconfirmed",
      tone: "unknown",
    };
  }

  if (control.desiredState !== control.reportedState) {
    return {
      label: "State mismatch",
      tone: "warning",
    };
  }

  return {
    label: "In sync",
    tone: "success",
  };
}

function formatTime(value) {
  if (!value) {
    return "Not available";
  }

  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return "Not available";
  }

  return date.toLocaleString();
}

function CommandStatus({ status }) {
  const tone = {
    Applied: "success",
    Delivered: "warning",
    Pending: "warning",
    Failed: "danger",
    Cancelled: "unknown",
  }[status] || "unknown";

  return (
    <span className={`sx-command-pill ${tone}`}>
      {status || "No commands"}
    </span>
  );
}

function DeviceCard({ sensor, snapshot, loading }) {
  const type = getDeviceType(sensor.category);
  const control = snapshot?.control;
  const latestCommand = snapshot?.latestCommand;
  const syncStatus = getSyncStatus(control);

  return (
    <article className="sx-command-device">
      <div className="sx-command-device-top">
        <div className="sx-command-device-icon">
          {type?.symbol || "◇"}
        </div>

        {snapshot?.error ? (
          <span className="sx-command-pill danger">
            API unavailable
          </span>
        ) : loading && !control ? (
          <span className="sx-command-pill unknown">
            Loading
          </span>
        ) : (
          <span
            className={`sx-command-pill ${syncStatus.tone}`}
          >
            <span className="sx-command-status-dot" />
            {syncStatus.label}
          </span>
        )}
      </div>

      <div className="sx-command-device-heading">
        <h3>{type?.label || sensor.category}</h3>
        <p>{sensor.location || "Location not specified"}</p>
      </div>

      <div className="sx-command-device-identity">
        <span>Sensor #{sensor.id}</span>
        <span>{sensor.macAddress || "No MAC address"}</span>
      </div>

      <div className="sx-command-state-grid">
        <div className="sx-command-state-panel">
          <span>Desired state</span>
          <strong>
            {formatState(
              type?.target,
              control?.desiredState
            )}
          </strong>
        </div>

        <div className="sx-command-state-panel">
          <span>Reported state</span>
          <strong>
            {formatState(
              type?.target,
              control?.reportedState
            )}
          </strong>
        </div>
      </div>

      <div className="sx-command-device-footer">
        <div>
          <span className="sx-command-mini-label">
            Latest command
          </span>
          <CommandStatus
            status={latestCommand?.status}
          />
        </div>

        <div className="sx-command-revision">
          Revision {control?.revision ?? "—"}
        </div>
      </div>

      {latestCommand && (
        <div className="sx-command-last-action">
          {latestCommand.origin === "Undo"
            ? "Undo"
            : "Manual"}{" "}
          · {latestCommand.target}{" "}
          · {formatState(
            latestCommand.target,
            latestCommand.requestedState
          )}
        </div>
      )}

      {snapshot?.error && (
        <p className="sx-command-device-error">
          {snapshot.error}
        </p>
      )}
    </article>
  );
}

function ActivityFeed({ events, error }) {
  return (
    <section className="sx-command-panel">
      <div className="sx-command-section-heading">
        <div>
          <h2>Recent activity</h2>
          <p>Persistent command audit events</p>
        </div>

        <span className="sx-command-counter">
          {events.length}
        </span>
      </div>

      {error && (
        <div className="sx-command-inline-error">
          {error}
        </div>
      )}

      {!error && events.length === 0 && (
        <div className="sx-command-empty-activity">
          No command activity recorded yet.
        </div>
      )}

      <div className="sx-command-activity-list">
        {events.map((event) => (
          <div
            className="sx-command-activity-item"
            key={event.id}
          >
            <span className="sx-command-activity-marker" />

            <div className="sx-command-activity-content">
              <strong>{event.action}</strong>

              <span>
                Sensor #{event.sensorId}
                {event.commandId
                  ? ` · ${String(event.commandId).slice(0, 8)}`
                  : ""}
              </span>

              <small>
                {formatTime(event.occurredAtUtc)}
              </small>
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}

export default function CommandStream() {
  const [sensors, setSensors] = useState([]);
  const [sensorError, setSensorError] = useState("");
  const [loadingSensors, setLoadingSensors] = useState(true);

  const [search, setSearch] = useState("");
  const [typeFilter, setTypeFilter] = useState("all");
  const [page, setPage] = useState(1);

  const [snapshots, setSnapshots] = useState({});
  const [loadingPage, setLoadingPage] = useState(true);

  const [events, setEvents] = useState([]);
  const [auditError, setAuditError] = useState("");

  const [lastUpdated, setLastUpdated] = useState(null);
  const [refreshVersion, setRefreshVersion] = useState(0);

  const actuatorSensors = useMemo(
    () =>
      sensors.filter((sensor) =>
        Boolean(getDeviceType(sensor.category))
      ),
    [sensors]
  );

  const filteredSensors = useMemo(() => {
    const query = search.trim().toLowerCase();

    return actuatorSensors.filter((sensor) => {
      const category = String(
        sensor.category || ""
      ).toLowerCase();

      if (
        typeFilter !== "all" &&
        category !== typeFilter
      ) {
        return false;
      }

      if (!query) {
        return true;
      }

      return [
        sensor.id,
        sensor.macAddress,
        sensor.location,
        sensor.category,
        getDeviceType(sensor.category)?.label,
      ].some((value) =>
        String(value ?? "").toLowerCase().includes(query)
      );
    });
  }, [actuatorSensors, search, typeFilter]);

  const totalPages = Math.max(
    1,
    Math.ceil(filteredSensors.length / PAGE_SIZE)
  );

  const currentPage = Math.min(page, totalPages);

  const visibleSensors = useMemo(
    () =>
      filteredSensors.slice(
        (currentPage - 1) * PAGE_SIZE,
        currentPage * PAGE_SIZE
      ),
    [filteredSensors, currentPage]
  );

  useEffect(() => {
    setPage(1);
  }, [search, typeFilter]);

  useEffect(() => {
    const controller = new AbortController();
    let active = true;
    let inFlight = false;

    const loadSensors = async () => {
      if (inFlight) {
        return;
      }

      inFlight = true;

      try {
        const result = await getCommandSensors(
          controller.signal
        );

        if (!active) {
          return;
        }

        setSensors(result);
        setSensorError("");
      } catch (error) {
        if (active && !controller.signal.aborted) {
          setSensorError(
            getCommandErrorMessage(error)
          );
        }
      } finally {
        if (active) {
          setLoadingSensors(false);
        }

        inFlight = false;
      }
    };

    loadSensors();

    const interval = setInterval(loadSensors, 30000);

    return () => {
      active = false;
      clearInterval(interval);
      controller.abort();
    };
  }, [refreshVersion]);

  useEffect(() => {
    const controller = new AbortController();
    let active = true;
    let inFlight = false;

    const loadPage = async () => {
      if (inFlight) {
        return;
      }

      inFlight = true;

      try {
        const results = await Promise.all(
          visibleSensors.map(async (sensor) => {
            const [controlResult, historyResult] =
              await Promise.allSettled([
                getDeviceControl(
                  sensor.id,
                  controller.signal
                ),
                getDeviceCommands(
                  sensor.id,
                  controller.signal
                ),
              ]);

            return {
              sensorId: sensor.id,
              control:
                controlResult.status === "fulfilled"
                  ? controlResult.value
                  : null,
              latestCommand:
                historyResult.status === "fulfilled"
                  ? historyResult.value[0] || null
                  : null,
              error:
                controlResult.status === "rejected"
                  ? getCommandErrorMessage(
                      controlResult.reason
                    )
                  : "",
            };
          })
        );

        if (!active) {
          return;
        }

        const next = {};

        for (const result of results) {
          next[result.sensorId] = result;
        }

        setSnapshots(next);
        setLastUpdated(new Date());
      } finally {
        if (active) {
          setLoadingPage(false);
        }

        inFlight = false;
      }
    };

    setLoadingPage(true);
    loadPage();

    const interval = setInterval(loadPage, 5000);

    return () => {
      active = false;
      clearInterval(interval);
      controller.abort();
    };
  }, [visibleSensors, refreshVersion]);

  useEffect(() => {
    const controller = new AbortController();
    let active = true;
    let inFlight = false;

    const loadEvents = async () => {
      if (inFlight) {
        return;
      }

      inFlight = true;

      try {
        const result = await getRecentCommandEvents(
          { limit: 12 },
          controller.signal
        );

        if (active) {
          setEvents(result);
          setAuditError("");
        }
      } catch (error) {
        if (active && !controller.signal.aborted) {
          setAuditError(
            getCommandErrorMessage(error)
          );
        }
      } finally {
        inFlight = false;
      }
    };

    loadEvents();

    const interval = setInterval(loadEvents, 10000);

    return () => {
      active = false;
      clearInterval(interval);
      controller.abort();
    };
  }, [refreshVersion]);

  const visibleSnapshots = visibleSensors
    .map((sensor) => snapshots[sensor.id])
    .filter((item) => item?.control);

  const syncedCount = visibleSnapshots.filter(
    (item) =>
      getSyncStatus(item.control).tone === "success"
  ).length;

  const mismatchCount = visibleSnapshots.filter(
    (item) =>
      getSyncStatus(item.control).tone === "warning"
  ).length;

  const latestLabel = lastUpdated
    ? lastUpdated.toLocaleTimeString()
    : "Waiting for first update";

  return (
    <main className="sx-command-page">
      <header className="sx-command-header">
        <div>
          <div className="sx-command-eyebrow">
            SMART-X / DEVICE OPERATIONS
          </div>

          <h1>Command Centre</h1>

          <p>
            Monitor actuator state, command delivery and
            persistent operational activity.
          </p>
        </div>

        <div className="sx-command-header-actions">
          <div className="sx-command-live-indicator">
            <span />
            Auto-refresh enabled
          </div>

          <button
            className="sx-command-refresh"
            type="button"
            onClick={() =>
              setRefreshVersion((value) => value + 1)
            }
          >
            ↻ Refresh
          </button>
        </div>
      </header>

      {sensorError && (
        <div
          className="sx-command-error-banner"
          role="alert"
        >
          {sensorError}
        </div>
      )}

      <section className="sx-command-stats">
        <div className="sx-command-stat">
          <span>Registered actuators</span>
          <strong>{actuatorSensors.length}</strong>
          <small>Across all device pages</small>
        </div>

        <div className="sx-command-stat">
          <span>In sync</span>
          <strong>{syncedCount}</strong>
          <small>Currently visible devices</small>
        </div>

        <div className="sx-command-stat">
          <span>State mismatches</span>
          <strong>{mismatchCount}</strong>
          <small>Currently visible devices</small>
        </div>

        <div className="sx-command-stat">
          <span>Recent audit events</span>
          <strong>{events.length}</strong>
          <small>Latest recorded activities</small>
        </div>
      </section>

      <div className="sx-command-layout">
        <section className="sx-command-main">
          <div className="sx-command-panel">
            <div className="sx-command-section-heading">
              <div>
                <h2>Controllable devices</h2>
                <p>
                  Desired and device-reported states
                </p>
              </div>

              <span className="sx-command-counter">
                {filteredSensors.length} devices
              </span>
            </div>

            <div className="sx-command-filters">
              <input
                type="search"
                value={search}
                onChange={(event) =>
                  setSearch(event.target.value)
                }
                placeholder="Search by device, MAC or location"
                aria-label="Search devices"
              />

              <select
                value={typeFilter}
                onChange={(event) =>
                  setTypeFilter(event.target.value)
                }
                aria-label="Filter device type"
              >
                <option value="all">
                  All actuator types
                </option>
                <option value="smartplug">
                  Smart plugs
                </option>
                <option value="irrigationvalve">
                  Irrigation valves
                </option>
                <option value="pumpcontroller">
                  Pump controllers
                </option>
              </select>
            </div>

            {loadingSensors && sensors.length === 0 ? (
              <div className="sx-command-empty">
                Loading registered devices...
              </div>
            ) : filteredSensors.length === 0 ? (
              <div className="sx-command-empty">
                <strong>No matching actuators</strong>
                <p>
                  Start the four-device simulator or
                  adjust the current search filters.
                </p>
              </div>
            ) : (
              <div className="sx-command-device-grid">
                {visibleSensors.map((sensor) => (
                  <DeviceCard
                    key={sensor.id}
                    sensor={sensor}
                    snapshot={snapshots[sensor.id]}
                    loading={loadingPage}
                  />
                ))}
              </div>
            )}

            <div className="sx-command-pagination">
              <span>
                Page {currentPage} of {totalPages}
              </span>

              <div>
                <button
                  type="button"
                  disabled={currentPage === 1}
                  onClick={() =>
                    setPage((value) =>
                      Math.max(1, value - 1)
                    )
                  }
                >
                  Previous
                </button>

                <button
                  type="button"
                  disabled={currentPage >= totalPages}
                  onClick={() =>
                    setPage((value) =>
                      Math.min(totalPages, value + 1)
                    )
                  }
                >
                  Next
                </button>
              </div>
            </div>
          </div>

          <p className="sx-command-update-time">
            Last refreshed: {latestLabel}
          </p>
        </section>

        <aside className="sx-command-sidebar">
          <ActivityFeed
            events={events}
            error={auditError}
          />

          <section className="sx-command-panel sx-command-help">
            <h2>Control state explained</h2>

            <div>
              <span className="sx-command-pill success">
                In sync
              </span>
              <p>
                The latest reported state matches
                the requested state.
              </p>
            </div>

            <div>
              <span className="sx-command-pill warning">
                State mismatch
              </span>
              <p>
                The reported state differs from
                the requested state.
              </p>
            </div>

            <div>
              <span className="sx-command-pill unknown">
                Unconfirmed
              </span>
              <p>
                Smart-X has not received enough
                state information for comparison.
              </p>
            </div>
          </section>
        </aside>
      </div>
    </main>
  );
}
