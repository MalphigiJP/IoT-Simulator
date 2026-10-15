import "./SensorList.css";

const PAGE_SIZE = 10;

function SensorList({
    sensors = [],
    onEdit,
    onDelete,

    search,
    onSearchChange,

    categoryFilter,
    onCategoryFilterChange,

    page,
    onPageChange
}) {

    const categories = [
        ...new Set(
            sensors
                .map((sensor) => sensor.category)
                .filter(Boolean)
        )
    ].sort();

    const filteredSensors = sensors.filter((sensor) => {

        const query = search.trim().toLowerCase();

        const matchesSearch = [
            sensor.macAddress,
            sensor.location,
            sensor.category,
            String(sensor.id)
        ].some((value) =>
            String(value ?? "")
                .toLowerCase()
                .includes(query)
        );

        const matchesCategory =
            categoryFilter === "All" ||
            sensor.category === categoryFilter;

        return matchesSearch && matchesCategory;
    });

    const totalPages = Math.max(
        1,
        Math.ceil(filteredSensors.length / PAGE_SIZE)
    );

    const currentPage = Math.min(
        Math.max(page, 1),
        totalPages
    );

    const startIndex = (currentPage - 1) * PAGE_SIZE;

    const displayedSensors = filteredSensors.slice(
        startIndex,
        startIndex + PAGE_SIZE
    );

    const handleSearchChange = (event) => {
        onSearchChange(event.target.value);
        onPageChange(1);
    };

    const handleCategoryChange = (event) => {
        onCategoryFilterChange(event.target.value);
        onPageChange(1);
    };

    const clearFilters = () => {
        onSearchChange("");
        onCategoryFilterChange("All");
        onPageChange(1);
    };

    return (
        <div className="sx-registry-list">

            <div className="sx-registry-toolbar">

                <label className="sx-registry-field">
                    <span>Search devices</span>

                    <input
                        type="search"
                        value={search}
                        onChange={handleSearchChange}
                        placeholder="MAC, location, ID..."
                    />
                </label>

                <label className="sx-registry-field">
                    <span>Category</span>

                    <select
                        value={categoryFilter}
                        onChange={handleCategoryChange}
                    >
                        <option value="All">
                            All categories
                        </option>

                        {categories.map((category) => (
                            <option
                                key={category}
                                value={category}
                            >
                                {category}
                            </option>
                        ))}
                    </select>
                </label>

                <button
                    type="button"
                    className="sx-registry-clear"
                    onClick={clearFilters}
                >
                    Clear Filters
                </button>

            </div>

            <div className="sx-registry-table-wrapper">

                <table className="sx-registry-table">

                    <thead>
                        <tr>
                            <th>ID</th>
                            <th>MAC Address</th>
                            <th>Location</th>
                            <th>Category</th>
                            <th>Registered</th>
                            <th>Actions</th>
                        </tr>
                    </thead>

                    <tbody>

                        {displayedSensors.map((sensor) => (
                            <tr key={sensor.id}>

                                <td>{sensor.id}</td>

                                <td>
                                    <code>
                                        {sensor.macAddress}
                                    </code>
                                </td>

                                <td>
                                    {sensor.location}
                                </td>

                                <td>
                                    {sensor.category}
                                </td>

                                <td>
                                    {sensor.registeredAt
                                        ? new Date(
                                            sensor.registeredAt
                                        ).toLocaleString()
                                        : "Unknown"}
                                </td>

                                <td>
                                    <div className="sx-registry-actions">

                                        {onEdit && (
                                            <button
                                                type="button"
                                                onClick={() =>
                                                    onEdit(sensor)
                                                }
                                            >
                                                Edit
                                            </button>
                                        )}

                                        {onDelete && (
                                            <button
                                                type="button"
                                                className="sx-registry-delete"
                                                onClick={() =>
                                                    onDelete(sensor.id)
                                                }
                                            >
                                                Delete
                                            </button>
                                        )}

                                    </div>
                                </td>

                            </tr>
                        ))}

                    </tbody>

                </table>
            </div>

            {filteredSensors.length === 0 && (
                <p className="sx-registry-empty">
                    {sensors.length === 0
                        ? "No sensors have been registered yet."
                        : "No sensors match your search or filters."}
                </p>
            )}

            <div className="sx-registry-pagination">

                <span>
                    {filteredSensors.length === 0
                        ? "0 devices"
                        : `Showing ${startIndex + 1}-${Math.min(
                            startIndex + PAGE_SIZE,
                            filteredSensors.length
                        )} of ${filteredSensors.length} devices`}
                </span>

                <div className="sx-registry-page-controls">

                    <button
                        type="button"
                        disabled={currentPage <= 1}
                        onClick={() =>
                            onPageChange(currentPage - 1)
                        }
                    >
                        Previous
                    </button>

                    <span>
                        Page {currentPage} of {totalPages}
                    </span>

                    <button
                        type="button"
                        disabled={currentPage >= totalPages}
                        onClick={() =>
                            onPageChange(currentPage + 1)
                        }
                    >
                        Next
                    </button>

                </div>
            </div>

        </div>
    );
}

export default SensorList;
