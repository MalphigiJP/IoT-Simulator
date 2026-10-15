
import {
    BrowserRouter,
    Routes,
    Route,
    Navigate
} from "react-router-dom";

import { AppStateProvider } from "./context/AppStateContext";

import TelemetryDashboard from "./pages/TelemetryDashboard";
import Sensors from "./pages/Sensors";
import CommandStream from "./pages/CommandStream";
import NetworkTopology from "./pages/NetworkTopology";

function App() {
    return (
        <BrowserRouter>
            <AppStateProvider>
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
                        element={<CommandStream />}
                    />

                    <Route
                        path="/topology"
                        element={<NetworkTopology />}
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
            </AppStateProvider>
        </BrowserRouter>
    );
}

export default App;
