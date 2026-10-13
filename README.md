# Smart-X | IoT Telemetry Gateway & Operations Dashboard

Smart-X is a **web-based IoT monitoring prototype** that registers connected devices, ingests sensor telemetry, simulates high-volume device activity, and presents operational data in an interactive dashboard. It was developed for a **PROG7312 academic project** focused on IoT data ingestion, validation, user engagement, and troubleshooting.

The system models deployments such as **hydroponic farms**, **smart buildings**, and **energy monitoring installations**, where operators need to identify unusual readings and sensors that have stopped reporting.

> **Project status:** Working academic prototype under active development. Features described as *planned* below are not yet part of the core implementation. Smart-X is not a certified industrial monitoring or safety-control system.

## Features

- **Sensor registry:** register, list, inspect, edit, and delete sensors with MAC address, deployment location, and category.
- **Telemetry ingestion:** accept individual readings or batches of readings through REST endpoints.
- **Mixed telemetry types:** support floating-point temperature and soil-moisture measurements, integer wattage, and Boolean valve state.
- **Telemetry simulation:** generate configurable batches of mock readings, including abnormal values for testing.
- **Operations dashboard:** visualise recent readings, switch chart metrics, and inspect individual devices.
- **Sensor health:** classify devices as **Online**, **Delayed**, or **Offline** using the timestamp of their latest reading.
- **Troubleshooting tools:** highlight demonstration threshold breaches, inspect sensor history, and filter devices by health status, location, or category.
- **Guided interaction:** dashboard setup checklist, visible refresh feedback, and a responsive interface.
- **Swagger/OpenAPI UI:** explore and test backend endpoints in development.

## Technology stack

| Layer | Technology |
| --- | --- |
| Frontend | React, Vite, JavaScript |
| Visualisations | Chart.js, react-chartjs-2 |
| HTTP client | Axios |
| Navigation | React Router |
| Backend | ASP.NET Core **.NET 10**, Minimal APIs, C# |
| Data access | Entity Framework Core |
| Database | Microsoft SQL Server / SQL Server LocalDB (according to local configuration) |
| API testing | Swagger UI |

## Architecture

```text
ESP32 devices / simulated sensor readings
                  |
                  v
        ASP.NET Core Minimal API
           /          |          \
     Sensor CRUD   Telemetry    Simulation
                   ingestion     service
           \          |          /
                  v
           EF Core / SQL Server
                  ^
                  |
          React + Vite dashboard
       - Operations Centre
       - Sensor Registry
       - Health and anomaly views
```

The frontend polls the API for updates (the Operations Centre currently uses a **10-second interval**). **SignalR/WebSocket push updates are a possible future enhancement**, not a current requirement for running the prototype.

## Repository layout

Depending on how the solution was cloned or reorganised, your frontend and backend may sit in slightly different folders. The key source locations are:

```text
smartx-project/
├── SmartX.Api/
│   ├── Data/
│   │   └── SmartXContext.cs
│   ├── DTOs/
│   ├── Endpoints/
│   │   └── SensorEndpoints.cs
│   ├── Models/
│   │   ├── Sensor.cs
│   │   └── TelemetryRecord.cs
│   ├── Services/
│   │   ├── SensorService.cs
│   │   └── TelemetrySeeder.cs
│   ├── Validation/
│   ├── Program.cs
│   └── appsettings.json
├── smartx-dashboard/             # React frontend (or frontend/smartx-dashboard/)
│   ├── src/
│   │   ├── api/
│   │   ├── components/
│   │   └── pages/
│   │       ├── TelemetryDashboard.jsx
│   │       └── Sensors.jsx
│   └── package.json
└── README.md
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Node.js and npm (a version supported by your installed Vite release)
- Microsoft SQL Server or SQL Server LocalDB
- Git
- Optional: SQL Server Management Studio or Azure Data Studio for inspecting data

## Getting started

### 1. Clone the repository

```bash
git clone https://github.com/YOUR_USERNAME/YOUR_REPOSITORY.git
cd YOUR_REPOSITORY
```

Replace the placeholder URL and folder name with your repository details.

### 2. Configure the backend database

From the `SmartX.Api` directory, configure the **`SmartXConnection`** connection string in `appsettings.json`, a local development settings file, or .NET user secrets.

For example, with **Windows SQL Server LocalDB**:

```json
{
  "ConnectionStrings": {
    "SmartXConnection": "Server=(localdb)\\MSSQLLocalDB;Database=SmartXDb;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

This is a **development-only example**. Do not commit production passwords or private connection strings. LocalDB is Windows-specific; on other operating systems, configure an accessible SQL Server instance.

### 3. Apply database migrations

```powershell
cd SmartX.Api
dotnet restore
dotnet ef database update
```

If the `dotnet ef` command is unavailable, install the EF Core CLI tool compatible with the project's EF Core version:

```powershell
dotnet tool install --global dotnet-ef
```

Existing EF Core migrations must be present in the project for `database update` to initialise its schema. If the project does not include migrations, create one from the current model first.

### 4. Start the API

```powershell
dotnet run
```

In the current local setup, the backend runs at **`http://localhost:5000`**. Always confirm the actual address printed by the terminal; it can differ by environment.

- API root: `http://localhost:5000/`
- Swagger UI (development): `http://localhost:5000/swagger`

### 5. Configure the frontend

Open a **second terminal**, navigate to the React folder (the one containing `package.json`), and install dependencies:

```powershell
npm install
```

Create a `.env` file alongside `package.json`:

```env
VITE_API_URL=http://localhost:5000/api
```

The API modules (`src/api/sensorApi.js` and `src/api/telemetryApi.js`) use this variable as their base URL. Vite variables prefixed with `VITE_` are accessible in the browser: **never store secrets in them**.

### 6. Run the frontend

```powershell
npm run dev
```

Open the URL shown by Vite, typically **`http://localhost:5173`**.

| Page | Example route |
| --- | --- |
| Operations Centre | `/telemetry` |
| Sensor Registry | `/sensors` |

The root route (`/`) may redirect to or display the Operations Centre depending on the project's current `App.jsx` configuration.

## API endpoints

### Sensors

| Method | Route | Description |
| --- | --- | --- |
| `GET` | `/api/sensors` | List registered sensors |
| `GET` | `/api/sensors/health` | Get current sensor health classifications |
| `GET` | `/api/sensors/{id}` | Get a sensor by ID |
| `POST` | `/api/sensors` | Register a sensor |
| `PUT` | `/api/sensors/{id}` | Update sensor information |
| `DELETE` | `/api/sensors/{id}` | Delete a sensor |
| `GET` | `/api/sensors/{id}/telemetry` | Get recent readings for a sensor |

### Telemetry

| Method | Route | Description |
| --- | --- | --- |
| `GET` | `/api/telemetry` | Get the latest 100 telemetry records |
| `POST` | `/api/telemetry` | Ingest a reading |
| `POST` | `/api/telemetry/batch` | Ingest multiple readings |
| `POST` | `/api/telemetry/simulate` | Generate mock telemetry |

> Sensor routes are mapped through `SensorEndpoints.cs`; telemetry routes are currently mapped in `Program.cs`. Do not also register duplicate sensor CRUD routes in `Program.cs`, as this causes ASP.NET Core `AmbiguousMatchException` errors.

## Example requests

### Register a sensor

`POST /api/sensors`

```json
{
  "macAddress": "AA:BB:CC:DD:EE:FF",
  "location": "Greenhouse A",
  "category": "Environmental"
}
```

Supported prototype categories include `Environmental`, `Power Consumption`, and `Actuator`.

### Submit telemetry

`POST /api/telemetry`

```json
{
  "sensorId": 1,
  "soilMoisture": 62.7,
  "temperature": 24.8,
  "wattage": 1430,
  "valveOpen": true
}
```

The sensor must already be registered. `Timestamp` can be omitted; the API assigns the current UTC timestamp.

### Generate simulated telemetry

`POST /api/telemetry/simulate`

```json
{
  "recordCount": 1000,
  "batchSize": 500,
  "anomalyRate": 0.02,
  "liveMode": true
}
```

- `recordCount`: total readings requested.
- `batchSize`: how many readings are inserted per batch.
- `anomalyRate`: approximate probability of an abnormal reading (`0.02` means 2%).
- `liveMode`: generates readings with recent timestamps; **does not** start a continuous background simulator.

## Health monitoring and alerts

### Connection-health states

| Status | Prototype rule |
| --- | --- |
| **Online** | Last telemetry received within 30 seconds |
| **Delayed** | Last telemetry received more than 30 seconds and up to 120 seconds ago |
| **Offline** | No readings received or last reading older than 120 seconds |

### Demonstration anomaly thresholds

| Metric | Flagged when |
| --- | --- |
| Temperature | Above **50 °C** |
| Soil moisture | Below **10%** |
| Power consumption | Above **6000 W** |

These are **prototype rules used for visualisation and testing**, not calibrated safety limits. They should eventually be configurable by sensor type, deployment and operating context. Health classifications depend on telemetry timestamps, so historical simulation data can intentionally make a sensor appear offline.

## User engagement and dashboard design

The dashboard applies five engagement strategies relevant to technical IoT operations:

1. **Real-time-oriented telemetry visualisation:** readable charts, metrics and condition indicators.
2. **Progressive onboarding and contextual guidance:** setup checklist and inline troubleshooting help.
3. **Proactive alerts and notifications:** prominently displayed connectivity and anomaly events, with click-through investigation.
4. **Gamification and progress indicators:** visible configuration completion progress.
5. **User-centred interface and continuous feedback:** searching, filtering, clear loading/error states and responsive layouts.

Together, these features support a practical troubleshooting flow: **detect an issue → locate the affected sensor → examine readings and timestamps → decide on corrective action**.

## Testing checklist

- Register a sensor, then verify it appears in the registry.
- Update the sensor's location and confirm the new value persists.
- Submit a telemetry reading for the registered sensor.
- Generate a batch containing anomalies and confirm the dashboard highlights threshold breaches.
- Select an affected sensor and inspect its chart history.
- Stop supplying fresh telemetry and check the delayed/offline classification after the expected interval.
- Verify mobile and desktop layouts using browser developer tools.
- Confirm sensor endpoints appear **once** in Swagger and return the expected status codes.

## Troubleshooting

**Blank React page or missing export**  
Ensure `src/api/sensorApi.js` exports `getSensorHealth` and `src/api/telemetryApi.js` exports `getTelemetry` and `getSensorTelemetry`. Restart Vite after updating files.

**Frontend cannot reach the API**  
Check that `VITE_API_URL` matches the backend's actual address, that the API process is running, and that the development CORS policy permits the frontend origin.

**Ambiguous endpoint match**  
Remove duplicate `/api/sensors` mappings from `Program.cs`; keep `app.MapSensorEndpoints()` once.

**SQL Server connection timeout**  
Check the `SmartXConnection` connection string, SQL Server/LocalDB service availability, and database resource pressure. Retry settings can help with transient failures but not with a consistently inaccessible database.

**Backend build fails due to `LiveMode`**  
Ensure `TelemetrySimulationDto` defines `public bool LiveMode { get; set; }` if referenced by the seeder.

**Old path mentioned in a `wwwroot` exception**  
After moving the project, stop the API, recreate `wwwroot` if needed, delete generated `bin`/`obj` folders, then restore and rebuild. Do not delete source folders or database files.

## Planned improvements

- SignalR/WebSocket streaming instead of polling.
- Configurable, server-side alert thresholds and alert history.
- Device authentication and secure telemetry ingestion.
- More scalable time-range queries, aggregation and pagination for large deployments.
- Sensor configuration, log and deployment-photo attachments.
- Automated tests, deployment scripts and production-oriented observability.

## Security and limitations

Smart-X is presently a **development/academic prototype**. Before any production deployment, restrict CORS origins, implement authentication and authorisation, secure device-to-gateway communication, validate ingestion rates and payload sizes, and establish retention and backup policies. Simulator-generated data must not be mistaken for real operational measurements.

## License

No license has been specified. Unless a `LICENSE` file is added, do not assume the repository grants open-source reuse rights.
