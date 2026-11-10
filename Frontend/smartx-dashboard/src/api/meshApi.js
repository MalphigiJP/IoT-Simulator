
const configuredBase = (
  import.meta.env.VITE_API_URL || "http://localhost:5000/api"
).replace(/\/+$/, "");

const API_BASE = /\/api$/i.test(configuredBase)
  ? configuredBase
  : `${configuredBase}/api`;

async function getJson(path, signal) {
  const response = await fetch(`${API_BASE}${path}`, {
    method: "GET",
    headers: { Accept: "application/json" },
    signal,
  });

  if (!response.ok) {
    const body = await response.json().catch(() => null);
    throw new Error(
      body?.error ||
        body?.message ||
        `Mesh API request failed (HTTP ${response.status}).`
    );
  }

  return response.json();
}

export function getMeshPreview(signal) {
  return getJson("/mesh/preview", signal);
}

export function getMeshMinimumSpanningTree(signal) {
  return getJson("/mesh/mst", signal);
}

export function getMeshRoute(sensorId, signal) {
  return getJson(`/mesh/route/${encodeURIComponent(sensorId)}`, signal);
}

export function getMeshNodeById(sensorId, signal) {
  return getJson(`/mesh/nodes/${encodeURIComponent(sensorId)}`, signal);
}

export function getMeshNodeByMac(macAddress, signal) {
  const query = new URLSearchParams({ macAddress });
  return getJson(`/mesh/nodes/by-mac?${query.toString()}`, signal);
}
