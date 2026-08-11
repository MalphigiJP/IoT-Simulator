import axios from "axios";

const api = axios.create({
    baseURL: "http://localhost:5001/api"
});

export const getSensors = async () => {
    const response = await api.get("/sensors");
    return response.data;
};

export const createSensor = async (sensor) => {
    const response = await api.post("/sensors", sensor);
    return response.data;
};

export const updateSensor = async (id, sensor) => {
    const response = await api.put(`/sensors/${id}`, sensor);
    return response.data;
};

export const deleteSensor = async (id) => {
    await api.delete(`/sensors/${id}`);
};

export default api;