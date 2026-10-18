
import { beforeEach, describe, expect, it, vi } from "vitest";
import commandClient from "./commandApi";
import {
  getRankedRecommendations,
  recordOperatorSearch,
} from "./insightApi";

vi.mock("./commandApi", () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
  },
}));

describe("Insight API", () => {
  beforeEach(() => {
    vi.mocked(commandClient.get).mockReset();
    vi.mocked(commandClient.post).mockReset();
  });

  it("retrieves recommendations using GET only", async () => {
    const report = {
      reviewCount: 0,
      monitorCount: 0,
      blockedCount: 0,
      recommendations: [],
    };

    vi.mocked(commandClient.get).mockResolvedValue({
      data: report,
    });

    const result = await getRankedRecommendations({
      days: 7,
      windowMinutes: 30,
    });

    expect(commandClient.get).toHaveBeenCalledWith(
      "/insights/recommendations",
      expect.objectContaining({
        params: {
          days: 7,
          windowMinutes: 30,
        },
      })
    );

    expect(commandClient.post).not.toHaveBeenCalled();
    expect(result).toEqual(report);
  });

  it("includes the source sensor filter", async () => {
    vi.mocked(commandClient.get).mockResolvedValue({
      data: {
        recommendations: [],
      },
    });

    await getRankedRecommendations({
      days: 1,
      windowMinutes: 5,
      sensorId: 15,
    });

    expect(commandClient.get).toHaveBeenCalledWith(
      "/insights/recommendations",
      expect.objectContaining({
        params: {
          days: 1,
          windowMinutes: 5,
          sensorId: 15,
        },
      })
    );

    expect(commandClient.post).not.toHaveBeenCalled();
  });

  it("propagates recommendation API failures", async () => {
    vi.mocked(commandClient.get).mockRejectedValue(
      new Error("API unavailable")
    );

    await expect(
      getRankedRecommendations()
    ).rejects.toThrow("API unavailable");
  });

  it("records explicit operator searches separately", async () => {
    vi.mocked(commandClient.post).mockResolvedValue({
      data: {
        searchId: 42,
        query: "pump",
        scope: "sensors",
      },
    });

    const result = await recordOperatorSearch({
      query: "pump",
      scope: "sensors",
      resultCount: 1,
    });

    expect(commandClient.post).toHaveBeenCalledWith(
      "/insights/searches",
      {
        query: "pump",
        scope: "sensors",
        sensorId: null,
        resultCount: 1,
      }
    );

    expect(result.searchId).toBe(42);
  });
});
