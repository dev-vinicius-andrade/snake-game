import { describe, expect, it } from "vitest";
import { DAMAGE_COLOR, segmentColor } from "./segmentColor";

describe("segmentColor", () => {
  it("returns the base colour at full health", () => {
    expect(segmentColor("#00ff00", 1)).toBe("#00ff00");
  });

  it("returns the damage colour at zero health", () => {
    expect(segmentColor("#00ff00", 0)).toBe(DAMAGE_COLOR);
  });

  it("blends halfway at half health", () => {
    expect(segmentColor("#000000", 0.5, "#ff0000")).toBe("#800000");
  });

  it("moves monotonically toward the damage colour", () => {
    const greens = [1, 0.75, 0.5, 0.25, 0].map((ratio) => parseInt(segmentColor("#00ff00", ratio, "#ff0000").slice(3, 5), 16));

    expect(greens).toEqual([...greens].sort((a, b) => b - a));
  });

  it("accepts shorthand hex", () => {
    expect(segmentColor("#0f0", 1)).toBe("#00ff00");
  });

  it("accepts upper case hex", () => {
    expect(segmentColor("#00FF00", 1)).toBe("#00ff00");
  });

  it("clamps the ratio", () => {
    expect(segmentColor("#00ff00", 3)).toBe("#00ff00");
    expect(segmentColor("#00ff00", -2)).toBe(DAMAGE_COLOR);
  });

  it("treats NaN as full health", () => {
    expect(segmentColor("#00ff00", Number.NaN)).toBe("#00ff00");
  });

  it.each(["red", "rgb(1,2,3)", "", "#12", "#gggggg"])("returns an unparsable base %j unchanged", (base) => {
    expect(segmentColor(base, 0.2)).toBe(base);
  });

  it("falls back to the damage colour when the damage colour is unparsable", () => {
    expect(segmentColor("#00ff00", 0.5, "nope")).toBe("#00ff00");
  });
});
