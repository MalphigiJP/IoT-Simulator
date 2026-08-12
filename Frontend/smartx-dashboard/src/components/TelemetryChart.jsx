import {
    Chart as ChartJS,
    CategoryScale,
    LinearScale,
    PointElement,
    LineElement,
    Title,
    Tooltip,
    Legend
} from "chart.js";

import { Line } from "react-chartjs-2";

ChartJS.register(
    CategoryScale,
    LinearScale,
    PointElement,
    LineElement,
    Title,
    Tooltip,
    Legend
);

function TelemetryChart({ telemetry }) {

    const orderedTelemetry = [...telemetry]
        .reverse();

    const data = {

        labels: orderedTelemetry.map(
            record =>
                new Date(record.timestamp)
                    .toLocaleTimeString()
        ),

        datasets: [

            {
                label: "Temperature",

                data: orderedTelemetry.map(
                    record =>
                        record.temperature
                ),

                tension: 0.3
            }

        ]
    };

    const options = {

        responsive: true,

        plugins: {

            legend: {
                position: "top"
            },

            title: {
                display: true,
                text: "Temperature Telemetry"
            }

        },

        scales: {

            y: {
                title: {
                    display: true,
                    text: "Temperature °C"
                }
            }

        }

    };

    return (
        <Line
            data={data}
            options={options}
        />
    );
}

export default TelemetryChart;