import { describe, expect, it } from "vitest";
import { perfModeOf, resolvePerfMode } from "./perfMode";

function memory(initial?: string): Pick<Storage, "getItem" | "setItem" | "removeItem"> {
  const items = new Map<string, string>(initial ? [["snake.perf", initial]] : []);
  return {
    getItem: (key) => items.get(key) ?? null,
    setItem: (key, value) => void items.set(key, value),
    removeItem: (key) => void items.delete(key),
  };
}

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

  it("turns it off with ?perf=off", () => {
    expect(perfModeOf("?perf=off")).toBe("off");
  });
});

describe("resolvePerfMode", () => {
  it("remembers the mode of the address for the rest of the tab, since going to the game drops the query", () => {
    const storage = memory();

    expect(resolvePerfMode("?perf=hide", storage)).toBe("hide");
    expect(resolvePerfMode("", storage)).toBe("hide");
  });

  it("is off when nothing was asked for", () => {
    expect(resolvePerfMode("", memory())).toBe("off");
  });

  it("forgets it with ?perf=off", () => {
    const storage = memory("show");

    expect(resolvePerfMode("?perf=off", storage)).toBe("off");
    expect(resolvePerfMode("", storage)).toBe("off");
  });

  it("works without storage", () => {
    expect(resolvePerfMode("?perf", null)).toBe("show");
    expect(resolvePerfMode("", null)).toBe("off");
  });
});
