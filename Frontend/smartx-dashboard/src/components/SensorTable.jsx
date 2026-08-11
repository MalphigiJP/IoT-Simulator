function SensorTable({ sensors }) {

    return (
        <div>

            <h2>Registered Sensors</h2>

            {sensors.length === 0 ? (

                <p>No sensors have been registered yet.</p>

            ) : (

                <table border="1">

                    <thead>

                        <tr>
                            <th>ID</th>
                            <th>MAC Address</th>
                            <th>Location</th>
                            <th>Category</th>
                            <th>Registered</th>
                        </tr>

                    </thead>

                    <tbody>

                        {sensors.map((sensor) => (

                            <tr key={sensor.id}>

                                <td>{sensor.id}</td>

                                <td>{sensor.macAddress}</td>

                                <td>{sensor.location}</td>

                                <td>{sensor.category}</td>

                                <td>
                                    {new Date(
                                        sensor.registeredAt
                                    ).toLocaleString()}
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