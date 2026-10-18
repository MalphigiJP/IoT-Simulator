
import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import SuggestedActions from "./SuggestedActions";
import { getRankedRecommendations } from "../api/insightApi";

vi.mock("../api/insightApi", () => ({
  getRankedRecommendations: vi.fn(),
}));

vi.mock("../api/commandApi", () => ({
  getCommandErrorMessage: vi.fn(
    (error) => error?.message || "Request failed"
  ),
}));

function recommendation(overrides = {}) {
  return {
    id: "PowerSurge:1:2:Relay:Off",
    rank: 1,
    condition: "PowerSurge",
    sourceSensorId: 1,
    actionSensorId: 2,
    sourceLocation: "Greenhouse A",
    target: "Relay",
    requestedState: false,
    suggestedAction: "Consider turning off the relay",
    readiness: "Review",
    score: 78.3,
    evidenceLevel: "Repeated",
    observedEpisodes: 5,
    matchedEpisodes: 3,
    followUpRatePercent: 60,
    relatedSearches: 2,
    lastHistoricalActionAtUtc: "2026-10-18T08:00:00Z",
    currentObservedValue: 6501,
    unit: "W",
    matchScope: "SameLocation",
    scoreBreakdown: {
      repetition: 15,
      followUpRate: 15,
      recency: 15,
      searchContext: 3.3,
      currentAnomaly: 20,
      actuatorReadiness: 10,
    },
    evidence: [
      "Three historical episodes had matching actions.",
    ],
    checks: [
      "Recent telemetry is available.",
    ],
    limitations: [
      "Historical correlation does not establish causation.",
    ],
    ...overrides,
  };
}

function makeReport(items = []) {
  return {
    generatedAtUtc: "2026-10-18T09:00:00Z",
    historicalFromUtc: "2026-10-11T09:00:00Z",
    historicalToUtc: "2026-10-18T09:00:00Z",
    windowMinutes: 30,
    historicalPatterns: items.length,
    reviewCount: items.filter(
      (item) => item.readiness === "Review"
    ).length,
    monitorCount: items.filter(
      (item) => item.readiness === "Monitor"
    ).length,
    blockedCount: items.filter(
      (item) => item.readiness === "Blocked"
    ).length,
    dataLimitReached: false,
    disclaimer: "Advisory only. No commands are executed.",
    recommendations: items,
  };
}

function sampleRecommendations() {
  return [
    recommendation(),
    recommendation({
      id: "LowSoilMoisture:3:4:Pump:On",
      rank: 2,
      condition: "LowSoilMoisture",
      sourceSensorId: 3,
      actionSensorId: 4,
      target: "Pump",
      requestedState: true,
      suggestedAction: "Consider starting the pump",
      readiness: "Monitor",
      score: 55,
      matchedEpisodes: 1,
      observedEpisodes: 4,
      followUpRatePercent: 25,
      unit: "%",
      currentObservedValue: 7,
    }),
    recommendation({
      id: "HighTemperature:5:6:Valve:Off",
      rank: 3,
      condition: "HighTemperature",
      sourceSensorId: 5,
      actionSensorId: 6,
      target: "Valve",
      requestedState: false,
      suggestedAction: "Consider closing the valve",
      readiness: "Blocked",
      score: 23,
      matchedEpisodes: 1,
      observedEpisodes: 5,
      unit: "°C",
      currentObservedValue: null,
      limitations: [
        "The actuator has no sufficiently recent telemetry.",
      ],
    }),
  ];
}

function renderPage() {
  return render(
    <MemoryRouter initialEntries={["/suggestions"]}>
      <SuggestedActions />
    </MemoryRouter>
  );
}

describe("SuggestedActions", () => {
  beforeEach(() => {
    vi.mocked(getRankedRecommendations).mockReset();
    vi.mocked(getRankedRecommendations).mockResolvedValue(
      makeReport()
    );
  });

  it("requests recommendations using the default filters", async () => {
    renderPage();

    await waitFor(() => {
      expect(getRankedRecommendations).toHaveBeenCalledWith(
        expect.objectContaining({
          days: 7,
          windowMinutes: 30,
          sensorId: null,
          signal: expect.any(AbortSignal),
        })
      );
    });
  });

  it("renders a valid empty historical analysis", async () => {
    renderPage();

    expect(
      await screen.findByRole("heading", {
        name: "No historical patterns detected yet",
      })
    ).toBeInTheDocument();

    expect(
      screen.getByText("0 of 0 suggestions shown")
    ).toBeInTheDocument();
  });

  it("displays recommendations in ranked cards", async () => {
    vi.mocked(getRankedRecommendations).mockResolvedValue(
      makeReport(sampleRecommendations())
    );

    renderPage();

    expect(
      await screen.findByRole("heading", {
        name: "Consider turning off the relay",
      })
    ).toBeInTheDocument();

    expect(
      screen.getByRole("heading", {
        name: "Consider starting the pump",
      })
    ).toBeInTheDocument();

    expect(
      screen.getByRole("heading", {
        name: "Consider closing the valve",
      })
    ).toBeInTheDocument();
  });

  it("filters recommendations by readiness", async () => {
    const user = userEvent.setup();

    vi.mocked(getRankedRecommendations).mockResolvedValue(
      makeReport(sampleRecommendations())
    );

    renderPage();

    await screen.findByRole("heading", {
      name: "Consider turning off the relay",
    });

    await user.selectOptions(
      screen.getByRole("combobox", {
        name: "Filter recommendation readiness",
      }),
      "Blocked"
    );

    expect(
      screen.getByRole("heading", {
        name: "Consider closing the valve",
      })
    ).toBeInTheDocument();

    expect(
      screen.queryByRole("heading", {
        name: "Consider turning off the relay",
      })
    ).not.toBeInTheDocument();

    expect(
      screen.getByText("1 of 3 suggestions shown")
    ).toBeInTheDocument();
  });

  it("filters recommendations by anomaly condition", async () => {
    const user = userEvent.setup();

    vi.mocked(getRankedRecommendations).mockResolvedValue(
      makeReport(sampleRecommendations())
    );

    renderPage();

    await screen.findByRole("heading", {
      name: "Consider turning off the relay",
    });

    await user.selectOptions(
      screen.getByRole("combobox", {
        name: "Filter anomaly condition",
      }),
      "LowSoilMoisture"
    );

    expect(
      screen.getByRole("heading", {
        name: "Consider starting the pump",
      })
    ).toBeInTheDocument();

    expect(
      screen.queryByRole("heading", {
        name: "Consider closing the valve",
      })
    ).not.toBeInTheDocument();
  });

  it("searches recommendations by action text", async () => {
    const user = userEvent.setup();

    vi.mocked(getRankedRecommendations).mockResolvedValue(
      makeReport(sampleRecommendations())
    );

    renderPage();

    await screen.findByRole("heading", {
      name: "Consider turning off the relay",
    });

    await user.type(
      screen.getByRole("searchbox", {
        name: "Search suggestions",
      }),
      "pump"
    );

    expect(
      screen.getByRole("heading", {
        name: "Consider starting the pump",
      })
    ).toBeInTheDocument();

    expect(
      screen.getByText("1 of 3 suggestions shown")
    ).toBeInTheDocument();
  });

  it("expands historical evidence and score explanations", async () => {
    const user = userEvent.setup();

    vi.mocked(getRankedRecommendations).mockResolvedValue(
      makeReport([recommendation()])
    );

    renderPage();

    const heading = await screen.findByRole("heading", {
      name: "Consider turning off the relay",
    });

    const card = heading.closest("article");

    expect(card).not.toBeNull();

    await user.click(
      within(card).getByText(
        "View evidence and score explanation"
      )
    );

    expect(
      within(card).getByText("Historical repetition")
    ).toBeInTheDocument();

    expect(
      within(card).getByText(
        "Three historical episodes had matching actions."
      )
    ).toBeInTheDocument();

    expect(
      within(card).getByText(
        "Historical correlation does not establish causation."
      )
    ).toBeInTheDocument();
  });

  it("shows safety information for blocked suggestions", async () => {
    vi.mocked(getRankedRecommendations).mockResolvedValue(
      makeReport([
        recommendation({
          readiness: "Blocked",
          limitations: [
            "The actuator has an outstanding command.",
          ],
        }),
      ])
    );

    renderPage();

    expect(
      await screen.findByText("Not ready for action")
    ).toBeInTheDocument();

    expect(
      screen.getByText(
        /one or more current-state or data-quality checks failed/i
      )
    ).toBeInTheDocument();
  });

  it("provides a device-review link without issuing a command", async () => {
    vi.mocked(getRankedRecommendations).mockResolvedValue(
      makeReport([recommendation()])
    );

    renderPage();

    const link = await screen.findByRole("link", {
      name: "Review sensor 2 in Command Centre",
    });

    expect(link).toHaveAttribute(
      "href",
      "/commands?sensorId=2"
    );

    expect(
      screen.queryByRole("button", {
        name: /execute command/i,
      })
    ).not.toBeInTheDocument();
  });

  it("uses selected historical filters in the next API request", async () => {
    const user = userEvent.setup();

    renderPage();

    await screen.findByRole("heading", {
      name: "No historical patterns detected yet",
    });

    await user.selectOptions(
      screen.getByLabelText("Historical period"),
      "1"
    );

    await user.selectOptions(
      screen.getByLabelText("Follow-up window"),
      "5"
    );

    await user.click(
      screen.getByRole("button", {
        name: "Analyse history",
      })
    );

    await waitFor(() => {
      expect(getRankedRecommendations).toHaveBeenLastCalledWith(
        expect.objectContaining({
          days: 1,
          windowMinutes: 5,
        })
      );
    });
  });

  it("reports API errors without crashing", async () => {
    vi.mocked(getRankedRecommendations).mockRejectedValue(
      new Error("Smart-X API unavailable")
    );

    renderPage();

    expect(
      await screen.findByText("Smart-X API unavailable")
    ).toBeInTheDocument();

    expect(
      screen.getByRole("heading", {
        name: "Suggested Actions",
      })
    ).toBeInTheDocument();
  });

  it("warns when historical data was truncated", async () => {
    vi.mocked(getRankedRecommendations).mockResolvedValue({
      ...makeReport(),
      dataLimitReached: true,
    });

    renderPage();

    expect(
      await screen.findByText("Historical data limit reached")
    ).toBeInTheDocument();
  });
});
