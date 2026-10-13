import { NavLink } from "react-router-dom";
import "./Navbar.css";

const navItems = [
  { to: "/telemetry", label: "Operations", icon: "◫" },
  { to: "/", label: "Sensor registry", icon: "▤" },
];

/**
 * variant="sidebar" uses the .sx-sidebar styles in TelemetryDashboard.css.
 * variant="top" is intended for the existing sensor registry page.
 */
export default function Navbar({ variant = "sidebar" }) {
  if (variant === "top") {
    return (
      <header className="sx-registry-navbar">
        <NavLink to="/telemetry" className="sx-registry-brand">
          <span aria-hidden="true">✳</span> Smart<span>X</span>
        </NavLink>
        <nav className="sx-registry-navlinks" aria-label="Main navigation">
          {navItems.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={item.to === "/"}
              className={({ isActive }) =>
                `sx-registry-navlink${isActive ? " sx-registry-navlink--active" : ""}`
              }
            >
              {item.label}
            </NavLink>
          ))}
        </nav>
      </header>
    );
  }

  return (
    <aside className="sx-sidebar" aria-label="Dashboard navigation">
      <NavLink className="sx-brand" to="/telemetry">
        <span className="sx-brand-mark" aria-hidden="true">✳</span>
        Smart<span>X</span>
      </NavLink>

      <p className="sx-sidebar-label">WORKSPACE</p>
      <nav className="sx-nav" aria-label="Main navigation">
        {navItems.map((item) => (
          <NavLink
            key={item.to}
            to={item.to}
            end={item.to === "/"}
            className={({ isActive }) =>
              `sx-nav-link${isActive ? " sx-nav-link--active" : ""}`
            }
          >
            <span aria-hidden="true">{item.icon}</span> &nbsp; {item.label}
          </NavLink>
        ))}
      </nav>

      <div className="sx-sidebar-foot">
        <div className="sx-live-dot" aria-hidden="true" />
        <div>
          <strong>Gateway overview</strong>
          <small>Auto-refresh · 10 seconds</small>
        </div>
      </div>
    </aside>
  );
}
