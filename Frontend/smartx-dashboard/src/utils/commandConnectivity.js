
export const CONNECTION_THRESHOLDS = {
  online: 30000,
  offline: 120000,
};

function getLastReceivedValue(device) {
  return (
    device?.lastReceivedAtUtc ??
    device?.lastReceivedUtc ??
    device?.lastSeenAtUtc ??
    device?.lastSeenUtc ??
    device?.lastTelemetryAtUtc ??
    null
  );
}

export function getConnectionStatus(
  device,
  now = Date.now()
) {
  if (!device) {
    return {
      status: "Unknown",
      label: "Not confirmed",
      tone: "unknown",
      canControl: false,
      lastReceivedAtUtc: null,
      ageMs: null,
    };
  }

  const receivedAt = getLastReceivedValue(device);

  const parsedTime = receivedAt
    ? new Date(receivedAt).getTime()
    : NaN;

  const ageMs = Number.isFinite(parsedTime)
    ? Math.max(0, now - parsedTime)
    : null;

  const rawStatus = String(
    device.status ??
    device.connectionStatus ??
    device.deviceStatus ??
    device.healthStatus ??
    ""
  ).trim().toLowerCase();

  let status = "Unknown";

  if (
    rawStatus === "offline" ||
    rawStatus === "disconnected" ||
    device.isConnected === false
  ) {
    status = "Offline";
  } else if (
    rawStatus === "delayed" ||
    rawStatus === "stale"
  ) {
    status = "Delayed";
  } else if (
    rawStatus === "awaitingtelemetry" ||
    rawStatus === "awaiting telemetry" ||
    rawStatus === "neverconnected"
  ) {
    status = "AwaitingTelemetry";
  } else if (
    ageMs !== null &&
    ageMs > CONNECTION_THRESHOLDS.offline
  ) {
    status = "Offline";
  } else if (
    ageMs !== null &&
    ageMs > CONNECTION_THRESHOLDS.online
  ) {
    status = "Delayed";
  } else if (
    rawStatus === "online" ||
    rawStatus === "connected" ||
    rawStatus === "active"
  ) {
    status = "Online";
  } else if (ageMs !== null) {
    status = "Online";
  }

  const labels = {
    Online: "Online",
    Delayed: "Delayed",
    Offline: "Offline",
    AwaitingTelemetry: "Awaiting telemetry",
    Unknown: "Not confirmed",
  };

  const tones = {
    Online: "success",
    Delayed: "warning",
    Offline: "danger",
    AwaitingTelemetry: "unknown",
    Unknown: "unknown",
  };

  return {
    status,
    label: labels[status],
    tone: tones[status],
    canControl: status === "Online",
    lastReceivedAtUtc: receivedAt,
    ageMs,
  };
}

export function formatTelemetryAge(ageMs) {
  if (ageMs === null || ageMs === undefined) {
    return "No telemetry timestamp";
  }

  const seconds = Math.floor(ageMs / 1000);

  if (seconds < 5) {
    return "Just received";
  }

  if (seconds < 60) {
    return `${seconds} seconds ago`;
  }

  const minutes = Math.floor(seconds / 60);

  if (minutes < 60) {
    return `${minutes} minute${minutes === 1 ? "" : "s"} ago`;
  }

  const hours = Math.floor(minutes / 60);

  return `${hours} hour${hours === 1 ? "" : "s"} ago`;
}

export function getDeviceTroubleshooting(
  connection,
  control,
  command,
  connectionError
) {
  if (connectionError) {
    return {
      title: "Unable to verify connectivity",
      message:
        "The live registry could not be loaded. " +
        "Check the Smart-X API and retry before issuing commands.",
      severity: "warning",
    };
  }

  if (connection.status === "Offline") {
    return {
      title: "Telemetry connection lost",
      message:
        "No recent telemetry is available. " +
        "Check that the simulator is running, the API is reachable " +
        "and this device has resumed reporting.",
      severity: "danger",
    };
  }

  if (connection.status === "Delayed") {
    return {
      title: "Telemetry delivery is delayed",
      message:
        "The device has not reported recently. " +
        "Check its reporting interval, queue activity and network availability.",
      severity: "warning",
    };
  }

  if (connection.status === "AwaitingTelemetry") {
    return {
      title: "Awaiting first telemetry",
      message:
        "The device is registered but has not yet submitted a reading. " +
        "Start the simulator and wait for its initial report.",
      severity: "unknown",
    };
  }

  if (connection.status === "Unknown") {
    return {
      title: "Connectivity not confirmed",
      message:
        "Smart-X does not have enough registry information " +
        "to establish whether this device is reporting.",
      severity: "unknown",
    };
  }

  if (command?.status === "Failed") {
    return {
      title: "Last command failed",
      message:
        command.failureReason ||
        "The device rejected its last command. " +
          "Inspect the command history before retrying.",
      severity: "danger",
    };
  }

  if (
    command?.status === "Pending" ||
    command?.status === "Delivered"
  ) {
    return {
      title: "Command awaiting acknowledgement",
      message:
        "A command is still outstanding. " +
        "Wait for its acknowledgement before requesting another operation.",
      severity: "warning",
    };
  }

  if (
    control?.desiredState != null &&
    control?.reportedState != null &&
    control.desiredState !== control.reportedState
  ) {
    return {
      title: "Control-state mismatch",
      message:
        "The reported actuator state differs from the requested state. " +
        "Review the latest command and device reports.",
      severity: "warning",
    };
  }

  if (control?.reportedState == null) {
    return {
      title: "Actual state not confirmed",
      message:
        "The device is submitting telemetry but has not confirmed " +
        "its actuator state. Check the state-reporting endpoint.",
      severity: "warning",
    };
  }

  return {
    title: "No immediate issues",
    message:
      "Recent telemetry is available and no outstanding " +
      "control-state discrepancy has been detected.",
    severity: "success",
  };
}
