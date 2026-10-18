
import commandClient from "./commandApi";

export async function recordOperatorSearch({
  query,
  scope,
  sensorId = null,
  resultCount = null,
}) {
  const response = await commandClient.post(
    "/insights/searches",
    {
      query,
      scope,
      sensorId,
      resultCount,
    }
  );

  return response.data;
}

export async function getOperatorSearches({
  sensorId,
  scope,
  limit = 50,
} = {}) {
  const response = await commandClient.get(
    "/insights/searches",
    {
      params: {
        ...(sensorId != null ? { sensorId } : {}),
        ...(scope ? { scope } : {}),
        limit,
      },
    }
  );

  return response.data;
}

export async function getOperatorInteractions({
  sensorId,
  limit = 50,
} = {}) {
  const response = await commandClient.get(
    "/insights/interactions",
    {
      params: {
        ...(sensorId != null ? { sensorId } : {}),
        limit,
      },
    }
  );

  return response.data;
}


export async function getRankedRecommendations({
  days = 7,
  windowMinutes = 30,
  sensorId = null,
  signal,
} = {}) {
  const response = await commandClient.get(
    "/insights/recommendations",
    {
      params: {
        days,
        windowMinutes,
        ...(sensorId != null ? { sensorId } : {}),
      },
      signal,
    }
  );

  return response.data;
}
