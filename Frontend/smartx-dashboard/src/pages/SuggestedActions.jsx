
import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";
import { getRankedRecommendations } from "../api/insightApi";
import { getCommandErrorMessage } from "../api/commandApi";
import "./SuggestedActions.css";

const REFRESH_INTERVAL = 60000;

const CONDITIONS = {
  LowSoilMoisture: {
    label: "Low soil moisture",
    symbol: "◉",
    description: "Soil moisture below the demo threshold",
  },
  HighTemperature: {
    label: "High temperature",
    symbol: "△",
    description: "Temperature above the demo threshold",
  },
  PowerSurge: {
    label: "Power surge",
    symbol: "⚡",
    description: "Wattage above the demo threshold",
  },
};

const READINESS = {
  Review: {
    label: "Review",
    description: "Current conditions warrant operator review",
  },
  Monitor: {
    label: "Monitor",
    description: "Historical evidence requires further observation",
  },
  Blocked: {
    label: "Blocked",
    description: "One or more screening checks failed",
  },
};

const SCORE_COMPONENTS = [
  {
    key: "repetition",
    label: "Historical repetition",
    maximum: 25,
  },
  {
    key: "followUpRate",
    label: "Observed follow-up rate",
    maximum: 25,
  },
  {
    key: "recency",
    label: "Historical recency",
    maximum: 15,
  },
  {
    key: "searchContext",
    label: "Related searches",
    maximum: 5,
  },
  {
    key: "currentAnomaly",
    label: "Current anomaly evidence",
    maximum: 20,
  },
  {
    key: "actuatorReadiness",
    label: "Actuator readiness",
    maximum: 10,
  },
];

function asArray(value) {
  return Array.isArray(value) ? value : [];
}

function formatDate(value) {
  if (!value) {
    return "Not available";
  }

  const date = new Date(value);

  return Number.isNaN(date.getTime())
    ? "Not available"
    : date.toLocaleString();
}

function formatNumber(value, digits = 1) {
  const number = Number(value);

  return value == null || !Number.isFinite(number)
    ? "—"
    : number.toFixed(digits);
}

function getConditionLabel(condition) {
  return CONDITIONS[condition]?.label || condition || "Unknown";
}

function getReadinessLabel(readiness) {
  return READINESS[readiness]?.label || readiness || "Unknown";
}

function getStateLabel(target, state) {
  if (state == null) {
    return "Unknown";
  }

  if (target === "Pump") {
    return state ? "Running" : "Stopped";
  }

  if (target === "Valve") {
    return state ? "Open" : "Closed";
  }

  if (target === "Relay") {
    return state ? "On" : "Off";
  }

  return state ? "On" : "Off";
}

function EvidenceSection({ title, items, className = "" }) {
  const entries = asArray(items);

  return (
    <section className={`sx-rec-evidence-section ${className}`}>
      <h4>{title}</h4>

      {entries.length === 0 ? (
        <p className="sx-rec-empty-evidence">
          No additional information available.
        </p>
      ) : (
        <ul>
          {entries.map((item, index) => (
            <li key={`${index}-${item}`}>{item}</li>
          ))}
        </ul>
      )}
    </section>
  );
}

function ScoreBreakdown({ recommendation }) {
  const breakdown = recommendation.scoreBreakdown || {};

  return (
    <section className="sx-rec-breakdown">
      <div className="sx-rec-details-heading">
        <h4>Explainable ranking</h4>
        <strong>
          {formatNumber(recommendation.score)} / 100
        </strong>
      </div>

      <div className="sx-rec-breakdown-list">
        {SCORE_COMPONENTS.map((component) => {
          const rawValue = Number(
            breakdown[component.key] ?? 0
          );

          const value = Number.isFinite(rawValue)
            ? Math.max(
                0,
                Math.min(component.maximum, rawValue)
              )
            : 0;

          const percentage =
            (value / component.maximum) * 100;

          return (
            <div
              key={component.key}
              className="sx-rec-breakdown-item"
            >
              <div className="sx-rec-breakdown-label">
                <span>{component.label}</span>

                <strong>
                  {formatNumber(value)} / {component.maximum}
                </strong>
              </div>

              <div className="sx-rec-breakdown-track">
                <div
                  className="sx-rec-breakdown-fill"
                  style={{ width: `${percentage}%` }}
                />
              </div>
            </div>
          );
        })}
      </div>

      <p className="sx-rec-score-disclaimer">
        This is a heuristic ranking score, not a probability
        of success or statistical confidence.
      </p>
    </section>
  );
}

function RecommendationCard({ recommendation }) {
  const condition = CONDITIONS[recommendation.condition];
  const readiness =
    READINESS[recommendation.readiness] || READINESS.Monitor;

  const rawScore = Number(recommendation.score);

  const score = Number.isFinite(rawScore)
    ? Math.max(0, Math.min(100, rawScore))
    : 0;

  const requestedState = getStateLabel(
    recommendation.target,
    recommendation.requestedState
  );

  const reviewLink =
    `/commands?sensorId=${encodeURIComponent(
      String(recommendation.actionSensorId)
    )}`;

  const observedValue =
    recommendation.currentObservedValue == null
      ? "Not confirmed"
      : `${formatNumber(
          recommendation.currentObservedValue,
          2
        )} ${recommendation.unit || ""}`;

  return (
    <article
      className={`sx-rec-card sx-rec-card-${recommendation.readiness.toLowerCase()}`}
    >
      <div className="sx-rec-card-top">
        <div className="sx-rec-condition">
          <span className="sx-rec-condition-symbol">
            {condition?.symbol || "◇"}
          </span>

          <div>
            <span className="sx-rec-eyebrow">
              RECOMMENDATION #{recommendation.rank}
            </span>

            <span className="sx-rec-condition-label">
              {getConditionLabel(recommendation.condition)}
            </span>
          </div>
        </div>

        <span
          className={`sx-rec-readiness sx-rec-${recommendation.readiness.toLowerCase()}`}
        >
          {getReadinessLabel(recommendation.readiness)}
        </span>
      </div>

      <h3 className="sx-rec-card-title">
        {recommendation.suggestedAction}
      </h3>

      <p className="sx-rec-card-subtitle">
        {readiness.description}
      </p>

      <div className="sx-rec-device-details">
        <span>
          <strong>Source:</strong>{" "}
          Sensor #{recommendation.sourceSensorId}
        </span>

        <span>
          <strong>Target:</strong>{" "}
          Sensor #{recommendation.actionSensorId}
        </span>

        <span>
          <strong>Location:</strong>{" "}
          {recommendation.sourceLocation || "Unknown"}
        </span>

        <span>
          <strong>Matching:</strong>{" "}
          {recommendation.matchScope === "SameSensor"
            ? "Same sensor"
            : recommendation.matchScope === "SameLocation"
              ? "Same registered location"
              : "Historical relationship"}
        </span>
      </div>

      <div className="sx-rec-score-section">
        <div className="sx-rec-score-heading">
          <div>
            <span>Ranking score</span>

            <strong>
              {formatNumber(score)}
              <small> / 100</small>
            </strong>
          </div>

          <div className="sx-rec-evidence-level">
            <span>Evidence classification</span>
            <strong>
              {recommendation.evidenceLevel || "Unknown"}
            </strong>
          </div>
        </div>

        <div
          className="sx-rec-score-track"
          role="progressbar"
          aria-label={`Ranking score for recommendation ${recommendation.rank}`}
          aria-valuenow={score}
          aria-valuemin={0}
          aria-valuemax={100}
        >
          <div
            className="sx-rec-score-fill"
            style={{ width: `${score}%` }}
          />
        </div>
      </div>

      <div className="sx-rec-facts">
        <div>
          <span>Matched episodes</span>

          <strong>
            {recommendation.matchedEpisodes} /{" "}
            {recommendation.observedEpisodes}
          </strong>
        </div>

        <div>
          <span>Follow-up rate</span>

          <strong>
            {formatNumber(
              recommendation.followUpRatePercent
            )}%
          </strong>
        </div>

        <div>
          <span>Latest observation</span>

          <strong>{observedValue}</strong>
        </div>

        <div>
          <span>Suggested state</span>

          <strong>{requestedState}</strong>
        </div>
      </div>

      {recommendation.readiness === "Blocked" && (
        <div className="sx-rec-blocked-message">
          <strong>Not ready for action</strong>
          <p>
            One or more current-state or data-quality checks
            failed. Review the limitations below.
          </p>
        </div>
      )}

      <details className="sx-rec-expandable">
        <summary>
          <span>View evidence and score explanation</span>
          <span aria-hidden="true">⌄</span>
        </summary>

        <div className="sx-rec-expanded-content">
          <ScoreBreakdown
            recommendation={recommendation}
          />

          <EvidenceSection
            title="Historical evidence"
            items={recommendation.evidence}
          />

          <EvidenceSection
            title="Current checks"
            items={recommendation.checks}
          />

          <EvidenceSection
            title="Limitations and safety"
            items={recommendation.limitations}
            className="sx-rec-limitations"
          />

          <div className="sx-rec-last-action">
            <span>Most recent retained matching action</span>

            <strong>
              {formatDate(
                recommendation.lastHistoricalActionAtUtc
              )}
            </strong>
          </div>
        </div>
      </details>

      <div className="sx-rec-card-footer">
        <div className="sx-rec-advisory-indicator">
          <span aria-hidden="true">◎</span>
          Advisory only
        </div>

        <Link
          className="sx-rec-review-link"
          to={reviewLink}
          aria-label={`Review sensor ${recommendation.actionSensorId} in Command Centre`}
        >
          Review in Command Centre
          <span aria-hidden="true">↗</span>
        </Link>
      </div>
    </article>
  );
}

export default function SuggestedActions() {
  const [draftFilters, setDraftFilters] = useState({
    days: "7",
    windowMinutes: "30",
    sensorId: "",
  });

  const [activeFilters, setActiveFilters] = useState({
    days: 7,
    windowMinutes: 30,
    sensorId: null,
  });

  const [report, setReport] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [filterError, setFilterError] = useState("");
  const [lastUpdated, setLastUpdated] = useState(null);
  const [refreshVersion, setRefreshVersion] = useState(0);

  const [readinessFilter, setReadinessFilter] =
    useState("all");

  const [conditionFilter, setConditionFilter] =
    useState("all");

  const [search, setSearch] = useState("");
  const [sortMode, setSortMode] = useState("rank");

  useEffect(() => {
    const controller = new AbortController();
    let active = true;
    let requestInProgress = false;

    async function loadRecommendations() {
      if (requestInProgress) {
        return;
      }

      requestInProgress = true;
      setLoading(true);

      try {
        const result = await getRankedRecommendations({
          ...activeFilters,
          signal: controller.signal,
        });

        if (!Array.isArray(result?.recommendations)) {
          throw new Error(
            "The recommendation API returned an unexpected response."
          );
        }

        if (active) {
          setReport(result);
          setError("");
          setLastUpdated(new Date());
        }
      } catch (requestError) {
        if (active && !controller.signal.aborted) {
          setError(
            getCommandErrorMessage(requestError)
          );
        }
      } finally {
        requestInProgress = false;

        if (active) {
          setLoading(false);
        }
      }
    }

    loadRecommendations();

    const interval = setInterval(
      loadRecommendations,
      REFRESH_INTERVAL
    );

    return () => {
      active = false;
      clearInterval(interval);
      controller.abort();
    };
  }, [activeFilters, refreshVersion]);

  function handleApplyFilters(event) {
    event.preventDefault();

    const rawSensorId = draftFilters.sensorId.trim();

    let sensorId = null;

    if (rawSensorId) {
      const parsed = Number(rawSensorId);

      if (
        !/^[1-9]\d*$/.test(rawSensorId) ||
        !Number.isSafeInteger(parsed)
      ) {
        setFilterError(
          "Sensor ID must be a positive whole number."
        );

        return;
      }

      sensorId = parsed;
    }

    setFilterError("");

    setActiveFilters({
      days: Number(draftFilters.days),
      windowMinutes: Number(draftFilters.windowMinutes),
      sensorId,
    });
  }

  const recommendations = useMemo(
    () => asArray(report?.recommendations),
    [report]
  );

  const filteredRecommendations = useMemo(() => {
    const query = search.trim().toLowerCase();

    const results = recommendations.filter(
      (recommendation) => {
        if (
          readinessFilter !== "all" &&
          recommendation.readiness !== readinessFilter
        ) {
          return false;
        }

        if (
          conditionFilter !== "all" &&
          recommendation.condition !== conditionFilter
        ) {
          return false;
        }

        if (!query) {
          return true;
        }

        return [
          recommendation.id,
          recommendation.condition,
          getConditionLabel(recommendation.condition),
          recommendation.suggestedAction,
          recommendation.sourceSensorId,
          recommendation.actionSensorId,
          recommendation.sourceLocation,
          recommendation.target,
          recommendation.evidenceLevel,
        ].some((value) =>
          String(value ?? "")
            .toLowerCase()
            .includes(query)
        );
      }
    );

    return [...results].sort((left, right) => {
      if (sortMode === "score") {
        return (
          Number(right.score) - Number(left.score) ||
          Number(left.rank) - Number(right.rank)
        );
      }

      if (sortMode === "matches") {
        return (
          Number(right.matchedEpisodes) -
            Number(left.matchedEpisodes) ||
          Number(left.rank) - Number(right.rank)
        );
      }

      if (sortMode === "recent") {
        return (
          new Date(
            right.lastHistoricalActionAtUtc || 0
          ).getTime() -
            new Date(
              left.lastHistoricalActionAtUtc || 0
            ).getTime() ||
          Number(left.rank) - Number(right.rank)
        );
      }

      return Number(left.rank) - Number(right.rank);
    });
  }, [
    recommendations,
    readinessFilter,
    conditionFilter,
    search,
    sortMode,
  ]);

  return (
    <main className="sx-rec-page">
      <header className="sx-rec-page-header">
        <div>
          <div className="sx-rec-eyebrow">
            SMART-X / OPERATOR INTELLIGENCE
          </div>

          <h1>Suggested Actions</h1>

          <p>
            Explainable recommendations from historical
            telemetry, operator behaviour and current
            device-state checks.
          </p>
        </div>

        <div className="sx-rec-header-actions">
          <span className="sx-rec-live-indicator">
            <span />
            Refreshes every 60 seconds
          </span>

          <button
            type="button"
            className="sx-rec-refresh-button"
            disabled={loading}
            onClick={() =>
              setRefreshVersion((value) => value + 1)
            }
          >
            ↻ {loading ? "Refreshing..." : "Refresh"}
          </button>
        </div>
      </header>

      <section className="sx-rec-advisory-banner">
        <span className="sx-rec-advisory-icon">◎</span>

        <div>
          <strong>Human-supervised recommendations</strong>

          <p>
            Smart-X does not automatically execute suggested
            actions. Ranking scores are heuristic and do not
            establish that an action is safe or effective.
            Operators must independently review conditions
            before using the Command Centre.
          </p>
        </div>
      </section>

      <form
        className="sx-rec-analysis-form"
        onSubmit={handleApplyFilters}
      >
        <div className="sx-rec-form-field">
          <label htmlFor="sx-rec-days">
            Historical period
          </label>

          <select
            id="sx-rec-window"
            value={draftFilters.windowMinutes}
            onChange={(event) =>
              setDraftFilters((previous) => ({
                ...previous,
                windowMinutes: event.target.value,
              }))
            }
          >
            <option value="1">1 minute (Quick test)</option>
            <option value="5">5 minutes</option>
            <option value="15">15 minutes</option>
            <option value="30">30 minutes</option>
            <option value="60">60 minutes</option>
            <option value="120">120 minutes</option>
          </select>
        </div>

        <div className="sx-rec-form-field">
          <label htmlFor="sx-rec-window">
            Follow-up window
          </label>

          <select
            id="sx-rec-window"
            value={draftFilters.windowMinutes}
            onChange={(event) =>
              setDraftFilters((previous) => ({
                ...previous,
                windowMinutes: event.target.value,
              }))
            }
          >
            <option value="5">5 minutes</option>
            <option value="15">15 minutes</option>
            <option value="30">30 minutes</option>
            <option value="60">60 minutes</option>
            <option value="120">120 minutes</option>
          </select>
        </div>

        <div className="sx-rec-form-field">
          <label htmlFor="sx-rec-sensor">
            Source sensor ID
          </label>

          <input
            id="sx-rec-sensor"
            type="text"
            inputMode="numeric"
            placeholder="All sensors"
            value={draftFilters.sensorId}
            onChange={(event) =>
              setDraftFilters((previous) => ({
                ...previous,
                sensorId: event.target.value,
              }))
            }
          />
        </div>

        <button
          className="sx-rec-apply-button"
          type="submit"
        >
          Analyse history
        </button>

        {filterError && (
          <p className="sx-rec-form-error" role="alert">
            {filterError}
          </p>
        )}
      </form>

      {error && (
        <div className="sx-rec-error" role="alert">
          <strong>Recommendations could not be refreshed.</strong>

          <p>{error}</p>

          {report && (
            <p>
              The results below are from the last successful
              request and may be outdated.
            </p>
          )}
        </div>
      )}

      {report?.dataLimitReached && (
        <div className="sx-rec-data-warning" role="status">
          <strong>Historical data limit reached</strong>

          <p>
            Some historical records were excluded by the
            backend's query limits. Recommendations may be
            incomplete, and readiness should remain blocked
            until data quality is resolved.
          </p>
        </div>
      )}

      <section
        className="sx-rec-stats"
        aria-label="Recommendation summary"
      >
        <div className="sx-rec-stat">
          <span>Recommendations</span>
          <strong>{recommendations.length}</strong>
          <small>Returned by the API</small>
        </div>

        <div className="sx-rec-stat sx-rec-stat-review">
          <span>Ready for review</span>
          <strong>{report?.reviewCount ?? "—"}</strong>
          <small>Operator assessment required</small>
        </div>

        <div className="sx-rec-stat sx-rec-stat-monitor">
          <span>Monitor</span>
          <strong>{report?.monitorCount ?? "—"}</strong>
          <small>Further observation needed</small>
        </div>

        <div className="sx-rec-stat sx-rec-stat-blocked">
          <span>Blocked</span>
          <strong>{report?.blockedCount ?? "—"}</strong>
          <small>Current checks not satisfied</small>
        </div>
      </section>

      <div className="sx-rec-results-header">
        <div>
          <h2>Ranked suggestions</h2>

          <p>
            {filteredRecommendations.length} of{" "}
            {recommendations.length} suggestions shown
          </p>
        </div>

        <div className="sx-rec-updated">
          Last successful update:{" "}
          {formatDate(lastUpdated)}
        </div>
      </div>

      <div className="sx-rec-list-filters">
        <input
          type="search"
          aria-label="Search suggestions"
          value={search}
          onChange={(event) =>
            setSearch(event.target.value)
          }
          placeholder="Search action, condition or sensor"
        />

        <select
          aria-label="Filter recommendation readiness"
          value={readinessFilter}
          onChange={(event) =>
            setReadinessFilter(event.target.value)
          }
        >
          <option value="all">All readiness states</option>
          <option value="Review">Review</option>
          <option value="Monitor">Monitor</option>
          <option value="Blocked">Blocked</option>
        </select>

        <select
          aria-label="Filter anomaly condition"
          value={conditionFilter}
          onChange={(event) =>
            setConditionFilter(event.target.value)
          }
        >
          <option value="all">All conditions</option>
          <option value="LowSoilMoisture">
            Low soil moisture
          </option>
          <option value="HighTemperature">
            High temperature
          </option>
          <option value="PowerSurge">
            Power surge
          </option>
        </select>

        <select
          aria-label="Sort suggestions"
          value={sortMode}
          onChange={(event) =>
            setSortMode(event.target.value)
          }
        >
          <option value="rank">Backend ranking</option>
          <option value="score">Highest score</option>
          <option value="matches">Most matched episodes</option>
          <option value="recent">Most recent action</option>
        </select>
      </div>

      {loading && !report && (
        <div className="sx-rec-empty-state" role="status">
          <span className="sx-rec-empty-symbol">◌</span>

          <h3>Analysing historical activity...</h3>

          <p>
            Retrieving recent telemetry, applied commands
            and recommendation evidence.
          </p>
        </div>
      )}

      {!loading &&
        !report &&
        !error && (
          <div className="sx-rec-empty-state">
            <h3>No recommendation report available</h3>

            <p>Refresh the page to request an analysis.</p>
          </div>
        )}

      {report &&
        recommendations.length === 0 && (
          <div className="sx-rec-empty-state">
            <span className="sx-rec-empty-symbol">◇</span>

            <h3>No historical patterns detected yet</h3>

            <p>
              Smart-X needs anomaly episodes followed by
              successfully applied manual commands before
              it can generate evidence-based suggestions.
            </p>

            <p>
              Continue using the simulator and Command
              Centre, or change the historical period.
            </p>
          </div>
        )}

      {report &&
        recommendations.length > 0 &&
        filteredRecommendations.length === 0 && (
          <div className="sx-rec-empty-state">
            <h3>No matching suggestions</h3>

            <p>
              Try changing the readiness filter, condition
              or search text.
            </p>
          </div>
        )}

      {filteredRecommendations.length > 0 && (
        <div className="sx-rec-card-grid">
          {filteredRecommendations.map(
            (recommendation) => (
              <RecommendationCard
                key={recommendation.id}
                recommendation={recommendation}
              />
            )
          )}
        </div>
      )}

      <footer className="sx-rec-page-footer">
        <p>
          Historical patterns analysed:{" "}
          <strong>
            {report?.historicalPatterns ?? "—"}
          </strong>
        </p>

        <p>
          Analysis window:{" "}
          <strong>
            {report?.windowMinutes ??
              activeFilters.windowMinutes}{" "}
            minutes
          </strong>
        </p>

        {report && (
          <p>
            Historical period:{" "}
            {formatDate(report.historicalFromUtc)} —{" "}
            {formatDate(report.historicalToUtc)}
          </p>
        )}

        <p>
          {report?.disclaimer ||
            "Advisory analysis only. No commands are executed."}
        </p>
      </footer>
    </main>
  );
}
