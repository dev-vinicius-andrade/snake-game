import { describe, expect, it } from "vitest";
import { TrackingMonitor } from "./trackingMonitor";

describe("TrackingMonitor", () => {
  it("starts out lost, because no face was seen yet", () => {
    expect(new TrackingMonitor().state).toBe("lost");
  });

  it("starts tracking once a face was present long enough", () => {
    const monitor = new TrackingMonitor({ recoverAfterMs: 150 });

    expect(monitor.update(true, 0)).toBeNull();
    expect(monitor.update(true, 100)).toBeNull();
    expect(monitor.update(true, 150)).toBe("tracking");
    expect(monitor.state).toBe("tracking");
  });

  it("does not start tracking on a single stray detection", () => {
    const monitor = new TrackingMonitor({ recoverAfterMs: 150 });

    monitor.update(true, 0);
    monitor.update(false, 50);

    expect(monitor.update(true, 100)).toBeNull();
    expect(monitor.update(true, 200)).toBeNull();
    expect(monitor.update(true, 250)).toBe("tracking");
  });

  it("only reports lost after the face was gone for the whole delay", () => {
    const monitor = new TrackingMonitor({ recoverAfterMs: 0, lostAfterMs: 600 });
    monitor.update(true, 0);

    expect(monitor.update(false, 100)).toBeNull();
    expect(monitor.update(false, 699)).toBeNull();
    expect(monitor.update(false, 700)).toBe("lost");
    expect(monitor.state).toBe("lost");
  });

  it("forgives a short dropout", () => {
    const monitor = new TrackingMonitor({ recoverAfterMs: 0, lostAfterMs: 600 });
    monitor.update(true, 0);

    monitor.update(false, 100);
    monitor.update(true, 400);

    expect(monitor.update(false, 800)).toBeNull();
    expect(monitor.state).toBe("tracking");
  });

  it("reports each change once", () => {
    const monitor = new TrackingMonitor({ recoverAfterMs: 0, lostAfterMs: 100 });
    const changes = [
      monitor.update(true, 0),
      monitor.update(true, 10),
      monitor.update(false, 20),
      monitor.update(false, 200),
      monitor.update(false, 300),
      monitor.update(true, 400),
    ];

    expect(changes).toEqual(["tracking", null, null, "lost", null, "tracking"]);
  });

  it("goes back to lost on reset", () => {
    const monitor = new TrackingMonitor({ recoverAfterMs: 0 });
    monitor.update(true, 0);

    monitor.reset();

    expect(monitor.state).toBe("lost");
  });

  it.each([{ lostAfterMs: -1 }, { recoverAfterMs: -1 }])("rejects the options %j", (options) => {
    expect(() => new TrackingMonitor(options)).toThrow(RangeError);
  });
});
