
import { describe, expect, it } from "vitest";
import {
  getConnectionStatus,
  formatTelemetryAge,
  getDeviceTroubleshooting,
} from "./commandConnectivity";

const NOW = new Date("2026-10-18T09:00:00Z").getTime();

function device(secondsAgo, status = "online") {
  return {
    sensorId: 15,
    status,
    lastReceivedAtUtc: new Date(
      NOW - secondsAgo * 1000
    ).toISOString(),
  };
}

describe("Device connectivity", () => {
  it("classifies recent telemetry as Online", () => {
    const result = getConnectionStatus(
      device(10),
      NOW
    );

    expect(result.status).toBe("Online");
    expect(result.canControl).toBe(true);
  });

  it("classifies delayed telemetry correctly", () => {
    const result = getConnectionStatus(
      device(45),
      NOW
    );

    expect(result.status).toBe("Delayed");
    expect(result.canControl).toBe(false);
  });

  it("classifies stale telemetry as Offline", () => {
    const result = getConnectionStatus(
      device(180),
      NOW
    );

    expect(result.status).toBe("Offline");
    expect(result.canControl).toBe(false);
  });

  it("respects an explicit disconnected status", () => {
    const result = getConnectionStatus(
      device(5, "offline"),
      NOW
    );

    expect(result.status).toBe("Offline");
    expect(result.canControl).toBe(false);
  });

  it("treats missing device data as Unknown", () => {
    const result = getConnectionStatus(
      null,
      NOW
    );

    expect(result.status).toBe("Unknown");
    expect(result.canControl).toBe(false);
  });

  it("formats telemetry age", () => {
    expect(formatTelemetryAge(10000)).toBe(
      "10 seconds ago"
    );

    expect(formatTelemetryAge(120000)).toBe(
      "2 minutes ago"
    );
  });

  it("provides troubleshooting guidance for an offline device", () => {
    const connection = getConnectionStatus(
      device(180),
      NOW
    );

    const diagnostic = getDeviceTroubleshooting(
      connection,
      null,
      null,
      ""
    );

    expect(diagnostic.title).toBe(
      "Telemetry connection lost"
    );

    expect(diagnostic.severity).toBe("danger");
  });
});
