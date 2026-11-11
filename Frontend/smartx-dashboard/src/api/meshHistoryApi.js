
const configuredBase = (
  import.meta.env.VITE_API_URL || "http://localhost:5000/api"
).replace(/\/+$/, "");

const API_BASE = /\/api$/i.test(configuredBase)
  ? configuredBase
  : `${configuredBase}/api`;

export async function getMeshSensorTelemetry(sensorId, signal) {
  if (!Number.isSafeInteger(sensorId) || sensorId <= 0) {
    throw new Error("A valid sensor ID is required.");
  }

  const response = await fetch(
    `${API_BASE}/sensors/${sensorId}/telemetry`,
    { headers: { Accept: "application/json" }, signal }
  );

  if (!response.ok) {
    const body = await response.json().catch(() => null);
    throw new Error(
      body?.message || body?.error ||
      `Could not load telemetry history (HTTP ${response.status}).`
    );
  }

  const records = await response.json();

  if (!Array.isArray(records)) {
    throw new Error("The telemetry API returned an unexpected response.");
  }

  return records;
}
