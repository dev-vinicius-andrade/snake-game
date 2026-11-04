import { describe, expect, it } from "vitest";
import { perfModeOf } from "./perfMode";

describe("perfModeOf", () => {
  it("is off without ?perf", () => {
    expect(perfModeOf("")).toBe("off");
    expect(perfModeOf("?room=abc")).toBe("off");
  });

  it("shows the overlay with ?perf", () => {
    expect(perfModeOf("?perf")).toBe("show");
    expect(perfModeOf("?perf=1")).toBe("show");
    expect(perfModeOf("?room=abc&perf")).toBe("show");
  });

  it("keeps measuring but hides the overlay with ?perf=hide", () => {
    expect(perfModeOf("?perf=hide")).toBe("hide");
    expect(perfModeOf("?room=abc&perf=hide")).toBe("hide");
  });
});
