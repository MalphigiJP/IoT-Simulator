
import {
  formatTelemetryAge,
  getDeviceTroubleshooting,
} from "../utils/commandConnectivity";

export default function CommandConnectivityPanel({
  sensorId,
  connection,
  control,
  latestCommand,
  connectionError,
}) {
  const diagnostic = getDeviceTroubleshooting(
    connection,
    control,
    latestCommand,
    connectionError
  );

  const ageLabel = formatTelemetryAge(
    connection.ageMs
  );

  return (
    <section
      className="sx-command-connectivity-panel"
      aria-label={`Connectivity diagnostics for sensor ${sensorId}`}
    >
      <div className="sx-command-connectivity-header">
        <span>Device connectivity</span>

        <span
          className={`sx-command-pill ${connection.tone}`}
        >
          <span className="sx-command-status-dot" />
          {connection.label}
        </span>
      </div>

      <div className="sx-command-connectivity-meta">
        <span>Last telemetry received</span>
        <strong>{ageLabel}</strong>
      </div>

      <div
        className={`sx-command-diagnostic ${diagnostic.severity}`}
      >
        <strong>{diagnostic.title}</strong>
        <p>{diagnostic.message}</p>
      </div>
    </section>
  );
}
