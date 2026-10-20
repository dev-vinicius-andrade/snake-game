import { describe, expect, it } from "vitest";
import { cameraSize, detectIntervalMs, MAX_DETECT_INTERVAL_MS, MIN_DETECT_INTERVAL_MS, smoothDetectTime } from "./detectBudget";

describe("detectIntervalMs", () => {
  it("runs as often as the camera delivers while detection is cheap", () => {
    expect(detectIntervalMs(0)).toBe(MIN_DETECT_INTERVAL_MS);
    expect(detectIntervalMs(3)).toBe(MIN_DETECT_INTERVAL_MS);
  });

  it("leaves room between detections in proportion to what one costs, so it takes about a quarter of the time", () => {
    expect(detectIntervalMs(10)).toBe(40);
    expect(detectIntervalMs(20)).toBe(80);
  });

  it("never waits longer than the top, so the face is still followed when detection is slow", () => {
    expect(detectIntervalMs(60)).toBe(MAX_DETECT_INTERVAL_MS);
  });

  it("copes with nonsense", () => {
    expect(detectIntervalMs(Number.NaN)).toBe(MIN_DETECT_INTERVAL_MS);
    expect(detectIntervalMs(-5)).toBe(MIN_DETECT_INTERVAL_MS);
  });
});

describe("smoothDetectTime", () => {
  it("starts from the first measure", () => {
    expect(smoothDetectTime(0, 12)).toBe(12);
  });

  it("follows later measures slowly, so one slow frame does not slow everything", () => {
    expect(smoothDetectTime(10, 60)).toBeCloseTo(20, 5);
    expect(smoothDetectTime(10, 10)).toBe(10);
  });
});

describe("cameraSize", () => {
  it("asks for a small picture for the head, which is big in the frame", () => {
    expect(cameraSize("head")).toEqual({ width: 320, height: 240 });
  });

  it("keeps the bigger picture for the eyes, which are small in the frame", () => {
    expect(cameraSize("eyes")).toEqual({ width: 640, height: 480 });
  });
});
