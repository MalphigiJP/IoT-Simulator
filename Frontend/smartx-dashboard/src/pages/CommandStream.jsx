
import { useEffect, useMemo, useRef, useState } from "react";
import {
  createDeviceCommand,
  getCommandSensors,
  getDeviceControl,
  getDeviceCommands,
  getUndoAvailability,
  getRecentCommandEvents,
  undoDeviceCommand,
  getCommandErrorMessage,
  getLiveCommandDevices,
} from "../api/commandApi";
import { getConnectionStatus } from "../utils/commandConnectivity";
import { useDeviceConnectivity } from "../hooks/useDeviceConnectivity";
import CommandConnectivityPanel from "../components/CommandConnectivityPanel";
import "./CommandStream.css";
import { recordOperatorSearch } from "../api/insightApi";
const PAGE_SIZE = 8;
const HISTORY_PAGE_SIZE = 10;

const DEVICE_TYPES = {
  smartplug: {
    label: "Smart Plug",
    target: "Relay",
    symbol: "◈",
    actions: [
      { state: true, label: "Turn ON" },
      { state: false, label: "Turn OFF" },
    ],
  },
  irrigationvalve: {
    label: "Irrigation Valve",
    target: "Valve",
    symbol: "◉",
    actions: [
      { state: true, label: "Open valve" },
      { state: false, label: "Close valve" },
    ],
  },
  pumpcontroller: {
    label: "Pump Controller",
    target: "Pump",
    symbol: "◆",
    actions: [
      { state: true, label: "Start pump" },
      { state: false, label: "Stop pump" },
    ],
  },
};

const OUTSTANDING = new Set(["Pending", "Delivered"]);

function getDeviceType(category) {
  return DEVICE_TYPES[
    String(category || "").trim().toLowerCase()
  ];
}

function formatState(target, value) {
  if (value == null) {
    return "Unknown";
  }

  if (target === "Pump") {
    return value ? "Running" : "Stopped";
  }

  if (target === "Valve") {
    return value ? "Open" : "Closed";
  }

  return value ? "On" : "Off";
}

function formatTime(value) {
  if (!value) {
    return "Not available";
  }

  const date = new Date(value);

  return Number.isNaN(date.getTime())
    ? "Not available"
    : date.toLocaleString();
}

function getTone(status) {
  if (
    ["Applied", "CommandApplied", "UndoApplied"].includes(status)
  ) {
    return "success";
  }

  if (
    [
      "Pending",
      "Delivered",
      "CommandSubmitted",
      "CommandDelivered",
      "UndoRequested",
    ].includes(status)
  ) {
    return "warning";
  }

  if (
    ["Failed", "CommandFailed", "UndoFailed"].includes(status)
  ) {
    return "danger";
  }

  return "unknown";
}

function getSyncStatus(control, command) {
  if (OUTSTANDING.has(command?.status)) {
    return { label: "Awaiting device", tone: "warning" };
  }

  if (
    !control ||
    control.desiredState == null ||
    control.reportedState == null
  ) {
    return { label: "Unconfirmed", tone: "unknown" };
  }

  if (control.desiredState !== control.reportedState) {
    return { label: "State mismatch", tone: "warning" };
  }

  return { label: "States match", tone: "success" };
}

function undoExplanation(undo) {
  if (!undo) {
    return "Waiting for undo information.";
  }

  const explanations = {
    Available: "The latest manual action can be reversed.",
    NoHistory: "No reversible manual actions are available.",
    DeviceBusy: "Wait for the current command to finish.",
    UnknownState: "The actual device state is not confirmed.",
    StateChanged: "The device state no longer matches the saved action.",
    ReadOnlyDevice: "This device cannot be controlled.",
  };

  return explanations[undo.reason] || "Undo is unavailable.";
}

function StatusPill({ status }) {
  return (
    <span className={`sx-command-pill ${getTone(status)}`}>
      {status || "No commands"}
    </span>
  );
}

function DeviceCard({
  sensor,
  snapshot,
  loading,
  busy,
  feedback,
  selected,
  liveDevice,
  registryError,
  onIssueCommand,
  onUndo,
  onViewHistory,
}) {
  const type = getDeviceType(sensor.category);
  const connection = getConnectionStatus(
    registryError ? null : liveDevice
  );
  const control = snapshot?.control;
  const latestCommand = snapshot?.latestCommand;
  const undo = snapshot?.undo;
  const sync = getSyncStatus(control, latestCommand);

  const outstanding = OUTSTANDING.has(
    latestCommand?.status
  );

  const unavailable =
    busy ||
    outstanding ||
    !control ||
    control.reportedState == null ||
    Boolean(snapshot?.error) ||
    Boolean(registryError) ||
    !connection.canControl;

  const canUndo = !unavailable && undo?.canUndo === true;

  let message = feedback?.message;
  let messageTone = feedback?.tone || "success";

  if (
    feedback?.commandId &&
    feedback.commandId === latestCommand?.commandId
  ) {
    if (latestCommand.status === "Applied") {
      message = feedback.isUndo
        ? "Undo completed and acknowledged by the device."
        : "Device acknowledged and applied the command.";
      messageTone = "success";
    } else if (latestCommand.status === "Failed") {
      message =
        latestCommand.failureReason ||
        "The device could not execute the command.";
      messageTone = "error";
    } else if (latestCommand.status === "Cancelled") {
      message = "The command was cancelled.";
      messageTone = "error";
    }
  }

  return (
    <article className="sx-command-device">
      <div className="sx-command-device-top">
        <div className="sx-command-device-icon">
          {type?.symbol}
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
          <span className={`sx-command-pill ${sync.tone}`}>
            <span className="sx-command-status-dot" />
            {sync.label}
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
            {formatState(type?.target, control?.desiredState)}
          </strong>
        </div>

        <div className="sx-command-state-panel">
          <span>Reported state</span>
          <strong>
            {formatState(type?.target, control?.reportedState)}
          </strong>
        </div>
      </div>


      <CommandConnectivityPanel
        sensorId={sensor.id}
        connection={connection}
        control={control}
        latestCommand={latestCommand}
        connectionError={registryError}
      />

      <div className="sx-command-control-section">
        <div className="sx-command-control-heading">
          <span>Manual device control</span>
          <span className="sx-command-control-type">
            {type?.target}
          </span>
        </div>

        <div className="sx-command-action-grid">
          {type?.actions.map((action) => {
            const alreadyApplied =
              control?.desiredState === action.state &&
              control?.reportedState === action.state &&
              !outstanding;

            return (
              <button
                key={String(action.state)}
                type="button"
                className={[
                  "sx-command-action",
                  action.state
                    ? "sx-command-action-on"
                    : "sx-command-action-off",
                  alreadyApplied
                    ? "sx-command-action-active"
                    : "",
                ].join(" ")}
                disabled={unavailable || alreadyApplied}
                onClick={() =>
                  onIssueCommand(sensor, type.target, action.state)
                }
              >
                <span>{action.state ? "●" : "○"}</span>
                {action.label}
              </button>
            );
          })}
        </div>

        <p className="sx-command-control-hint">
          {busy
            ? "Submitting command..."
            : registryError
              ? "Manual controls paused. Connectivity information is unavailable."
              : !connection.canControl
                ? `Manual controls paused: ${connection.label.toLowerCase()}.`
                : outstanding
                  ? "Waiting for device acknowledgement."
                  : control?.reportedState == null
                    ? "Waiting for a confirmed device state."
                    : "Select an action to request a state change."}
        </p>

        <div className="sx-command-undo-panel">
          <div className="sx-command-undo-heading">
            <span>Last-action undo</span>
            <span className="sx-command-counter">
              {undo?.stackDepth ?? "—"} in stack
            </span>
          </div>

          <p>
            {undo?.canUndo
              ? `Restore ${formatState(
                  type?.target,
                  undo.restoreState
                )} from the previous manual action.`
              : undoExplanation(undo)}
          </p>

          <button
            className="sx-command-undo-button"
            type="button"
            disabled={!canUndo}
            onClick={() => onUndo(sensor, undo)}
          >
            ↶ Undo last manual action
          </button>
        </div>

        {message && (
          <div
            className={`sx-command-feedback ${messageTone}`}
            role={messageTone === "error" ? "alert" : "status"}
          >
            {message}
          </div>
        )}
      </div>

      <div className="sx-command-device-footer">
        <div>
          <span className="sx-command-mini-label">
            Latest command
          </span>
          <StatusPill status={latestCommand?.status} />
        </div>

        <span className="sx-command-revision">
          Revision {control?.revision ?? "—"}
        </span>
      </div>

      {latestCommand && (
        <div className="sx-command-last-action">
          {latestCommand.origin === "Undo" ? "Undo" : "Manual"}
          {" · "}
          {latestCommand.target}
          {" · "}
          {formatState(
            latestCommand.target,
            latestCommand.requestedState
          )}
        </div>
      )}

      <div className="sx-command-card-bottom">
        <span>
          Last confirmed: {formatTime(control?.lastReportedAtUtc)}
        </span>

        <button
          type="button"
          className={`sx-command-history-open ${
            selected ? "selected" : ""
          }`}
          onClick={() => onViewHistory(sensor.id)}
        >
          {selected ? "Viewing history" : "View history →"}
        </button>
      </div>

      {snapshot?.error && (
        <p className="sx-command-device-error">
          {snapshot.error}
        </p>
      )}

      {snapshot?.undoError && (
        <p className="sx-command-device-error">
          Undo status: {snapshot.undoError}
        </p>
      )}
    </article>
  );
}

function CommandHistoryPanel({
  sensor,
  history,
  loading,
  error,
  onClose,
}) {
  const [search, setSearch] = useState("");
  const [statusFilter, setStatusFilter] = useState("all");
  const [originFilter, setOriginFilter] = useState("all");
  const [visibleCount, setVisibleCount] = useState(HISTORY_PAGE_SIZE);
  const [expandedId, setExpandedId] = useState(null);
  const [searchActivityError, setSearchActivityError] = useState("");

  const filtered = useMemo(() => {
    const query = search.trim().toLowerCase();

    return history.filter((command) => {
      if (
        statusFilter !== "all" &&
        command.status !== statusFilter
      ) {
        return false;
      }

      if (
        originFilter !== "all" &&
        command.origin !== originFilter
      ) {
        return false;
      }

      return [
        command.commandId,
        command.undoOfCommandId,
        command.target,
        command.status,
        command.origin,
        command.sensorId,
      ].some((value) =>
        String(value ?? "").toLowerCase().includes(query)
      );
    });
  }, [history, search, statusFilter, originFilter]);

  useEffect(() => {
    setSearch("");
    setStatusFilter("all");
    setOriginFilter("all");
    setExpandedId(null);
    setVisibleCount(HISTORY_PAGE_SIZE);
  }, [sensor?.id]);

  useEffect(() => {
    setVisibleCount(HISTORY_PAGE_SIZE);
  }, [search, statusFilter, originFilter]);

  return (
    <section className="sx-command-panel sx-command-history-panel">
      <div className="sx-command-section-heading">
        <div>
          <h2>Command history</h2>
          <p>
            {sensor
              ? `Sensor #${sensor.id} · ${
                  getDeviceType(sensor.category)?.label ||
                  sensor.category
                } · ${sensor.location || "Unknown location"}`
              : "Select View history on an actuator card"}
          </p>
        </div>

        {sensor && (
          <button
            type="button"
            className="sx-command-history-close"
            onClick={onClose}
          >
            Close
          </button>
        )}
      </div>

      {!sensor ? (
        <div className="sx-command-empty">
          Select a device above to review its manual commands,
          undo operations and execution results.
        </div>
      ) : (
        <>
          <div className="sx-command-history-filters">
            <input
              type="search"
              aria-label="Search command history"
              placeholder="Search command ID, target or status"
              value={search}
              onChange={(event) => setSearch(event.target.value)}
            />

            <select
              aria-label="Filter command status"
              value={statusFilter}
              onChange={(event) =>
                setStatusFilter(event.target.value)
              }
            >
              <option value="all">All statuses</option>
              <option value="Pending">Pending</option>
              <option value="Delivered">Delivered</option>
              <option value="Applied">Applied</option>
              <option value="Failed">Failed</option>
              <option value="Cancelled">Cancelled</option>
            </select>

            <select
              aria-label="Filter command origin"
              value={originFilter}
              onChange={(event) =>
                setOriginFilter(event.target.value)
              }
            >
              <option value="all">All origins</option>
              <option value="Manual">Manual</option>
              <option value="Undo">Undo</option>
            </select>
          </div>

          <div className="sx-command-history-summary">
            <span>{history.length} saved commands</span>
            <span>{filtered.length} matching records</span>
          </div>

          {error && (
            <div className="sx-command-inline-error" role="alert">
              {error}
            </div>
          )}

          {loading && history.length === 0 && (
            <div className="sx-command-empty">
              Loading command history...
            </div>
          )}

          {!loading && !error && filtered.length === 0 && (
            <div className="sx-command-empty">
              No commands match your current filters.
            </div>
          )}

          <div className="sx-command-history-list">
            {filtered.slice(0, visibleCount).map((command) => {
              const expanded =
                expandedId === command.commandId;

              return (
                <article
                  className="sx-command-history-item"
                  key={command.commandId}
                >
                  <div className="sx-command-history-row">
                    <div className="sx-command-history-main">
                      <div className="sx-command-history-tags">
                        <StatusPill status={command.status} />
                        <span className="sx-command-origin">
                          {command.origin}
                        </span>
                      </div>

                      <strong>
                        {command.target} →{" "}
                        {formatState(
                          command.target,
                          command.requestedState
                        )}
                      </strong>

                      <span>
                        {formatTime(command.createdAtUtc)}
                      </span>
                    </div>

                    <button
                      type="button"
                      className="sx-command-details-button"
                      aria-expanded={expanded}
                      onClick={() =>
                        setExpandedId(
                          expanded ? null : command.commandId
                        )
                      }
                    >
                      {expanded ? "Hide details" : "Details"}
                    </button>
                  </div>

                  {expanded && (
                    <div className="sx-command-details">
                      <div>
                        <span>Command ID</span>
                        <strong>{command.commandId}</strong>
                      </div>
                      <div>
                        <span>Previous desired state</span>
                        <strong>
                          {formatState(
                            command.target,
                            command.previousDesiredState
                          )}
                        </strong>
                      </div>
                      <div>
                        <span>Delivered</span>
                        <strong>
                          {formatTime(command.deliveredAtUtc)}
                        </strong>
                      </div>
                      <div>
                        <span>Completed</span>
                        <strong>
                          {formatTime(command.completedAtUtc)}
                        </strong>
                      </div>
                      {command.undoOfCommandId && (
                        <div>
                          <span>Undo of command</span>
                          <strong>
                            {command.undoOfCommandId}
                          </strong>
                        </div>
                      )}
                      {command.failureReason && (
                        <div>
                          <span>Failure reason</span>
                          <strong>{command.failureReason}</strong>
                        </div>
                      )}
                    </div>
                  )}
                </article>
              );
            })}
          </div>

          {visibleCount < filtered.length && (
            <button
              type="button"
              className="sx-command-load-more"
              onClick={() =>
                setVisibleCount((count) =>
                  count + HISTORY_PAGE_SIZE
                )
              }
            >
              Show more commands
            </button>
          )}
        </>
      )}
    </section>
  );
}

function AuditTimeline({
  events,
  error,
  selectedSensorId,
  scope,
  onScopeChange,
}) {
  const [query, setQuery] = useState("");
  const [mode, setMode] = useState("commands");
  const [visibleCount, setVisibleCount] = useState(12);

  const filtered = useMemo(() => {
    const text = query.trim().toLowerCase();

    return events.filter((event) => {
      if (
        mode === "commands" &&
        ["StateReported", "StateInitialized"].includes(
          event.action
        )
      ) {
        return false;
      }

      return [
        event.action,
        event.commandId,
        event.sensorId,
        event.id,
      ].some((value) =>
        String(value ?? "").toLowerCase().includes(text)
      );
    });
  }, [events, query, mode]);

  useEffect(() => {
    setVisibleCount(12);
  }, [scope, selectedSensorId, query, mode]);

  return (
    <section className="sx-command-panel">
      <div className="sx-command-section-heading">
        <div>
          <h2>Audit timeline</h2>
          <p>Recent SQL Server audit events</p>
        </div>

        <span className="sx-command-counter">
          {filtered.length}
        </span>
      </div>

      <div className="sx-command-audit-filters">
        <select
          value={scope}
          onChange={(event) =>
            onScopeChange(event.target.value)
          }
          aria-label="Select audit scope"
        >
          <option value="all">All devices</option>
          {selectedSensorId != null && (
            <option value="selected">
              Selected sensor #{selectedSensorId}
            </option>
          )}
        </select>

        <select
          value={mode}
          onChange={(event) => setMode(event.target.value)}
          aria-label="Select audit event type"
        >
          <option value="commands">Command events</option>
          <option value="all">All events</option>
        </select>

        <input
          type="search"
          aria-label="Search audit events"
          placeholder="Search event or command ID"
          value={query}
          onChange={(event) => setQuery(event.target.value)}
        />
      </div>

      {error && (
        <div className="sx-command-inline-error" role="alert">
          {error}
        </div>
      )}

      {!error && filtered.length === 0 && (
        <div className="sx-command-empty-activity">
          No matching events in the recently loaded audit records.
        </div>
      )}

      <div className="sx-command-activity-list">
        {filtered.slice(0, visibleCount).map((event) => (
          <div
            className="sx-command-activity-item"
            key={event.id}
          >
            <span
              className={`sx-command-activity-marker ${
                getTone(event.action)
              }`}
            />

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

      {visibleCount < filtered.length && (
        <button
          type="button"
          className="sx-command-load-more"
          onClick={() =>
            setVisibleCount((count) => count + 12)
          }
        >
          Show more events
        </button>
      )}

      <p className="sx-command-audit-note">
        Showing matches from the most recent 200 audit records.
      </p>
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
  const [submittingBySensor, setSubmittingBySensor] =
    useState({});
  const [feedbackBySensor, setFeedbackBySensor] =
    useState({});

  const [selectedSensorId, setSelectedSensorId] =
    useState(null);
  const [history, setHistory] = useState([]);
  const [historyError, setHistoryError] = useState("");
  const [loadingHistory, setLoadingHistory] = useState(false);

  const [events, setEvents] = useState([]);
  const [auditError, setAuditError] = useState("");
  const [auditScope, setAuditScope] = useState("all");

  const [lastUpdated, setLastUpdated] = useState(null);
  const [refreshVersion, setRefreshVersion] = useState(0);
  const {
    devicesBySensor,
    loading: connectivityLoading,
    error: connectivityError,
    lastChecked: connectivityLastChecked,
  } = useDeviceConnectivity(refreshVersion);

  const submissionLocks = useRef(new Set());

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
      if (
        typeFilter !== "all" &&
        String(sensor.category).toLowerCase() !== typeFilter
      ) {
        return false;
      }

      return [
        sensor.id,
        sensor.category,
        sensor.location,
        sensor.macAddress,
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

  const selectedSensor = actuatorSensors.find(
    (sensor) => sensor.id === selectedSensorId
  );

  useEffect(() => {
    setPage(1);
  }, [search, typeFilter]);

  useEffect(() => {
    const controller = new AbortController();
    let active = true;
    let inFlight = false;

    async function loadSensors() {
      if (inFlight) {
        return;
      }

      inFlight = true;

      try {
        const result = await getCommandSensors(
          controller.signal
        );

        if (active) {
          setSensors(result);
          setSensorError("");
        }
      } catch (error) {
        if (active && !controller.signal.aborted) {
          setSensorError(getCommandErrorMessage(error));
        }
      } finally {
        if (active) {
          setLoadingSensors(false);
        }

        inFlight = false;
      }
    }

    loadSensors();

    const timer = setInterval(loadSensors, 30000);

    return () => {
      active = false;
      controller.abort();
      clearInterval(timer);
    };
  }, [refreshVersion]);

  useEffect(() => {
    const controller = new AbortController();
    let active = true;
    let inFlight = false;

    async function loadPage() {
      if (inFlight) {
        return;
      }

      inFlight = true;

      try {
        const results = await Promise.all(
          visibleSensors.map(async (sensor) => {
            const [controlResult, commandsResult, undoResult] =
              await Promise.allSettled([
                getDeviceControl(
                  sensor.id,
                  controller.signal
                ),
                getDeviceCommands(
                  sensor.id,
                  controller.signal
                ),
                getUndoAvailability(
                  sensor.id,
                  controller.signal
                ),
              ]);

            const control =
              controlResult.status === "fulfilled"
                ? controlResult.value
                : null;

            const latestCommand =
              commandsResult.status === "fulfilled"
                ? commandsResult.value[0] ?? null
                : null;

            const undo =
              undoResult.status === "fulfilled"
                ? undoResult.value
                : null;

            const mainFailure = [
              controlResult,
              commandsResult,
            ].find((result) => result.status === "rejected");

            return {
              sensorId: sensor.id,
              control,
              latestCommand,
              undo,
              error: mainFailure
                ? getCommandErrorMessage(mainFailure.reason)
                : "",
              undoError:
                undoResult.status === "rejected"
                  ? getCommandErrorMessage(undoResult.reason)
                  : "",
            };
          })
        );

        if (active) {
          setSnapshots((previous) => {
            const next = { ...previous };

            for (const item of results) {
              next[item.sensorId] = item;
            }

            return next;
          });

          setSensorError("");
          setLastUpdated(new Date());
        }
      } catch (error) {
        if (active && !controller.signal.aborted) {
          setSensorError(getCommandErrorMessage(error));
        }
      } finally {
        if (active) {
          setLoadingPage(false);
        }

        inFlight = false;
      }
    }

    setLoadingPage(true);
    loadPage();

    const timer = setInterval(loadPage, 5000);

    return () => {
      active = false;
      controller.abort();
      clearInterval(timer);
    };
  }, [visibleSensors, refreshVersion]);

  useEffect(() => {
    if (selectedSensorId == null) {
      setHistory([]);
      setHistoryError("");
      setLoadingHistory(false);
      return;
    }

    const controller = new AbortController();
    let active = true;
    let inFlight = false;

    async function loadHistory() {
      if (inFlight) {
        return;
      }

      inFlight = true;

      try {
        const result = await getDeviceCommands(
          selectedSensorId,
          controller.signal
        );

        if (active) {
          setHistory(result);
          setHistoryError("");
        }
      } catch (error) {
        if (active && !controller.signal.aborted) {
          setHistoryError(
            getCommandErrorMessage(error)
          );
        }
      } finally {
        if (active) {
          setLoadingHistory(false);
        }

        inFlight = false;
      }
    }

    setHistory([]);
    setLoadingHistory(true);
    loadHistory();

    const timer = setInterval(loadHistory, 8000);

    return () => {
      active = false;
      controller.abort();
      clearInterval(timer);
    };
  }, [selectedSensorId, refreshVersion]);

  const auditSensorId =
    auditScope === "selected"
      ? selectedSensorId
      : null;

  useEffect(() => {
    const controller = new AbortController();
    let active = true;
    let inFlight = false;

    async function loadEvents() {
      if (inFlight) {
        return;
      }

      inFlight = true;

      try {
        const result = await getRecentCommandEvents(
          {
            sensorId: auditSensorId ?? undefined,
            limit: 200,
          },
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
    }

    loadEvents();

    const timer = setInterval(loadEvents, 10000);

    return () => {
      active = false;
      controller.abort();
      clearInterval(timer);
    };
  }, [auditSensorId, refreshVersion]);

  async function submitOperation(
    sensor,
    execute,
    confirmationText,
    successText,
    isUndo
  ) {
    if (submissionLocks.current.has(sensor.id)) {
      return;
    }

    if (!window.confirm(confirmationText)) {
      return;
    }

    submissionLocks.current.add(sensor.id);

    setSubmittingBySensor((previous) => ({
      ...previous,
      [sensor.id]: true,
    }));

    setFeedbackBySensor((previous) => ({
      ...previous,
      [sensor.id]: null,
    }));

    try {
      const liveDevices = await getLiveCommandDevices();

      if (!Array.isArray(liveDevices)) {
        throw new Error("The live device registry returned an invalid response.");
      }

      const liveDevice = liveDevices.find(
        (device) =>
          Number(device.sensorId ?? device.id) === Number(sensor.id)
      );

      const connection = getConnectionStatus(liveDevice);

      if (!connection.canControl) {
        setFeedbackBySensor((previous) => ({
          ...previous,
          [sensor.id]: {
            tone: "error",
            message:
              `Command not submitted. Device connectivity: ${connection.label}. ` +
              "Wait for recent telemetry before retrying.",
            isUndo,
          },
        }));
        return;
      }

      const command = await execute();

      setSnapshots((previous) => {
        const current = previous[sensor.id] || {};

        return {
          ...previous,
          [sensor.id]: {
            ...current,
            latestCommand: command,
            control: current.control
              ? {
                  ...current.control,
                  desiredState: command.requestedState,
                  lastCommandId: command.commandId,
                }
              : null,
            undo: current.undo
              ? {
                  ...current.undo,
                  canUndo: false,
                  reason: "DeviceBusy",
                }
              : null,
          },
        };
      });

      setFeedbackBySensor((previous) => ({
        ...previous,
        [sensor.id]: {
          tone: "success",
          commandId: command.commandId,
          isUndo,
          message: successText,
        },
      }));
    } catch (error) {
      setFeedbackBySensor((previous) => ({
        ...previous,
        [sensor.id]: {
          tone: "error",
          message: getCommandErrorMessage(error),
          isUndo,
        },
      }));
    } finally {
      submissionLocks.current.delete(sensor.id);

      setSubmittingBySensor((previous) => ({
        ...previous,
        [sensor.id]: false,
      }));

      setRefreshVersion((value) => value + 1);
    }
  }

  function handleIssueCommand(
    sensor,
    target,
    requestedState
  ) {
    const snapshot = snapshots[sensor.id];

    if (
      !snapshot?.control ||
      snapshot.error ||
      snapshot.control.reportedState == null ||
      OUTSTANDING.has(snapshot.latestCommand?.status) ||
      Boolean(connectivityError) ||
      !getConnectionStatus(devicesBySensor[sensor.id]).canControl
    ) {
      return;
    }

    const deviceName =
      getDeviceType(sensor.category)?.label ||
      sensor.category;

    const desired = formatState(
      target,
      requestedState
    );

    return submitOperation(
      sensor,
      () =>
        createDeviceCommand(
          sensor.id,
          target,
          requestedState
        ),
      `Confirm manual command?\n\n` +
        `Device: ${deviceName}\n` +
        `Sensor: ${sensor.id}\n` +
        `Location: ${sensor.location || "Unknown"}\n` +
        `Requested state: ${desired}\n\n` +
        `The device must acknowledge this action.`,
      "Command accepted. Waiting for device acknowledgement.",
      false
    );
  }

  function handleUndo(sensor, undo) {
    const snapshot = snapshots[sensor.id];

    if (
      !undo?.canUndo ||
      snapshot?.error ||
      OUTSTANDING.has(snapshot?.latestCommand?.status) ||
      Boolean(connectivityError) ||
      !getConnectionStatus(devicesBySensor[sensor.id]).canControl
    ) {
      return;
    }

    const target = getDeviceType(sensor.category)?.target;

    return submitOperation(
      sensor,
      () => undoDeviceCommand(sensor.id),
      `Undo the last manual command?\n\n` +
        `Sensor: ${sensor.id}\n` +
        `Device: ${sensor.location || sensor.category}\n` +
        `Restore: ${formatState(target, undo.restoreState)}\n` +
        `Original command: ${undo.sourceCommandId}\n\n` +
        `A new reversing command will be sent to the device.`,
      "Undo accepted. Waiting for device acknowledgement.",
      true
    );
  }

  const visibleSnapshots = visibleSensors
    .map((sensor) => snapshots[sensor.id])
    .filter((item) => item?.control);

  const syncedCount = visibleSensors.filter((sensor) => {
    const snapshot = snapshots[sensor.id];
    const connection = getConnectionStatus(
      connectivityError ? null : devicesBySensor[sensor.id]
    );

    return (
      connection.canControl &&
      snapshot?.control &&
      getSyncStatus(snapshot.control, snapshot.latestCommand).tone ===
        "success"
    );
  }).length;

  const awaitingCount = visibleSnapshots.filter(
    (item) => OUTSTANDING.has(item.latestCommand?.status)
  ).length;

  const undoableCount = visibleSensors.filter(
    (sensor) => {
      const snapshot = snapshots[sensor.id];

      return (
        snapshot?.undo?.canUndo === true &&
        !snapshot?.error &&
        !OUTSTANDING.has(snapshot?.latestCommand?.status) &&
        !connectivityError &&
        getConnectionStatus(devicesBySensor[sensor.id]).canControl
      );
    }
  ).length;

  const connectionCounts = visibleSensors.reduce(
    (counts, sensor) => {
      const device = connectivityError
        ? null
        : devicesBySensor[sensor.id];

      const connection = getConnectionStatus(device);
      counts[connection.status] = (counts[connection.status] ?? 0) + 1;

      return counts;
    },
    {
      Online: 0,
      Delayed: 0,
      Offline: 0,
      AwaitingTelemetry: 0,
      Unknown: 0,
    }
  );

  
async function handleDeviceSearchKeyDown(event) {
  if (event.key !== "Enter" || event.nativeEvent.isComposing) {
    return;
  }

  event.preventDefault();

  const query = search.trim();

  if (query.length < 2) {
    return;
  }

    try {
      await recordOperatorSearch({
        query,
        scope: "sensors",
        resultCount: filteredSensors.length,
      });

      setSearchActivityError("");
    } catch (error) {
      setSearchActivityError(
        getCommandErrorMessage(error)
      );
    }
  }

  return (
    <main className="sx-command-page">
      <header className="sx-command-header">
        <div>
          <div className="sx-command-eyebrow">
            SMART-X / DEVICE OPERATIONS
          </div>

          <h1>Command Centre</h1>

          <p>
            Control connected actuators, undo manual actions
            and investigate command execution history.
          </p>
        </div>

        <div className="sx-command-header-actions">
          <div className="sx-command-live-indicator">
            <span />
            Auto-refresh enabled
          </div>

          <button
            type="button"
            className="sx-command-refresh"
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
          <span>Online and in sync</span>
          <strong>{syncedCount}</strong>
          <small>Currently visible devices</small>
        </div>

        <div className="sx-command-stat">
          <span>Awaiting acknowledgement</span>
          <strong>{awaitingCount}</strong>
          <small>Currently visible devices</small>
        </div>

        <div className="sx-command-stat">
          <span>Undo available</span>
          <strong>{undoableCount}</strong>
          <small>Currently visible devices</small>
        </div>
      </section>

      <section
        className="sx-command-connectivity-strip"
        aria-label="Visible device connectivity"
      >
        <div className="sx-command-connectivity-overview">
          <div>
            <h2>Connectivity overview</h2>
            <p>Telemetry health for devices on this page</p>
          </div>

          <div className="sx-command-connectivity-counts">
            <span className="sx-command-count-online">
              {connectionCounts.Online} Online
            </span>
            <span className="sx-command-count-delayed">
              {connectionCounts.Delayed} Delayed
            </span>
            <span className="sx-command-count-offline">
              {connectionCounts.Offline} Offline
            </span>
            <span className="sx-command-count-unknown">
              {connectionCounts.AwaitingTelemetry + connectionCounts.Unknown}{" "}
              Unconfirmed
            </span>
          </div>
        </div>

        <div className="sx-command-connectivity-timestamp">
          {connectivityLoading
            ? "Checking device connectivity..."
            : connectivityError
              ? "Connectivity registry unavailable"
              : connectivityLastChecked
                ? `Last checked: ${connectivityLastChecked.toLocaleTimeString()}`
                : "Waiting for connectivity information"}
        </div>

        {connectivityError && (
          <div className="sx-command-inline-error" role="alert">
            {connectivityError}
          </div>
        )}
      </section>

      <div className="sx-command-layout">
        <section className="sx-command-main">
          <div className="sx-command-panel">
            <div className="sx-command-section-heading">
              <div>
                <h2>Controllable devices</h2>
                <p>Manual overrides and undo operations</p>
              </div>

              <span className="sx-command-counter">
                {filteredSensors.length} devices
              </span>
            </div>

            <div className="sx-command-filters">
              <input
                type="search"
                value={search}
                onChange={(event) => setSearch(event.target.value)}
                placeholder="Search device, MAC or location"
                aria-label="Search devices"
              />

                {searchActivityError && (
                  <div
                    className="sx-command-inline-error"
                    role="alert"
                  >
                    Search activity could not be recorded:{" "}
                    {searchActivityError}
                  </div>
                )}

              <select
                value={typeFilter}
                onChange={(event) =>
                  setTypeFilter(event.target.value)
                }
                aria-label="Filter device type"
              >
                <option value="all">All actuator types</option>
                <option value="smartplug">Smart plugs</option>
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
                  Start the simulator or adjust your filters.
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
                    busy={Boolean(
                      submittingBySensor[sensor.id]
                    )}
                    feedback={feedbackBySensor[sensor.id]}
                    selected={selectedSensorId === sensor.id}
                    onIssueCommand={handleIssueCommand}
                    onUndo={handleUndo}
                    onViewHistory={setSelectedSensorId}
                    liveDevice={devicesBySensor[sensor.id]}
                    registryError={connectivityError}
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
            Last refreshed:{" "}
            {lastUpdated
              ? lastUpdated.toLocaleTimeString()
              : "Waiting for first update"}
          </p>
        </section>

        <aside className="sx-command-sidebar">
          <AuditTimeline
            events={events}
            error={auditError}
            selectedSensorId={selectedSensorId}
            scope={auditScope}
            onScopeChange={setAuditScope}
          />

          <section className="sx-command-panel sx-command-help">
            <h2>Command safety</h2>

            <div>
              <span className="sx-command-pill success">
                States match
              </span>
              <p>
                Desired and reported states agree. This
                does not independently prove connectivity.
              </p>
            </div>

            <div>
              <span className="sx-command-pill warning">
                Awaiting device
              </span>
              <p>
                A command is pending or delivered.
                Other operations remain disabled.
              </p>
            </div>

            <div>
              <span className="sx-command-pill unknown">
                Undo unavailable
              </span>
              <p>
                Smart-X only allows undo when the
                backend confirms a reversible state.
              </p>
            </div>
          </section>
        </aside>
      </div>

      <div className="sx-command-history-container">
        <CommandHistoryPanel
          sensor={selectedSensor}
          history={history}
          loading={loadingHistory}
          error={historyError}
          onClose={() => {
            setSelectedSensorId(null);
            setAuditScope("all");
          }}
        />
      </div>
    </main>
  );
}
