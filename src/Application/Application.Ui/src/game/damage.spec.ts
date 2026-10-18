import { describe, expect, it } from "vitest";
import { parseDamage } from "./damage";

const raw = { shooterGuid: "s", targetGuid: "t", x: 100, y: 50, damage: 23.4, kind: "hit" };

describe("parseDamage", () => {
  it("reads what the server sends", () => {
    expect(parseDamage(raw)).toEqual({ shooterGuid: "s", targetGuid: "t", x: 100, y: 50, damage: 23.4, kind: "hit" });
  });

  it.each(["hit", "broke", "kill"])("knows the kind %s", (kind) => {
    expect(parseDamage({ ...raw, kind })?.kind).toBe(kind);
  });

  it("treats an unknown kind as a plain hit", () => {
    expect(parseDamage({ ...raw, kind: "mystery" })?.kind).toBe("hit");
  });

  it("needs a position", () => {
    expect(parseDamage({ ...raw, x: undefined })).toBeNull();
    expect(parseDamage({ ...raw, y: "far" })).toBeNull();
  });

  it("needs to know who was involved", () => {
    expect(parseDamage({ ...raw, shooterGuid: undefined })).toBeNull();
    expect(parseDamage({ ...raw, targetGuid: 4 })).toBeNull();
  });

  it("takes a missing or odd damage as none", () => {
    expect(parseDamage({ ...raw, damage: undefined })?.damage).toBe(0);
    expect(parseDamage({ ...raw, damage: -5 })?.damage).toBe(0);
  });

  it.each([null, undefined, 5, "text", []])("returns null for %j", (value) => {
    expect(parseDamage(value)).toBeNull();
  });
});
