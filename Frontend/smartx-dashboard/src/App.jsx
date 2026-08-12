import {
    BrowserRouter,
    Routes,
    Route
} from "react-router-dom";

import Dashboard from "./pages/Dashboard";
import TelemetryDashboard from "./pages/TelemetryDashboard";

function App() {

    return (
        <BrowserRouter>

            <Routes>

                <Route
                    path="/"
                    element={<Dashboard />}
                />

                <Route
                    path="/telemetry"
                    element={<TelemetryDashboard />}
                />

            </Routes>

        </BrowserRouter>
    );
}

export default App;