
import axios from "axios";

const api = axios.create({
    baseURL:
        import.meta.env.VITE_API_URL ||
        "http://localhost:5001/api",

    timeout: 15000
});

// GET all sensors
export const getSensors = async () => {
    const response = await api.get("/sensors");
    return response.data;
};

// GET single sensor
export const getSensorById = async (id) => {
    const response = await api.get(`/sensors/${id}`);
    return response.data;
};

// POST new sensor
export const createSensor = async (sensor) => {
    const response = await api.post("/sensors", sensor);
    return response.data;
};

// PUT update sensor
export const updateSensor = async (id, sensor) => {
    const response = await api.put(`/sensors/${id}`, sensor);
    return response.data;
};

// DELETE sensor
export const deleteSensor = async (id) => {
    await api.delete(`/sensors/${id}`);
};

// GET sensor health
export const getSensorHealth = async () => {
    const response = await api.get("/sensors/health");
    return response.data;
};

export default api;
