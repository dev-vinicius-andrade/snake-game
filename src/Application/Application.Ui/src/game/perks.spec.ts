import { describe, expect, it } from "vitest";
import { perkLabel } from "./perks";

describe("perkLabel", () => {
  it("turns an id into words", () => {
    expect(perkLabel("twin-shot")).toBe("Twin shot");
    expect(perkLabel("swift")).toBe("Swift");
  });

  it("copes with underscores and spaces", () => {
    expect(perkLabel("deep_lungs")).toBe("Deep lungs");
    expect(perkLabel("  quick reload ")).toBe("Quick reload");
  });

  it("gives an empty id back as it is", () => {
    expect(perkLabel("")).toBe("");
  });
});
