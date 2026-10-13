import api from "./telemetryApi";

/**
 * Request mock telemetry from the ASP.NET Core simulation endpoint.
 * Generation and database persistence happen on the backend, not in React.
 *
 * Requires: POST /api/telemetry/simulate
 */
export async function simulateTelemetry({
  recordCount = 1000,
  batchSize = 500,
  anomalyRate = 0.02,
  liveMode = true,
} = {}) {
  if (!Number.isInteger(recordCount) || recordCount < 1) {
    throw new Error("Record count must be a positive whole number.");
  }
  if (!Number.isInteger(batchSize) || batchSize < 1) {
    throw new Error("Batch size must be a positive whole number.");
  }
  if (!Number.isFinite(anomalyRate) || anomalyRate < 0 || anomalyRate > 1) {
    throw new Error("Anomaly rate must be a number between 0 and 1.");
  }

  const response = await api.post("/telemetry/simulate", {
    recordCount,
    batchSize,
    anomalyRate,
    liveMode,
  });

  return response.data;
}

export default simulateTelemetry;
