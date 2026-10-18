import { describe, expect, it } from "vitest";
import { DAMAGE_COLOR, contrastColor, headColor, segmentColor, shadeColor } from "./segmentColor";

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

describe("headColor", () => {
  const luminance = (hex: string) => {
    const [r, g, b] = [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16) / 255);
    return 0.2126 * r + 0.7152 * g + 0.0722 * b;
  };

  it("is a lighter version of a dark or mid colour", () => {
    expect(headColor("#007700")).not.toBe("#007700");
    expect(luminance(headColor("#007700"))).toBeGreaterThan(luminance("#007700"));
  });

  it("is a darker version of a very light colour, so it still stands out", () => {
    expect(luminance(headColor("#f5f5f5"))).toBeLessThan(luminance("#f5f5f5"));
  });

  it("always differs enough from the body to see", () => {
    ["#00ff00", "#ff0000", "#3366ff", "#ffffff", "#000000", "#ffff00", "#808080"].forEach((body) => {
      expect(Math.abs(luminance(headColor(body)) - luminance(body))).toBeGreaterThan(0.08);
    });
  });

  it("returns a valid hex colour", () => {
    expect(headColor("#00ff00")).toMatch(/^#[0-9a-f]{6}$/);
  });

  it("accepts the short form", () => {
    expect(headColor("#0f0")).toBe(headColor("#00ff00"));
  });

  it.each(["red", "", "#12"])("returns an unparsable colour %j unchanged", (base) => {
    expect(headColor(base)).toBe(base);
  });
});

describe("shadeColor", () => {
  it("darkens by the factor", () => {
    expect(shadeColor("#80c0ff", 0.5)).toBe("#4060 80".replace(" ", ""));
  });

  it("leaves a colour alone at factor 1", () => {
    expect(shadeColor("#80c0ff", 1)).toBe("#80c0ff");
  });

  it("clamps the factor into 0..1", () => {
    expect(shadeColor("#80c0ff", 3)).toBe("#80c0ff");
    expect(shadeColor("#80c0ff", -1)).toBe("#000000");
  });

  it("accepts the short form", () => {
    expect(shadeColor("#fff", 0.5)).toBe("#808080");
  });

  it.each(["red", "", "#12"])("returns an unparsable colour %j unchanged", (base) => {
    expect(shadeColor(base, 0.5)).toBe(base);
  });
});

describe("contrastColor", () => {
  it("is dark on a light colour and light on a dark colour", () => {
    expect(contrastColor("#f0f0f0")).toBe("#0b0d17");
    expect(contrastColor("#101010")).toBe("#ffffff");
  });

  it("treats a bright green as light", () => {
    expect(contrastColor("#00ff00")).toBe("#0b0d17");
  });

  it("falls back to dark for an unparsable colour", () => {
    expect(contrastColor("nope")).toBe("#0b0d17");
  });
});
