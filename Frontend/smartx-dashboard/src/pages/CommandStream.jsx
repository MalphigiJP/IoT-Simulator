import { Link } from "react-router-dom";

import Navbar from "../components/Navbar";
import "./ModulePages.css";

const commandModules = [
    {
        number: "01",
        title: "Live Command Stream",
        description:
            "Monitor incoming telemetry packets, processing queues, device states and critical alerts.",
        technology: "FIFO Queue + PriorityQueue",
        phase: "Phase 2"
    },
    {
        number: "02",
        title: "Manual Device Controls",
        description:
            "Issue simulated commands to pumps, smart relays, valves and other connected actuators.",
        technology: "Device Dictionary + Command API",
        phase: "Phase 4"
    },
    {
        number: "03",
        title: "Command History & Undo",
        description:
            "Review previous device operations and reverse eligible commands using their saved state.",
        technology: "Stack + SQL Server",
        phase: "Phase 4"
    },
    {
        number: "04",
        title: "Suggested Actions",
        description:
            "Identify recurring operational patterns and recommend relevant device investigations.",
        technology: "Pattern Analysis + Dictionaries",
        phase: "Phase 6"
    }
];

const workflow = [
    {
        step: "01",
        title: "Receive",
        description:
            "Simulated devices transmit telemetry packets to the Smart-X API."
    },
    {
        step: "02",
        title: "Prioritise",
        description:
            "Critical telemetry is prioritised ahead of normal queued readings."
    },
    {
        step: "03",
        title: "Investigate",
        description:
            "Operators identify affected devices and inspect their current state."
    },
    {
        step: "04",
        title: "Control",
        description:
            "An operator issues a command, receives its simulated acknowledgement and reviews its history."
    }
];

export default function CommandStream() {
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
                            SMART-X / PART 2 / DEVICE OPERATIONS
                        </p>

                        <h1>
                            Real-Time Command
                            <span> Stream & History</span>
                        </h1>

                        <p className="sx-module-description">
                            A central workspace for monitoring
                            incoming sensor data, investigating
                            device activity, issuing manual
                            overrides and reviewing operational
                            recommendations.
                        </p>

                        <div className="sx-module-actions">
                            <Link
                                to="/telemetry"
                                className="sx-module-button sx-module-button--primary"
                            >
                                View Telemetry Dashboard
                            </Link>

                            <Link
                                to="/sensors"
                                className="sx-module-button sx-module-button--secondary"
                            >
                                Open Sensor Registry
                            </Link>
                        </div>
                    </div>

                    <div className="sx-module-header-aside">
                        <div className="sx-module-info-card">
                            <span className="sx-module-info-icon">
                                ◈
                            </span>

                            <span className="sx-module-mini-label">
                                DEVELOPMENT STATUS
                            </span>

                            <strong>
                                Command Centre Foundation
                            </strong>

                            <p>
                                Navigation and page structure
                                are ready. Real-time processing
                                and device commands will be
                                connected in the next phases.
                            </p>

                            <span className="sx-module-state">
                                Interface foundation
                            </span>
                        </div>
                    </div>
                </header>

                <section
                    className="sx-module-section"
                    aria-labelledby="command-features-title"
                >
                    <div className="sx-module-section-heading">
                        <div>
                            <span className="sx-module-eyebrow">
                                FUNCTIONAL AREAS
                            </span>

                            <h2 id="command-features-title">
                                Command Centre Modules
                            </h2>

                            <p>
                                Four integrated components
                                will form the command and
                                telemetry processing workspace.
                            </p>
                        </div>

                        <span className="sx-module-section-tag">
                            Implementation roadmap
                        </span>
                    </div>

                    <div className="sx-module-card-grid">
                        {commandModules.map((module) => (
                            <article
                                className="sx-module-feature-card"
                                key={module.number}
                            >
                                <div className="sx-module-card-top">
                                    <span className="sx-module-card-number">
                                        {module.number}
                                    </span>

                                    <span className="sx-module-phase">
                                        {module.phase}
                                    </span>
                                </div>

                                <h3>{module.title}</h3>

                                <p>{module.description}</p>

                                <div className="sx-module-card-bottom">
                                    <span>DATA STRUCTURE / SYSTEM</span>
                                    <strong>
                                        {module.technology}
                                    </strong>
                                </div>
                            </article>
                        ))}
                    </div>
                </section>

                <section
                    className="sx-module-section"
                    aria-labelledby="workflow-title"
                >
                    <div className="sx-module-section-heading">
                        <div>
                            <span className="sx-module-eyebrow">
                                PROCESSING MODEL
                            </span>

                            <h2 id="workflow-title">
                                From Telemetry to Action
                            </h2>

                            <p>
                                The planned command lifecycle
                                connects incoming IoT data
                                with operator decisions.
                            </p>
                        </div>
                    </div>

                    <ol className="sx-module-workflow">
                        {workflow.map((item) => (
                            <li
                                className="sx-module-workflow-step"
                                key={item.step}
                            >
                                <span className="sx-module-step-number">
                                    {item.step}
                                </span>

                                <h3>{item.title}</h3>

                                <p>{item.description}</p>
                            </li>
                        ))}
                    </ol>
                </section>

                <section className="sx-module-notice">
                    <div>
                        <strong>
                            Device command execution is not enabled yet.
                        </strong>

                        <p>
                            This is the Command Centre interface
                            foundation. The processing engine,
                            command endpoints and command
                            history will be implemented in
                            subsequent Part 2 chunks.
                        </p>
                    </div>

                    <Link to="/telemetry">
                        Continue monitoring →
                    </Link>
                </section>

                <footer className="sx-module-footer">
                    <span>
                        Smart-X IoT Ecosystem
                    </span>

                    <span>
                        Real-Time Command Stream & History
                    </span>
                </footer>
            </main>
        </div>
    );
}
