
import { useEffect, useState } from "react";

import Navbar from "../components/Navbar";
import SensorForm from "../components/SensorForm";
import SensorList from "../components/SensorList";

import {
    getSensors,
    deleteSensor
} from "../api/sensorApi";

function Sensors() {
    const [sensors, setSensors] = useState([]);
    const [editingSensor, setEditingSensor] = useState(null);

    const [loading, setLoading] = useState(true);
    const [error, setError] = useState("");
    const [message, setMessage] = useState("");

    const loadSensors = async () => {
        try {
            setError("");

            const data = await getSensors();
            setSensors(data);
        } catch (err) {
            console.error("Unable to fetch sensors:", err);
            setError("Unable to load registered sensors.");
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        loadSensors();
    }, []);

    const handleSensorCreated = async () => {
        await loadSensors();
        setMessage("Sensor registered successfully.");
    };

    const handleSensorUpdated = async () => {
        await loadSensors();
        setEditingSensor(null);
        setMessage("Sensor updated successfully.");
    };

    const handleEdit = (sensor) => {
        setEditingSensor(sensor);
        setMessage("");
        window.scrollTo({ top: 0, behavior: "smooth" });
    };

    const handleDelete = async (id) => {
        const confirmed = window.confirm(
            "Delete this sensor? Its associated telemetry may also be deleted."
        );

        if (!confirmed) return;

        try {
            setError("");
            setMessage("");

            await deleteSensor(id);

            if (editingSensor?.id === id) {
                setEditingSensor(null);
            }

            await loadSensors();
            setMessage("Sensor deleted successfully.");
        } catch (err) {
            console.error("Unable to delete sensor:", err);
            setError("Unable to delete the selected sensor.");
        }
    };

    return (
        <div className="sx-layout">
            <Navbar variant="top" />

            <main className="sx-sensors-page">
                <header className="sx-page-header">
                    <div>
                        <p className="sx-eyebrow">
                            Smart-X / Device Management
                        </p>
                        <h1>Sensor Registry</h1>
                        <p>
                            Register, configure and manage
                            IoT sensors across your deployments.
                        </p>
                    </div>

                    <div className="sx-sensor-count">
                        <strong>{sensors.length}</strong>
                        <span>Registered sensors</span>
                    </div>
                </header>

                {error && (
                    <div role="alert" className="sx-error">
                        {error}
                    </div>
                )}

                {message && (
                    <div role="status" className="sx-success">
                        {message}
                    </div>
                )}

                <section className="sx-panel">
                    <SensorForm
                        editingSensor={editingSensor}
                        onSensorCreated={handleSensorCreated}
                        onSensorUpdated={handleSensorUpdated}
                    />
                </section>

                <section className="sx-panel">
                    <div className="sx-section-header">
                        <div>
                            <h2>Registered Sensors</h2>
                            <p>
                                View, search and maintain
                                your connected devices.
                            </p>
                        </div>

                        <button
                            type="button"
                            onClick={loadSensors}
                            disabled={loading}
                        >
                            Refresh Sensors
                        </button>
                    </div>

                    {loading ? (
                        <p>Loading sensors...</p>
                    ) : (
                        <SensorList
                            sensors={sensors}
                            onEdit={handleEdit}
                            onDelete={handleDelete}
                        />
                    )}
                </section>
            </main>
        </div>
    );
}

export default Sensors;
