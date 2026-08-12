import { useEffect, useState } from "react";

import { getTelemetry } from "../api/telemetryApi";
import { getSensors } from "../api/sensorApi";

import TelemetryOverview
    from "../components/TelemetryOverview";

import TelemetryChart
    from "../components/TelemetryChart";

import AnomalyPanel
    from "../components/AnomalyPanel";

function TelemetryDashboard() {

    const [telemetry, setTelemetry] = useState([]);
    const [sensors, setSensors] = useState([]);

    const [loading, setLoading] = useState(true);

    const [error, setError] = useState("");

    const loadDashboard = async () => {

        try {

            setError("");

            const [
                telemetryData,
                sensorData
            ] = await Promise.all([

                getTelemetry(),

                getSensors()

            ]);

            setTelemetry(telemetryData);

            setSensors(sensorData);

        } catch (error) {

            console.error(error);

            setError(
                "Unable to load telemetry dashboard."
            );

        } finally {

            setLoading(false);

        }
    };

    useEffect(() => {

        loadDashboard();

        const interval = setInterval(() => {

            loadDashboard();

        }, 5000);

        return () => {
            clearInterval(interval);
        };

    }, []);

    if (loading) {
        return <p>Loading Smart-X telemetry...</p>;
    }

    if (error) {
        return <p>{error}</p>;
    }

    return (

        <div>

            <h1>Smart-X Telemetry</h1>

            <TelemetryOverview
                telemetry={telemetry}
                sensorCount={sensors.length}
            />

            <TelemetryChart
                telemetry={telemetry}
            />

            <AnomalyPanel
                telemetry={telemetry}
            />

        </div>

    );
}

export default TelemetryDashboard;