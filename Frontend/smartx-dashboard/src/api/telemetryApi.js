import axios from "axios";

const api = axios.create({
    baseURL: "http://localhost:5001/api"
});

export const getTelemetry = async () => {
    const response = await api.get("/telemetry");
    return response.data;
};

export const getSensorTelemetry = async (sensorId) => {
    const response = await api.get(
        `/sensors/${sensorId}/telemetry`
    );

    return response.data;
};

export default api;