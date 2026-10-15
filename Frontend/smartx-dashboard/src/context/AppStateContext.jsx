
import {
    createContext,
    useContext,
    useState
} from "react";

const AppStateContext = createContext(null);

export function AppStateProvider({ children }) {

    const [metric, setMetric] = useState("temperature");

    const [selectedSensorId, setSelectedSensorId] =
        useState("all");

    const [search, setSearch] = useState("");

    const [statusFilter, setStatusFilter] =
        useState("All");

    const [categoryFilter, setCategoryFilter] =
        useState("All");

    const [registrySearch, setRegistrySearch] =
        useState("");

    const [
        registryCategoryFilter,
        setRegistryCategoryFilter
    ] = useState("All");

    const [registryPage, setRegistryPage] =
        useState(1);

    const [editingSensorId, setEditingSensorId] =
        useState(null);

    const value = {
        // Telemetry
        metric,
        setMetric,

        selectedSensorId,
        setSelectedSensorId,

        search,
        setSearch,

        statusFilter,
        setStatusFilter,

        categoryFilter,
        setCategoryFilter,

        // Sensor Registry
        registrySearch,
        setRegistrySearch,

        registryCategoryFilter,
        setRegistryCategoryFilter,

        registryPage,
        setRegistryPage,

        editingSensorId,
        setEditingSensorId
    };

    return (
        <AppStateContext.Provider value={value}>
            {children}
        </AppStateContext.Provider>
    );
}

// Custom hook for accessing shared state.
export function useAppState() {
    const context = useContext(AppStateContext);

    if (context === null) {
        throw new Error(
            "useAppState must be used inside AppStateProvider."
        );
    }

    return context;
}
