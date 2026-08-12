function TelemetryOverview({
    telemetry,
    sensorCount
}) {

    const anomalyCount = telemetry.filter(
        (record) =>
            (record.temperature !== null &&
                record.temperature > 50) ||

            (record.soilMoisture !== null &&
                record.soilMoisture < 10) ||

            (record.wattage !== null &&
                record.wattage > 6000)
    ).length;

    return (
        <div>

            <div>
                <h3>Sensors</h3>
                <p>{sensorCount}</p>
            </div>

            <div>
                <h3>Telemetry Records</h3>
                <p>{telemetry.length}</p>
            </div>

            <div>
                <h3>Anomalies</h3>
                <p>{anomalyCount}</p>
            </div>

        </div>
    );
}

export default TelemetryOverview;