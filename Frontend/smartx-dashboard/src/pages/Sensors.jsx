import { useCallback, useEffect, useState } from "react";

import Navbar from "../components/Navbar";
import SensorForm from "../components/SensorForm";
import SensorList from "../components/SensorList";

import {
    getSensors,
    deleteSensor
} from "../api/sensorApi";

import { useAppState } from "../context/AppStateContext";

function Sensors() {

    const [sensors, setSensors] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState("");
    const [message, setMessage] = useState("");

    // Editing selection is preserved across navigation.
    const {
        editingSensorId,
        setEditingSensorId,

        registrySearch,
        setRegistrySearch,

        registryCategoryFilter,
        setRegistryCategoryFilter,

        registryPage,
        setRegistryPage
    } = useAppState();

    const loadSensors = useCallback(async () => {

        try {
            setLoading(true);
            setError("");

            const data = await getSensors();

            if (!Array.isArray(data)) {
                throw new Error(
                    "Unexpected sensor response."
                );
            }

            setSensors(data);

        } catch (err) {

            console.error(
                "Unable to load sensors:",
                err
            );

            setError(
                "Unable to retrieve registered sensors."
            );

        } finally {
            setLoading(false);
        }

    }, []);

    useEffect(() => {
        loadSensors();
    }, [loadSensors]);

    // Resolve the selected ID against freshly loaded data.
    const editingSensor = sensors.find(
        (sensor) => sensor.id === editingSensorId
    ) ?? null;

    const handleSensorCreated = async () => {

        await loadSensors();

        setEditingSensorId(null);

        setMessage(
            "Sensor registered successfully."
        );
    };

    const handleSensorUpdated = async () => {

        await loadSensors();

        setEditingSensorId(null);

        setMessage(
            "Sensor updated successfully."
        );
    };

    const handleEdit = (sensor) => {

        setEditingSensorId(sensor.id);

        setMessage("");
        setError("");

        window.scrollTo({
            top: 0,
            behavior: "smooth"
        });
    };

    const handleCancelEdit = () => {
        setEditingSensorId(null);
        setMessage("");
    };

    const handleDelete = async (id) => {

        const confirmed = window.confirm(
            "Delete this sensor? Associated telemetry may also be deleted."
        );

        if (!confirmed) {
            return;
        }

        try {

            setError("");
            setMessage("");

            await deleteSensor(id);

            if (editingSensorId === id) {
                setEditingSensorId(null);
            }

            await loadSensors();

            setMessage(
                "Sensor deleted successfully."
            );

        } catch (err) {

            console.error(
                "Unable to delete sensor:",
                err
            );

            setError(
                "Unable to delete the selected sensor."
            );
        }
    };

    return (
        <div className="sx-layout">

            <Navbar variant="top" />

            <main className="sx-sensors-page">

                <header className="sx-page-header">
                    <div>

                        <p className="sx-eyebrow">
                            SMART-X / DEVICE MANAGEMENT
                        </p>

                        <h1>Sensor Registry</h1>

                        <p>
                            Register, configure and manage
                            IoT devices across Smart-X
                            deployments.
                        </p>

                    </div>

                    <div className="sx-sensor-count">
                        <strong>{sensors.length}</strong>
                        <span>Registered sensors</span>
                    </div>
                </header>

                {error && (
                    <div
                        role="alert"
                        className="sx-error"
                    >
                        {error}
                    </div>
                )}

                {message && (
                    <div
                        role="status"
                        className="sx-success"
                    >
                        {message}
                    </div>
                )}

                <section className="sx-panel">

                    <div className="sx-section-header">
                        <h2>
                            {editingSensorId !== null
                                ? "Edit Sensor"
                                : "Register Sensor"}
                        </h2>

                        {editingSensorId !== null && (
                            <button
                                type="button"
                                onClick={handleCancelEdit}
                            >
                                Cancel Editing
                            </button>
                        )}
                    </div>

                    {!loading && (
                        <SensorForm
                            key={
                                editingSensor
                                    ? editingSensor.id
                                    : "new"
                            }
                            editingSensor={editingSensor}
                            onSensorCreated={
                                handleSensorCreated
                            }
                            onSensorUpdated={
                                handleSensorUpdated
                            }
                        />
                    )}

                </section>

                <section className="sx-panel">

                    <div className="sx-section-header">
                        <div>
                            <h2>Registered Sensors</h2>

                            <p>
                                Search, filter and maintain
                                your connected IoT devices.
                            </p>
                        </div>

                        <button
                            type="button"
                            onClick={loadSensors}
                            disabled={loading}
                        >
                            {loading
                                ? "Refreshing..."
                                : "Refresh Sensors"}
                        </button>
                    </div>

                    {loading ? (
                        <p>Loading sensors...</p>
                    ) : (
                        <SensorList
                            sensors={sensors}

                            onEdit={handleEdit}
                            onDelete={handleDelete}

                            search={registrySearch}
                            onSearchChange={
                                setRegistrySearch
                            }

                            categoryFilter={
                                registryCategoryFilter
                            }
                            onCategoryFilterChange={
                                setRegistryCategoryFilter
                            }

                            page={registryPage}
                            onPageChange={
                                setRegistryPage
                            }
                        />
                    )}

                </section>

            </main>

        </div>
    );
}

export default Sensors;
