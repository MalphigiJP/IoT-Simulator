import { useState } from "react";
import { createSensor } from "../api/sensorApi";

function SensorForm({ onSensorCreated }) {

    const [macAddress, setMacAddress] = useState("");
    const [location, setLocation] = useState("");
    const [category, setCategory] = useState("Environmental");

    const [error, setError] = useState("");
    const [success, setSuccess] = useState("");
    const [submitting, setSubmitting] = useState(false);

    const handleSubmit = async (event) => {

        event.preventDefault();

        setError("");
        setSuccess("");

        if (!macAddress || !location) {

            setError("MAC address and location are required.");

            return;
        }

        try {

            setSubmitting(true);

            await createSensor({
                macAddress,
                location,
                category
            });

            setMacAddress("");
            setLocation("");
            setCategory("Environmental");

            setSuccess("Sensor registered successfully!");

            await onSensorCreated();

        } catch (error) {

            console.error(error);

            setError("Unable to register sensor.");

        } finally {

            setSubmitting(false);

        }
    };

    return (
        <div>

            <h2>Register Sensor</h2>

            <form onSubmit={handleSubmit}>

                <div>

                    <label>
                        MAC Address
                    </label>

                    <br />

                    <input
                        type="text"
                        placeholder="AA:BB:CC:DD:EE:FF"
                        value={macAddress}
                        onChange={(event) =>
                            setMacAddress(event.target.value)
                        }
                    />

                </div>

                <br />

                <div>

                    <label>
                        Deployment Location
                    </label>

                    <br />

                    <input
                        type="text"
                        placeholder="Greenhouse A"
                        value={location}
                        onChange={(event) =>
                            setLocation(event.target.value)
                        }
                    />

                </div>

                <br />

                <div>

                    <label>
                        Sensor Category
                    </label>

                    <br />

                    <select
                        value={category}
                        onChange={(event) =>
                            setCategory(event.target.value)
                        }
                    >

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

                <br />

                <button
                    type="submit"
                    disabled={submitting}
                >

                    {submitting
                        ? "Registering..."
                        : "Register Sensor"}

                </button>

            </form>

            {error && (
                <p>{error}</p>
            )}

            {success && (
                <p>{success}</p>
            )}

        </div>
    );
}

export default SensorForm;