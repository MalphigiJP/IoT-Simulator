
import { Link, NavLink } from "react-router-dom";
import "./Navbar.css";

const navigationItems = [
    {
        path: "/telemetry",
        label: "Sensor Data Ingestion & Telemetry",
        shortLabel: "Telemetry",
        icon: "◫"
    },
    {
        path: "/commands",
        label: "Real-Time Command Stream & History",
        shortLabel: "Commands",
        icon: "⌁"
    },
    {
        path: "/topology",
        label: "Network Topology & Mesh Routing",
        shortLabel: "Network",
        icon: "◎"
    },
    {
        path: "/sensors",
        label: "Sensor Registry",
        shortLabel: "Sensors",
        icon: "▤"
    }
];

function Navbar({ variant = "sidebar" }) {

    if (variant === "top") {
        return (
            <header className="sx-navbar-top">
                <div className="sx-navbar-top-inner">

                    <Link
                        to="/telemetry"
                        className="sx-navbar-top-brand"
                    >
                        <span className="sx-brand-mark">
                            ✳
                        </span>

                        <span>
                            Smart<span className="sx-brand-x">X</span>
                        </span>
                    </Link>

                    <nav
                        className="sx-navbar-top-menu"
                        aria-label="Main navigation"
                    >
                        {navigationItems.map((item) => (
                            <NavLink
                                key={item.path}
                                to={item.path}
                                className={({ isActive }) =>
                                    `sx-navbar-top-link ${
                                        isActive
                                            ? "sx-navbar-top-link--active"
                                            : ""
                                    }`
                                }
                                title={item.label}
                            >
                                <span aria-hidden="true">
                                    {item.icon}
                                </span>

                                <span>{item.shortLabel}</span>
                            </NavLink>
                        ))}
                    </nav>
                </div>
            </header>
        );
    }


    return (
        <aside
            className="sx-sidebar"
            aria-label="Dashboard navigation"
        >
            <Link
                className="sx-brand"
                to="/telemetry"
            >
                <span className="sx-brand-mark">
                    ✳
                </span>

                Smart<span>X</span>
            </Link>

            <p className="sx-sidebar-label">
                WORKSPACE
            </p>

            <nav
                className="sx-nav"
                aria-label="Main navigation"
            >
                {navigationItems.map((item) => (
                    <NavLink
                        key={item.path}
                        to={item.path}
                        className={({ isActive }) =>
                            `sx-nav-link ${
                                isActive
                                    ? "sx-nav-link--active"
                                    : ""
                            }`
                        }
                        title={item.label}
                    >
                        <span
                            className="sx-nav-icon"
                            aria-hidden="true"
                        >
                            {item.icon}
                        </span>

                        <span>{item.label}</span>
                    </NavLink>
                ))}
            </nav>

            <div className="sx-sidebar-foot">
                <div
                    className="sx-live-dot"
                    aria-hidden="true"
                />

                <div>
                    <strong>Smart-X Gateway</strong>
                    <small>IoT Monitoring Platform</small>
                </div>
            </div>
        </aside>
    );
}

export default Navbar;
