
import axios from "axios";

const baseURL = (
  import.meta.env.VITE_API_URL ||
  "http://localhost:5000/api"
).replace(/\/+$/, "");

const commandClient = axios.create({
  baseURL,
  timeout: 15000,
  headers: {
    "Content-Type": "application/json",
  },
});

const unwrapArray = (data) => {
  if (Array.isArray(data)) {
    return data;
  }

  if (Array.isArray(data?.items)) {
    return data.items;
  }

  if (Array.isArray(data?.sensors)) {
    return data.sensors;
  }

  return [];
};

export async function getCommandSensors(signal) {
  const response = await commandClient.get("/sensors", {
    signal,
  });

  return unwrapArray(response.data);
}

export async function getDeviceControl(
  sensorId,
  signal
) {
  const response = await commandClient.get(
    `/devices/${sensorId}/control`,
    { signal }
  );

  return response.data;
}

export async function getDeviceCommands(
  sensorId,
  signal
) {
  const response = await commandClient.get(
    `/devices/${sensorId}/commands`,
    { signal }
  );

  return unwrapArray(response.data);
}

export async function getCommandById(
  commandId,
  signal
) {
  const response = await commandClient.get(
    `/commands/${commandId}`,
    { signal }
  );

  return response.data;
}

export async function getUndoAvailability(
  sensorId,
  signal
) {
  const response = await commandClient.get(
    `/devices/${sensorId}/commands/undo/status`,
    { signal }
  );

  return response.data;
}

export async function getRecentCommandEvents(
  { sensorId, limit = 20 } = {},
  signal
) {
  const response = await commandClient.get(
    "/commands/audit",
    {
      params: {
        ...(sensorId != null ? { sensorId } : {}),
        limit,
      },
      signal,
    }
  );

  return unwrapArray(response.data);
}

export async function createDeviceCommand(
  sensorId,
  target,
  state
) {
  const response = await commandClient.post(
    `/devices/${sensorId}/commands`,
    {
      target,
      state,
    }
  );

  return response.data;
}

export async function cancelDeviceCommand(
  commandId
) {
  const response = await commandClient.post(
    `/commands/${commandId}/cancel`
  );

  return response.data;
}

export async function undoDeviceCommand(
  sensorId
) {
  const response = await commandClient.post(
    `/devices/${sensorId}/commands/undo`
  );

  return response.data;
}

export function getCommandErrorMessage(error) {
  if (axios.isCancel(error)) {
    return "Request cancelled.";
  }

  const serverError = error?.response?.data?.error;

  if (typeof serverError === "string") {
    return serverError;
  }

  if (!error?.response) {
    return "Unable to connect to the Smart-X API.";
  }

  return (
    error.response.data?.title ||
    `Request failed with HTTP ${error.response.status}.`
  );
}

export default commandClient;
