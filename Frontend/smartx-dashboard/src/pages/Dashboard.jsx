import { useEffect, useState } from "react";
import SensorForm from "../components/SensorForm";
import SensorTable from "../components/SensorTable";
import { getSensors } from "../api/sensorApi";
import { deleteSensor } from "../api/sensorApi";

function Dashboard() {

    const [sensors, setSensors] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState("");
    const [searchTerm, setSearchTerm] = useState("");
    const [categoryFilter, setCategoryFilter] = useState("All");
    const [editingSensor, setEditingSensor] = useState(null);

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

    const handleDelete = async (id) => {

    const confirmed = window.confirm(
        "Are you sure you want to delete this sensor?"
    );

    if (!confirmed) {
        return;
    }

    try {

        await deleteSensor(id);

        await loadSensors();

    } catch (error) {

        console.error(error);

        setError("Unable to delete sensor.");

        }
    };

    //Search function
    const filteredSensors = sensors.filter((sensor) => {

    const matchesSearch =
        sensor.macAddress
            .toLowerCase()
            .includes(searchTerm.toLowerCase()) ||
        sensor.location
            .toLowerCase()
            .includes(searchTerm.toLowerCase());

    const matchesCategory =
        categoryFilter === "All" ||
        sensor.category === categoryFilter;

        return matchesSearch && matchesCategory;
    });

    useEffect(() => {

        loadSensors();

    }, []);

    return (
        
        <div className="container">
            <div>

        <input
            type="text"
            placeholder="Search MAC address or location..."
            value={searchTerm}
            onChange={(event) =>
                setSearchTerm(event.target.value)
            }
        />

        <select
            value={categoryFilter}
            onChange={(event) =>
                setCategoryFilter(event.target.value)
            }
        >

            <option value="All">
                All Categories
            </option>

            <option value="Environmental">
                Environmental
            </option>

            <option value="Power Consumption">
                Power Consumption
            </option>

            <option value="Actuator">
                Actuator
            </option>

        </select>

    </div>

            <h1>Smart-X Dashboard</h1>

            <SensorForm onSensorCreated={loadSensors} />

            {loading && <p>Loading sensors...</p>}

            {error && <p>{error}</p>}

            {!loading && !error && (
                <SensorTable sensors={filteredSensors} onEdit={setEditingSensor} onDelete={handleDelete} />
            )}

        </div>
    );
}

export default Dashboard;