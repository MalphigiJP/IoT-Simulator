
import { Link } from "react-router-dom";

import Navbar from "../components/Navbar";
import "./ModulePages.css";

const networkModules = [
    {
        number: "01",
        title: "Network Discovery",
        description:
            "Identify registered gateways, ESP32 nodes and other devices participating in the IoT network."
    },
    {
        number: "02",
        title: "Topology Visualisation",
        description:
            "Display network relationships and connections between gateways, nodes and devices."
    },
    {
        number: "03",
        title: "Mesh Routing",
        description:
            "Examine available communication paths and the routing relationships between connected nodes."
    },
    {
        number: "04",
        title: "Connection Health",
        description:
            "Highlight disconnected nodes, communication problems and changes in network connectivity."
    }
];

export default function NetworkTopology() {
    return (
        <div className="sx-module-app">
            <Navbar variant="top" />

            <main
                className="sx-module-main"
                id="main-content"
            >
                <header className="sx-module-header">
                    <div>
                        <p className="sx-module-eyebrow">
                            SMART-X / NETWORK INFRASTRUCTURE
                        </p>

                        <h1>
                            Network Topology
                            <span> & Mesh Routing</span>
                        </h1>

                        <p className="sx-module-description">
                            A dedicated workspace for
                            visualising connected IoT devices,
                            understanding their network
                            relationships and investigating
                            communication pathways.
                        </p>

                        <div className="sx-module-actions">
                            <Link
                                to="/sensors"
                                className="sx-module-button sx-module-button--primary"
                            >
                                View Registered Devices
                            </Link>

                            <Link
                                to="/telemetry"
                                className="sx-module-button sx-module-button--secondary"
                            >
                                View Telemetry
                            </Link>
                        </div>
                    </div>

                    <div className="sx-module-header-aside">
                        <div className="sx-module-info-card">
                            <span className="sx-module-info-icon">
                                ◎
                            </span>

                            <span className="sx-module-mini-label">
                                DEVELOPMENT STATUS
                            </span>

                            <strong>
                                Final POE Module
                            </strong>

                            <p>
                                This page establishes the
                                Network Topology interface.
                                Network discovery, visualisation
                                and routing functionality will
                                be developed in the final POE.
                            </p>

                            <span className="sx-module-state">
                                Planned implementation
                            </span>
                        </div>
                    </div>
                </header>

                <section
                    className="sx-module-section"
                    aria-labelledby="topology-overview-title"
                >
                    <div className="sx-module-section-heading">
                        <div>
                            <span className="sx-module-eyebrow">
                                NETWORK ARCHITECTURE
                            </span>

                            <h2 id="topology-overview-title">
                                IoT Network Overview
                            </h2>

                            <p>
                                An illustration of how the
                                future network visualisation
                                may represent the Smart-X
                                ecosystem.
                            </p>
                        </div>

                        <span className="sx-module-section-tag">
                            Conceptual diagram
                        </span>
                    </div>

                    <figure className="sx-topology-figure">
                        <div
                            className="sx-topology-diagram"
                            role="img"
                            aria-label="Conceptual network diagram showing a Smart-X gateway connecting to environmental sensors, smart plugs and actuators through network links."
                        >
                            <div className="sx-topology-node sx-topology-node--gateway">
                                <span className="sx-topology-node-icon">
                                    ◈
                                </span>

                                <strong>
                                    Smart-X Gateway
                                </strong>

                                <small>
                                    Central processing API
                                </small>
                            </div>

                            <div
                                className="sx-topology-connector"
                                aria-hidden="true"
                            >
                                <span>↓</span>
                            </div>

                            <div className="sx-topology-node-grid">
                                <div className="sx-topology-node">
                                    <span className="sx-topology-node-icon">
                                        ◉
                                    </span>

                                    <strong>
                                        Environmental Nodes
                                    </strong>

                                    <small>
                                        Temperature / Moisture
                                    </small>
                                </div>

                                <div className="sx-topology-node">
                                    <span className="sx-topology-node-icon">
                                        ⚡
                                    </span>

                                    <strong>
                                        Smart Plugs
                                    </strong>

                                    <small>
                                        Power Monitoring
                                    </small>
                                </div>

                                <div className="sx-topology-node">
                                    <span className="sx-topology-node-icon">
                                        ◇
                                    </span>

                                    <strong>
                                        Actuator Nodes
                                    </strong>

                                    <small>
                                        Pumps / Valves / Relays
                                    </small>
                                </div>
                            </div>
                        </div>

                        <figcaption className="sx-topology-caption">
                            Illustrative architecture only.
                            These are device categories, not
                            discovered network nodes or
                            calculated mesh routes.
                        </figcaption>
                    </figure>
                </section>

                <section
                    className="sx-module-section"
                    aria-labelledby="network-features-title"
                >
                    <div className="sx-module-section-heading">
                        <div>
                            <span className="sx-module-eyebrow">
                                FINAL POE
                            </span>

                            <h2 id="network-features-title">
                                Planned Network Capabilities
                            </h2>

                            <p>
                                This module will evolve into
                                an interactive network
                                management interface.
                            </p>
                        </div>
                    </div>

                    <div className="sx-module-card-grid">
                        {networkModules.map((module) => (
                            <article
                                className="sx-module-feature-card"
                                key={module.number}
                            >
                                <div className="sx-module-card-top">
                                    <span className="sx-module-card-number">
                                        {module.number}
                                    </span>

                                    <span className="sx-module-phase">
                                        Final POE
                                    </span>
                                </div>

                                <h3>{module.title}</h3>

                                <p>{module.description}</p>
                            </article>
                        ))}
                    </div>
                </section>

                <section className="sx-module-notice">
                    <div>
                        <strong>
                            Network topology is reserved for
                            the final POE.
                        </strong>

                        <p>
                            For now, registered device
                            information is accessible through
                            the Sensor Registry and
                            Operations Centre.
                        </p>
                    </div>

                    <Link to="/sensors">
                        Open Sensor Registry →
                    </Link>
                </section>

                <footer className="sx-module-footer">
                    <span>Smart-X IoT Ecosystem</span>

                    <span>
                        Network Topology & Mesh Routing
                    </span>
                </footer>
            </main>
        </div>
    );
}
