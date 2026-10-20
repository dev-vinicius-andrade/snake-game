import { describe, expect, it, vi } from "vitest";
import { onDetectTiming, reportDetectTiming } from "./detectTimings";

describe("detectTimings", () => {
  it("tells whoever listens how long a detection took, and when", () => {
    const heard = vi.fn();
    const stop = onDetectTiming(heard);

    reportDetectTiming(1000, 6.5);

    expect(heard).toHaveBeenCalledWith(1000, 6.5);
    stop();
  });

  it("stops telling after the listener leaves", () => {
    const heard = vi.fn();
    onDetectTiming(heard)();

    reportDetectTiming(1000, 6.5);

    expect(heard).not.toHaveBeenCalled();
  });

  it("does not mind that nobody listens", () => {
    expect(() => reportDetectTiming(1000, 6.5)).not.toThrow();
  });
});
