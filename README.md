# Smart-X | IoT Telemetry Management Platform

**Smart-X** is a web-based Internet of Things (IoT) monitoring and telemetry management system designed to help operators register connected devices, collect measurements, understand device health, and investigate unusual sensor activity from a central dashboard.

## Purpose

IoT installations may contain hundreds or thousands of distributed sensors generating measurements in different formats. Reading those values one record at a time makes it difficult to detect sudden spikes, identify silent devices, and understand what needs attention.

Smart-X converts raw device data into an accessible operational view. It is intended to:

- Consolidate sensor information and telemetry in one place.
- Support **multiple measurement types**, including floating-point readings, integer power values, and Boolean equipment states.
- Make abnormal measurements and reporting gaps easier to spot.
- Help operators investigate a particular sensor without searching through unrelated readings.
- Demonstrate scalable IoT data-ingestion concepts with simulated devices.

## Application overview

Smart-X has two primary frontend workspaces.

### Operations Centre

The **Operations Centre** is the telemetry-monitoring dashboard. It brings together:

- **Operational summary cards** showing the number of registered devices, currently online devices, offline devices, and anomalies in recently loaded samples.
- **Interactive telemetry charts** for temperature (°C), soil moisture (%), and power usage (W).
- **Sensor-specific investigation**, allowing an operator to select a device and examine its recent measurement history.
- **Attention-needed alerts** for abnormal sample values and devices that are offline or reporting late. Selecting an alert focuses the relevant device.
- **Sensor health directory** with search and category/status filtering.
- **Guided setup checklist** illustrating core steps in preparing a monitored deployment.
- **Automatic dashboard updates**, currently performed by periodic API polling, plus manual refresh.
- **Responsive layouts** for desktop and smaller displays.

The dashboard's current anomaly thresholds are **demonstration rules**, not calibrated safety limits. They illustrate how a monitoring interface can surface unusual activity.

### Sensor Registry

The **Sensor Registry** is the device-management workspace. Operators can:

- Register sensors using a MAC address, deployment location, and category.
- Browse and search registered devices.
- Update sensor details when deployments change.
- Remove a sensor from the registry.

Each registered sensor provides the identity used to associate incoming telemetry with a specific device.

## How Smart-X works

**1. Register devices.** A sensor is added with identifying information such as its MAC address, category, and installation location.

**2. Ingest telemetry.** The API accepts individual readings or batches of readings associated with registered sensor IDs. A reading can contain temperature, soil moisture, power consumption, and actuator state values.

**3. Persist measurements.** Entity Framework Core stores sensors and their telemetry records in SQL Server. Each reading has a timestamp, enabling trend analysis and device-status calculations.

**4. Determine device health.** The backend compares each sensor's latest telemetry timestamp with the current time. In the current prototype, a device is shown as **Online** when its most recent reading is within 30 seconds, **Delayed** within 30–120 seconds, and **Offline** after 120 seconds or when it has never reported.

**5. Visualize and investigate.** The React dashboard fetches recent measurements and device health information. Operators can switch measurements, filter devices, inspect historical readings, and jump from alerts to the affected sensor.

**6. Simulate activity.** The simulator produces batches of category-related sample readings, including a configurable share of deliberately abnormal values. This enables demonstrations of trends, anomalies, and health monitoring without requiring a deployed physical sensor network.


## Data model

### Sensor

A registered IoT device includes:

| Field | Meaning |
|---|---|
| `Id` | Internal sensor identifier |
| `MacAddress` | Device network identifier |
| `Location` | Deployment location, such as a greenhouse or building |
| `Category` | Type of monitoring device |
| `RegisteredAt` | Time the sensor was registered |

### TelemetryRecord

Each telemetry record is linked to a sensor and may contain a mixture of measurement types:

| Field | Type | Example |
|---|---|---|
| `SensorId` | Integer | `1` |
| `Temperature` | Nullable floating-point | `24.8` °C |
| `SoilMoisture` | Nullable floating-point | `62.7` % |
| `Wattage` | Nullable integer | `1430` W |
| `ValveOpen` | Nullable Boolean | `true` |
| `Timestamp` | Date/time | UTC measurement time |

Nullable measurements allow a telemetry message to contain the fields relevant to that sensor rather than requiring every device to report every measurement.

## API capabilities

### Sensor management

| Method | Route | Action |
|---|---|---|
| `GET` | `/api/sensors` | List registered sensors |
| `GET` | `/api/sensors/{id}` | Retrieve one sensor |
| `POST` | `/api/sensors` | Register a sensor |
| `PUT` | `/api/sensors/{id}` | Update sensor details |
| `DELETE` | `/api/sensors/{id}` | Remove a sensor |
| `GET` | `/api/sensors/health` | View calculated device-health states |

### Telemetry

| Method | Route | Action |
|---|---|---|
| `GET` | `/api/telemetry` | Retrieve latest telemetry readings |
| `GET` | `/api/sensors/{sensorId}/telemetry` | Retrieve recent readings for one sensor |
| `POST` | `/api/telemetry` | Ingest one telemetry record |
| `POST` | `/api/telemetry/batch` | Ingest multiple telemetry records |
| `POST` | `/api/telemetry/simulate` | Generate simulated telemetry in batches |

### Example telemetry submission

```json
{
  "sensorId": 1,
  "soilMoisture": 62.7,
  "temperature": 24.8,
  "wattage": 1430,
  "valveOpen": true
}
```

### Example simulation request

```json
{
  "recordCount": 1000,
  "batchSize": 500,
  "anomalyRate": 0.02,
  "liveMode": true
}
```

`recordCount` controls the number of samples, `batchSize` controls how samples are grouped for insertion, and `anomalyRate` controls the approximate proportion of generated abnormal readings. `liveMode` generates recent timestamps for dashboard demonstrations; it does **not** start an ongoing background stream.

## Dashboard design and user engagement

Smart-X is organized around five complementary strategies to support efficient use of a technical monitoring system:

1. **Real-time-oriented visual feedback:** frequently refreshed measurements, trend charts, and clear health states make changing conditions visible.
2. **Proactive alert presentation:** abnormal samples and devices with stale telemetry are surfaced in an attention panel, with direct paths to investigate affected sensors.
3. **Progressive onboarding and contextual guidance:** setup progress and in-dashboard explanations help users understand monitoring tasks and chart signals.
4. **Progress indicators:** a setup checklist makes configuration milestones visible and encourages completion of the monitoring workflow.
5. **User-centred interactive design:** search, filters, focused sensor history, readable summaries, and responsive layouts reduce unnecessary navigation.

The intended troubleshooting journey is simple: **notice a problem → identify the sensor → inspect its recent measurements → decide what to investigate next**.

## Intended use cases

- **Hydroponic farms:** observe temperature and moisture patterns and inspect irrigation actuator states.
- **Smart buildings:** monitor connected devices and investigate unusual consumption or missing telemetry.
- **Energy monitoring:** surface power spikes and review recent wattage trends across deployed devices.
- **Development and demonstrations:** generate representative telemetry volumes, explore dashboard interactions, and demonstrate gateway/data-management concepts without physical hardware.


# Assigment 2
## Phase 1: Navigation and Application State establishes the frontend foundation for Smart-X's real-time processing and device management features.

### Implemented Features
- **Main Navigation:** Added navigation between Sensor Data Ingestion & Telemetry, Real-Time Command Stream & History, Network Topology & Mesh Routing, and Sensor Registry.
- **Dedicated Module Pages:** Created `CommandStream.jsx` and `NetworkTopology.jsx` as foundations for upcoming functionality.
- **Shared Application State:** Implemented React Context (`AppStateContext.jsx`) to preserve selected sensors, search filters, chart preferences, pagination, and editing selections when navigating between pages.
- **Responsive Interface:** Maintained consistent styling and navigation across desktop and mobile devices.

### Technologies Used
React, React Router, React Context API, JavaScript (JSX), and CSS.

**Outcome:** Smart-X now supports seamless client-side navigation while retaining important user interface settings. This establishes the foundation for real-time command processing, device control, and predictive monitoring in subsequent phases.

## Phase 2: Real-Time Processing Engine
Phase 2 transforms Smart-X from a basic IoT telemetry gateway into a real-time processing system using advanced C# data structures.

### Implemented Features
- FIFO Telemetry Queue (Queue<T>): Processes incoming sensor readings in arrival order using a background worker.
- Priority Processing (PriorityQueue<TElement, TPriority>): Prioritises emergency and critical telemetry based on temperature, soil moisture and power thresholds.
- Live Device Registry (Dictionary<TKey, TValue>): Provides fast, MAC-address-based device lookups and maintains 
- Chronological History (SortedDictionary<TKey, TValue>): Organises telemetry by timestamp, supports duplicate timestamps and maintains a bounded in-memory history.
- Active Alerts (HashSet<T>): Tracks unique sensor faults and disconnected devices, preventing duplicate alerts and automatically resolving recovered conditions.

### Backend Architecture
Incoming HTTP telemetry is queued and processed asynchronously by ASP.NET Core background services. Successfully processed readings are persisted using Entity Framework Core and SQL Server, while in-memory data structures support real-time monitoring.

### Testing and Validation
Phase 2 includes 35 xUnit test cases covering queue behaviour, prioritisation, device lookups, chronological ordering, duplicate prevention and alert resolution. A Postman collection provides additional API-level functional tests.

## Phase 3: IoT Device Simulation and Load Testing
Phase 3 uses a standalone .NET 10 IoT device simulator that generates realistic sensor readings and transmits them to the Smart-X API using HTTP.

### Implemented Features
- Device Profiles: Developed simulated ESP32 environmental sensors, smart plugs, irrigation valves and pump controllers, each generating device-specific telemetry.
- HTTP Communication: Implemented automatic device registration using MAC addresses and real HTTP telemetry transmission to the Smart-X API.
- Continuous Simulation: Enabled multiple devices to operate concurrently with configurable reporting intervals, graceful shutdown and live statistics.
- Fault Injection: Introduced simulated temperature spikes, moisture drops, power surges and device disconnections to test emergency processing and automatic alert resolution.
- Load Testing: Implemented high-volume simulation supporting up to 2,000 virtual devices, with configurable request rates, bounded concurrency and performance monitoring.

### Technologies Used
C# 13, .NET 10, HttpClient, asynchronous Tasks, PeriodicTimer, Channels, ASP.NET Core Minimal APIs, Entity Framework Core and SQL Server.

## Phase 4: Real-Time IoT Device Commands, Manual Overrides and Persistent History
Phase 4 introduces bidirectional communication between the Smart-X backend and simulated IoT devices.
While Phase 3 focused on device registration, telemetry ingestion, fault detection and load testing, Phase 4 enables operators to issue manual control instructions to connected devices.
The system supports controlling pumps, irrigation valves and smart plugs through REST APIs. Devices periodically retrieve pending commands, execute them, and send acknowledgements confirming whether the requested operation succeeded.

### Implemented Features
- Device Command Models and Control States
- Device Command Service and REST API
- Simulated Device Command Execution and Acknowledgement
- Manual Command Undo Using Stack<T>
- Persistent Command History and SQL Server Audit Logging

## Phase 5 - Command Centre Dashboard
Phase 5 introduces an interactive React-based Command Centre for managing Smart-X IoT devices. Operators can remotely control smart plugs, irrigation valves, and pumps, monitor device connectivity, track command execution, and undo previous actions. The dashboard also includes searchable command history, SQL Server audit logs, real-time status updates, and responsive layouts. Safety checks prevent commands from being issued to offline devices or devices with outstanding operations.

## Phase 6 – Predictive Actions & Recommendation Engine

Phase 6 introduces an explainable, rule-based recommendation engine that analyses historical telemetry, operator searches, and previously executed device commands. Smart-X identifies recurring relationships between sensor anomalies and operator actions, then ranks suggested actions using historical frequency, recency, current sensor conditions, and device readiness. A dedicated Suggested Actions dashboard displays recommendation scores, supporting evidence, and safety warnings. All recommendations are advisory only, ensuring that operators retain full control over device operations.

# Part 2 – Requirements and Implementation

The following table identifies where each Part 2 requirementc is implemented in the Smart-X application.

| Requirement | Implementation |
|---|---|
| Main Menu & Navigation | `Navbar.jsx` provides navigation between Telemetry, Command Centre, Sensor Registry, Suggested Actions and Network Topology. React Router enables navigation without losing application state, supported by `AppStateContext`. Network Topology is reserved for the final POE. |
| Real-Time Command Stream | `CommandStream.jsx` provides live device monitoring, search/filtering, manual control of smart plugs, pumps and valves, command acknowledgements, execution history, undo functionality and audit logs. |
| Queue – FIFO | The C# backend uses `Queue<T>` to process incoming telemetry packets in First-In-First-Out order. |
| **Priority Queue** | `PriorityQueue<TElement, TPriority>` prioritises critical telemetry alerts, allowing urgent anomalies to be processed ahead of standard events. |
| Stack – Undo | `DeviceCommandService` uses `Stack<T>` to maintain manual command history and support Last-In-First-Out undo operations. |
| Dictionary – Fast Lookups | `Dictionary<TKey, TValue>` manages registered/live devices by unique identifiers for efficient O(1) average-case lookups. |
| SortedDictionary – Historical Logs | `SortedDictionary<TKey, TValue>` organises historical telemetry chronologically for efficient timeline retrieval and display. |
| HashSet – Unique Alerts | `HashSet<T>` tracks unique active device errors and disconnections, preventing duplicate alert processing. |
| Predictive Recommendation Engine | `OperatorSearchEvent` records search activity, `HistoricalRelationshipAnalyzer.cs` identifies anomaly-action patterns, and `RecommendationRanker.cs` scores suggested actions. `SuggestedActions.jsx` displays explainable recommendations and device warnings. |
| ESP32 Simulation | `SmartX.DeviceSimulator` simulates environmental sensors, smart plugs, irrigation valves and pumps, sending telemetry and receiving commands through the REST API. |

## Reference List


## Assigment 2
Microsoft Ignite.2026. Unit testing C# in .NET using dotnet test and xUnit. [online]. Available at: <https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-csharp-with-xunit> [Accessed 15 October 2026]

Microsoft Ignite.2026. Unit testing C# in .NET using dotnet test and xUnit. [online]. Available at: <https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-csharp-with-xunit> [Accessed 15 October 2026]

Linux Code.2026.C# Queue With Examples: Practical FIFO Patterns for Modern .NET. [online]. Available at: <https://thelinuxcode.com/c-queue-with-examples-practical-fifo-patterns-for-modern-net/> [Accessed 16 October 2026]

GeeksForGeeks.2026.C# PriorityQueue. [online]. Available at: <https://www.geeksforgeeks.org/c-sharp/c-priorityqueue/> [Accessed 16 October 2026]

Microsoft Ignite.2026.SortedDictionary TKey,TValue Class. [online]. Available at: <https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.sorteddictionary-2?view=net-10.0> [Accessed 16 October 2026]

Microsoft Ignite.2026.Task asynchronous programming model. [online]. Available at: <https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/task-asynchronous-programming-model> [Accessed 17 October 2026]

Microsoft Ingite.2026.PeriodicTimer Class. [online]. Available at: <https://learn.microsoft.com/en-us/dotnet/api/system.threading.periodictimer?view=net-10.0/> [Accessed 17 October 2026]

Microsoft Ingite.2026.Dictionary<TKey,TValue> Class. [online]. Available at: <https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2?view=net-10.0> [Accessed 17 October 2026]

## Assignment 1
GitHubDocs. 2026 Building and testing Node.js [online] Available at: <https://docs.github.com/en/actions/tutorials/build-and-test-code/nodejs > 

Schwarzmuller, M. 2026. React - The Complete Guide (incl. Next.js, Redux). [Online Course] Available at:< https://www.udemy.com/course/react-the-complete-guide-incl-redux/?couponCode=KEEPLEARNING >

Chart.js. 2025. Bar Chart [online] Available at: < https://www.chartjs.org/docs/latest/charts/bar.html >

Choudhary, H. 2026. React - Complete web development course. [Online Course] Available at: < https://www.udemy.com/course/web-dev-master/?couponCode=KEEPLEARNING >

Uppuluri,K. 2024. Mastering ASP.NET Core MVC From Scratch Using C#. [Online Course] Available at:<https://www.udemy.com/course/mastering-aspnetcoremvc/?couponCode=MT261012G1>


Chart.js. 2025. Bar Chart [online] Available at: < https://www.chartjs.org/docs/latest/charts/bar.html >

