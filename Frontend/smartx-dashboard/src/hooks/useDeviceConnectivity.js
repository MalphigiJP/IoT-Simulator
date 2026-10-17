
import { useEffect, useState } from "react";
import {
  getLiveCommandDevices,
  getCommandErrorMessage,
} from "../api/commandApi";

const REFRESH_INTERVAL = 10000;

export function useDeviceConnectivity(refreshVersion) {
  const [devicesBySensor, setDevicesBySensor] = useState({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [lastChecked, setLastChecked] = useState(null);

  useEffect(() => {
    const controller = new AbortController();
    let active = true;
    let inFlight = false;

    async function loadConnectivity() {
      if (inFlight) {
        return;
      }

      inFlight = true;

      try {
        const devices = await getLiveCommandDevices(
          controller.signal
        );

        if (!active) {
          return;
        }

        const next = {};

        for (const device of devices) {
          const sensorId = Number(
            device.sensorId ?? device.id
          );

          if (
            Number.isInteger(sensorId) &&
            sensorId > 0
          ) {
            next[sensorId] = device;
          }
        }

        setDevicesBySensor(next);
        setError("");
        setLastChecked(new Date());
      } catch (requestError) {
        if (active && !controller.signal.aborted) {
          setDevicesBySensor({});
          setError(
            getCommandErrorMessage(requestError)
          );
          setLastChecked(null);
        }
      } finally {
        if (active) {
          setLoading(false);
        }

        inFlight = false;
      }
    }

    loadConnectivity();

    const interval = setInterval(
      loadConnectivity,
      REFRESH_INTERVAL
    );

    return () => {
      active = false;
      clearInterval(interval);
      controller.abort();
    };
  }, [refreshVersion]);

  return {
    devicesBySensor,
    loading,
    error,
    lastChecked,
  };
}
