
import {
    BrowserRouter,
    Routes,
    Route,
    Navigate,
    Link
} from "react-router-dom";

import TelemetryDashboard from "./pages/TelemetryDashboard";
import Sensors from "./pages/Sensors";
import Navbar from "./components/Navbar";


function ModulePreview({
    title,
    description,
    moduleName
}) {
    return (
        <>
            <Navbar variant="top" />

            <main className="sx-module-preview">
                <div className="sx-module-preview-content">

                    <span className="sx-module-preview-eyebrow">
                        SMART-X / {moduleName}
                    </span>

                    <h1>{title}</h1>

                    <p>{description}</p>

                    <p>
                        This module is registered in the
                        Smart-X navigation system.
                        Its functionality will be
                        implemented in the next
                        development chunks.
                    </p>

                    <Link
                        to="/telemetry"
                        className="sx-module-preview-link"
                    >
                        Return to Operations Centre
                    </Link>
                </div>
            </main>
        </>
    );
}

function App() {
    return (
        <BrowserRouter>
            <Routes>

                <Route
                    path="/"
                    element={
                        <Navigate
                            to="/telemetry"
                            replace
                        />
                    }
                />

                <Route
                    path="/telemetry"
                    element={<TelemetryDashboard />}
                />

                <Route
                    path="/sensors"
                    element={<Sensors />}
                />

                <Route
                    path="/commands"
                    element={
                        <ModulePreview
                            moduleName="COMMAND STREAM"
                            title="Real-Time Command Stream & History"
                            description={
                                "Monitor incoming telemetry, issue manual device override commands, inspect command history and review automated recommendations."
                            }
                        />
                    }
                />

                <Route
                    path="/topology"
                    element={
                        <ModulePreview
                            moduleName="NETWORK TOPOLOGY"
                            title="Network Topology & Mesh Routing"
                            description={
                                "Visualise connected IoT nodes, inspect network relationships and explore routing paths across the Smart-X ecosystem."
                            }
                        />
                    }
                />

                <Route
                    path="*"
                    element={
                        <Navigate
                            to="/telemetry"
                            replace
                        />
                    }
                />

            </Routes>
        </BrowserRouter>
    );
}

export default App;
