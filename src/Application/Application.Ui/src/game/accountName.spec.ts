import { describe, expect, it } from "vitest";
import { cleanUsername, formatDisplay, formatTag, isGuestNicknameAllowed, parseTag } from "./accountName";

describe("parseTag", () => {
  it("reads 1 to 5 digits, with or without #", () => {
    expect(parseTag("09291")).toBe(9291);
    expect(parseTag("9291")).toBe(9291);
    expect(parseTag("#09291")).toBe(9291);
    expect(parseTag(" #0 ")).toBe(0);
    expect(parseTag("99999")).toBe(99999);
  });

  it("refuses anything else", () => {
    for (const bad of ["", "#", "123456", "12a", "-1", "1.5", "##1", null, undefined]) expect(parseTag(bad)).toBeNull();
  });
});

describe("formatTag and formatDisplay", () => {
  it("pads the tag to 5 digits", () => {
    expect(formatTag(9291)).toBe("09291");
    expect(formatTag(0)).toBe("00000");
  });

  it("joins name and tag", () => {
    expect(formatDisplay("Vini", 9291)).toBe("Vini#09291");
  });
});

describe("isGuestNicknameAllowed", () => {
  it("refuses a nickname that ends in # and digits", () => {
    expect(isGuestNicknameAllowed("Vini#09291")).toBe(false);
    expect(isGuestNicknameAllowed("Vini#1")).toBe(false);
    expect(isGuestNicknameAllowed("Vini#09291  ")).toBe(false);
  });

  it("allows the rest", () => {
    expect(isGuestNicknameAllowed("Vini")).toBe(true);
    expect(isGuestNicknameAllowed("Vini#")).toBe(true);
    expect(isGuestNicknameAllowed("#12 Vini")).toBe(true);
    expect(isGuestNicknameAllowed("Vini#12a")).toBe(true);
  });
});

describe("cleanUsername", () => {
  it("trims and collapses spaces", () => {
    expect(cleanUsername("  Big   Snake ")).toBe("Big Snake");
    expect(cleanUsername("")).toBe("");
  });
});
