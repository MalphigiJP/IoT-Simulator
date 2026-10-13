import axios from "axios";

const api = axios.create({
    baseURL:
        import.meta.env.VITE_API_URL ||
        "http://localhost:5001/api",
    timeout: 15000
});

// Retrieve all registered sensors
export const getSensors = async () => {
    const response = await api.get("/sensors");
    return response.data;
};

// Retrieve a specific sensor
export const getSensorById = async (id) => {
    const response = await api.get(`/sensors/${id}`);
    return response.data;
};

// Register a sensor
export const createSensor = async (sensor) => {
    const response = await api.post("/sensors", sensor);
    return response.data;
};

// Update a sensor
export const updateSensor = async (id, sensor) => {
    const response = await api.put(`/sensors/${id}`, sensor);
    return response.data;
};

// Delete a sensor
export const deleteSensor = async (id) => {
    await api.delete(`/sensors/${id}`);
};

// Retrieve connectivity and health status
export const getSensorHealth = async () => {
    const response = await api.get("/sensors/health");
    return response.data;
};

export default api;
