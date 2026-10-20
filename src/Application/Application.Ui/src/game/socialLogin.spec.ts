import { describe, expect, it } from "vitest";
import en from "@/locales/messages/en";
import pt from "@/locales/messages/pt";
import { contrastRatio, OAUTH_ERRORS, providerIcon, providerLook, readOAuthOutcome } from "./socialLogin";

describe("readOAuthOutcome", () => {
  it("reads the ticket the manager sent the browser back with", () => {
    expect(readOAuthOutcome({ ticket: "abc123" })).toEqual({ kind: "ticket", ticket: "abc123" });
  });

  it("reads a reason it failed, and falls back to a general one for a reason it does not know", () => {
    expect(readOAuthOutcome({ error: "cancelled" })).toEqual({ kind: "error", error: "cancelled" });
    expect(readOAuthOutcome({ error: "<script>" })).toEqual({ kind: "error", error: "providerFailed" });
  });

  it("is nothing without either, and a ticket wins over an error", () => {
    expect(readOAuthOutcome({})).toEqual({ kind: "none" });
    expect(readOAuthOutcome({ ticket: "t", error: "cancelled" })).toEqual({ kind: "ticket", ticket: "t" });
  });

  it("takes the first when the address repeats a value, and ignores a blank one", () => {
    expect(readOAuthOutcome({ ticket: ["a", "b"] })).toEqual({ kind: "ticket", ticket: "a" });
    expect(readOAuthOutcome({ ticket: "  " })).toEqual({ kind: "none" });
  });
});

describe("providerIcon", () => {
  it("has an icon for each provider the manager knows, and a generic one for another", () => {
    for (const id of ["github", "discord", "google", "steam"]) expect(providerIcon(id)).toMatch(/^mdi-/);
    expect(providerIcon("somethingnew")).toBe("mdi-account-circle");
  });
});

describe("providerLook", () => {
  it("makes GitHub's button black with white text", () => {
    expect(providerLook("github")).toMatchObject({ background: "#000000", text: "#ffffff" });
  });

  it("makes Discord's button its purple with white text", () => {
    expect(providerLook("discord")).toMatchObject({ background: "#5865f2", text: "#ffffff" });
  });

  it("makes Google's button white with dark text and a border, as Google asks", () => {
    expect(providerLook("google")).toMatchObject({ background: "#ffffff", text: "#1f1f1f" });
    expect(providerLook("google").border).not.toBe(providerLook("google").background);
  });

  it("makes Steam's button its dark blue", () => {
    expect(providerLook("steam")).toMatchObject({ background: "#171a21" });
  });

  it("is readable: every button has at least the contrast the guidelines ask for, normal and hovered", () => {
    for (const id of ["github", "discord", "google", "steam"]) {
      const look = providerLook(id);
      expect(contrastRatio(look.text, look.background), id).toBeGreaterThanOrEqual(4.5);
      expect(contrastRatio(look.text, look.hover), `${id} hovered`).toBeGreaterThanOrEqual(4.5);
    }
  });

  it("gives another provider a neutral look that is readable too", () => {
    const look = providerLook("somethingnew");

    expect(contrastRatio(look.text, look.background)).toBeGreaterThanOrEqual(4.5);
  });
});

describe("contrastRatio", () => {
  it("is 21 for black on white, 1 for the same colour, and does not mind the order", () => {
    expect(contrastRatio("#000000", "#ffffff")).toBeCloseTo(21, 0);
    expect(contrastRatio("#336699", "#336699")).toBeCloseTo(1, 5);
    expect(contrastRatio("#ffffff", "#000000")).toBeCloseTo(contrastRatio("#000000", "#ffffff"), 5);
  });
});

describe("the privacy note", () => {
  it("says in both languages that only a hash of the login identifier and the display name are kept", () => {
    for (const messages of [en, pt]) {
      const note = (messages as any).account.socialPrivacy as string;
      expect(note).toBeTruthy();
      expect(note.toLowerCase()).toMatch(/hash/);
    }
  });
});

describe("the texts", () => {
  it("say something for every reason a sign in can fail, in both languages", () => {
    for (const messages of [en, pt]) {
      const texts = (messages as any).account.oauthErrors;
      for (const error of OAUTH_ERRORS) expect(texts[error], error).toBeTruthy();
    }
  });
});
