function SensorTable({
    sensors,
    onEdit,
    onDelete
}) {

    return (

        <div className="sensor-table">

            <h2>
                Registered Sensors
            </h2>


            {sensors.length === 0 ? (

                <p>
                    No sensors match your search.
                </p>

            ) : (

                <table border="1">

                    <thead>

                        <tr>

                            <th>
                                ID
                            </th>

                            <th>
                                MAC Address
                            </th>

                            <th>
                                Location
                            </th>

                            <th>
                                Category
                            </th>

                            <th>
                                Registered
                            </th>

                            <th>
                                Actions
                            </th>

                        </tr>

                    </thead>


                    <tbody>

                        {sensors.map((sensor) => (

                            <tr key={sensor.id}>

                                <td>
                                    {sensor.id}
                                </td>

                                <td>
                                    {sensor.macAddress}
                                </td>

                                <td>
                                    {sensor.location}
                                </td>

                                <td>
                                    {sensor.category}
                                </td>

                                <td>

                                    {sensor.registeredAt

                                        ? new Date(
                                            sensor.registeredAt
                                        ).toLocaleString()

                                        : "Unknown"

                                    }

                                </td>


                                <td>

                                    <button
                                        type="button"
                                        onClick={() =>
                                            onEdit(sensor)
                                        }
                                    >

                                        Edit

                                    </button>


                                    {" "}


                                    <button
                                        type="button"
                                        onClick={() =>
                                            onDelete(sensor.id)
                                        }
                                    >

                                        Delete

                                    </button>

                                </td>

                            </tr>

                        ))}

                    </tbody>

                </table>

            )}

        </div>

    );

}

export default SensorTable;