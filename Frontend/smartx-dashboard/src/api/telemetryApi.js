
import axios from "axios";


// SMART-X TELEMETRY API CONFIGURATION
const api = axios.create({
    baseURL:
        import.meta.env.VITE_API_URL ||
        "https://localhost:7057/api",

    headers: {
        "Content-Type": "application/json"
    },

    timeout: 15000
});

// GET LATEST TELEMETRY RECORDS
export const getTelemetry = async () => {
    const response = await api.get("/telemetry");
    return response.data;
};

// GET TELEMETRY FOR A SPECIFIC SENSOR
export const getSensorTelemetry = async (sensorId) => {
    const response = await api.get(
        `/sensors/${sensorId}/telemetry`
    );

    return response.data;
};

// POST A SINGLE TELEMETRY RECORD
export const createTelemetry = async (telemetry) => {
    const response = await api.post(
        "/telemetry",
        telemetry
    );

    return response.data;
};


// POST MULTIPLE TELEMETRY RECORDS
export const createTelemetryBatch = async (records) => {
    const response = await api.post(
        "/telemetry/batch",
        records
    );

    return response.data;
};

// GENERATE SIMULATED TELEMETRY
export const simulateTelemetry = async ({
    recordCount = 1000,
    batchSize = 500,
    anomalyRate = 0.02,
    liveMode = true
} = {}) => {
    const response = await api.post(
        "/telemetry/simulate",
        {
            recordCount,
            batchSize,
            anomalyRate,
            liveMode
        },
        {
            // Large simulations may need more time.
            timeout: 120000
        }
    );

    return response.data;
};

// Default export for additional API operations.
export default api;
