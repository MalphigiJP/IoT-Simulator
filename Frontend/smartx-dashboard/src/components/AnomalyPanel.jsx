function AnomalyPanel({ telemetry }) {

    const anomalies = telemetry.filter(
        (record) =>
            (record.temperature !== null &&
                record.temperature > 50) ||

            (record.soilMoisture !== null &&
                record.soilMoisture < 10) ||

            (record.wattage !== null &&
                record.wattage > 6000)
    );

    return (
        <div>

            <h2>🚨 Anomaly Spotlight</h2>

            {anomalies.length === 0 ? (

                <p>
                    No anomalies detected.
                </p>

            ) : (

                anomalies
                    .slice(0, 10)
                    .map((record) => {

                        const temperatureAnomaly =
                            record.temperature !== null &&
                            record.temperature > 50;

                        const moistureAnomaly =
                            record.soilMoisture !== null &&
                            record.soilMoisture < 10;

                        const powerAnomaly =
                            record.wattage !== null &&
                            record.wattage > 6000;

                        return (
                            <div
                                key={record.id}
                            >

                                <h3>
                                    Sensor {record.sensorId}
                                </h3>

                                {temperatureAnomaly && (
                                    <p>
                                        Temperature:{" "}
                                        {record.temperature.toFixed(1)}
                                        °C
                                    </p>
                                )}

                                {moistureAnomaly && (
                                    <p>
                                        Soil Moisture:{" "}
                                        {record.soilMoisture.toFixed(1)}
                                        %
                                    </p>
                                )}

                                {powerAnomaly && (
                                    <p>
                                        Power:{" "}
                                        {record.wattage}
                                        W
                                    </p>
                                )}

                                <small>
                                    {new Date(
                                        record.timestamp
                                    ).toLocaleString()}
                                </small>

                            </div>
                        );
                    })

            )}

        </div>
    );
}

export default AnomalyPanel;