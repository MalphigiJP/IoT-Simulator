import { useEffect, useState } from "react";
import {
    createSensor,
    updateSensor
} from "../api/sensorApi";

function SensorForm({
    onSensorCreated,
    editingSensor,
    onSensorUpdated,
    onCancelEdit
}) {

    const [macAddress, setMacAddress] = useState("");
    const [location, setLocation] = useState("");
    const [category, setCategory] = useState("Environmental");

    const [error, setError] = useState("");
    const [success, setSuccess] = useState("");
    const [submitting, setSubmitting] = useState(false);


    /*
     * When editingSensor changes,
     * populate the form with the selected sensor.
     */
    useEffect(() => {

        if (editingSensor) {

            setMacAddress(editingSensor.macAddress);
            setLocation(editingSensor.location);
            setCategory(editingSensor.category);

            setError("");
            setSuccess("");

        } else {

            resetForm();

        }

    }, [editingSensor]);


    /*
     * Reset the form back to its default state.
     */
    const resetForm = () => {

        setMacAddress("");
        setLocation("");
        setCategory("Environmental");

        setError("");
        setSuccess("");
    };


    /*
     * Handle form submission.
     */
    const handleSubmit = async (event) => {

        event.preventDefault();

        setError("");
        setSuccess("");


        /*
         * Basic frontend validation.
         */
        if (!macAddress.trim()) {

            setError("MAC address is required.");

            return;
        }

        if (!location.trim()) {

            setError("Deployment location is required.");

            return;
        }


        try {

            setSubmitting(true);


            /*
             * EDIT EXISTING SENSOR
             */
            if (editingSensor) {

                await updateSensor(
                    editingSensor.id,
                    {
                        macAddress: macAddress.trim(),
                        location: location.trim(),
                        category: category
                    }
                );

                setSuccess(
                    "Sensor updated successfully!"
                );


                /*
                 * Tell Dashboard to reload sensors.
                 */
                if (onSensorUpdated) {
                    await onSensorUpdated();
                }

            }


            /*
             * CREATE NEW SENSOR
             */
            else {

                await createSensor(
                    {
                        macAddress: macAddress.trim(),
                        location: location.trim(),
                        category: category
                    }
                );

                setSuccess(
                    "Sensor registered successfully!"
                );


                /*
                 * Tell Dashboard to reload sensors.
                 */
                if (onSensorCreated) {
                    await onSensorCreated();
                }

                resetForm();

            }

        } catch (error) {

            console.error(
                "Sensor operation failed:",
                error
            );


            /*
             * Try to display an error returned
             * by the ASP.NET API.
             */
            if (error.response?.data?.message) {

                setError(
                    error.response.data.message
                );

            } else {

                setError(
                    editingSensor
                        ? "Unable to update sensor."
                        : "Unable to register sensor."
                );

            }

        } finally {

            setSubmitting(false);

        }
    };


    /*
     * Cancel editing.
     */
    const handleCancel = () => {

        resetForm();

        if (onCancelEdit) {
            onCancelEdit();
        }

    };


    const isEditing = Boolean(editingSensor);


    return (

        <div className="sensor-form">

            <h2>
                {isEditing
                    ? "Edit Sensor"
                    : "Register Sensor"}
            </h2>


            <form onSubmit={handleSubmit}>

                {/* MAC ADDRESS */}

                <div>

                    <label htmlFor="macAddress">
                        MAC Address
                    </label>

                    <br />

                    <input
                        id="macAddress"
                        type="text"
                        placeholder="AA:BB:CC:DD:EE:FF"
                        value={macAddress}
                        onChange={(event) =>
                            setMacAddress(
                                event.target.value
                            )
                        }
                        disabled={submitting}
                    />

                </div>


                <br />


                {/* LOCATION */}

                <div>

                    <label htmlFor="location">
                        Deployment Location
                    </label>

                    <br />

                    <input
                        id="location"
                        type="text"
                        placeholder="Greenhouse A"
                        value={location}
                        onChange={(event) =>
                            setLocation(
                                event.target.value
                            )
                        }
                        disabled={submitting}
                    />

                </div>


                <br />


                {/* CATEGORY */}

                <div>

                    <label htmlFor="category">
                        Sensor Category
                    </label>

                    <br />

                    <select
                        id="category"
                        value={category}
                        onChange={(event) =>
                            setCategory(
                                event.target.value
                            )
                        }
                        disabled={submitting}
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


                {/* ERROR MESSAGE */}

                {error && (

                    <p>
                        {error}
                    </p>

                )}


                {/* SUCCESS MESSAGE */}

                {success && (

                    <p>
                        {success}
                    </p>

                )}


                {/* BUTTONS */}

                <button
                    type="submit"
                    disabled={submitting}
                >

                    {submitting

                        ? (
                            isEditing
                                ? "Updating..."
                                : "Registering..."
                        )

                        : (
                            isEditing
                                ? "Update Sensor"
                                : "Register Sensor"
                        )

                    }

                </button>


                {/* CANCEL BUTTON */}

                {isEditing && (

                    <button
                        type="button"
                        onClick={handleCancel}
                        disabled={submitting}
                    >

                        Cancel

                    </button>

                )}

            </form>

        </div>

    );

}

export default SensorForm;