import { useEffect, useState } from "react";
import SensorForm from "../components/SensorForm";
import SensorTable from "../components/SensorTable";
import { getSensors } from "../api/sensorApi";

function Dashboard() {

    const [sensors, setSensors] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState("");

    const loadSensors = async () => {

        try {

            setLoading(true);
            setError("");

            const data = await getSensors();

            setSensors(data);

        } catch (error) {

            console.error(error);

            setError("Unable to load sensors.");

        } finally {

            setLoading(false);

        }

    };

    useEffect(() => {

        loadSensors();

    }, []);

    return (
        <div className="container">

            <h1>Smart-X Dashboard</h1>

            <SensorForm onSensorCreated={loadSensors} />

            {loading && <p>Loading sensors...</p>}

            {error && <p>{error}</p>}

            {!loading && !error && (
                <SensorTable sensors={sensors} />
            )}

        </div>
    );
}

export default Dashboard;