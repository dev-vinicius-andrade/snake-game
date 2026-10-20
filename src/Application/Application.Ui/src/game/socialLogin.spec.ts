import { describe, expect, it } from "vitest";
import en from "@/locales/messages/en";
import pt from "@/locales/messages/pt";
import { OAUTH_ERRORS, providerIcon, readOAuthOutcome } from "./socialLogin";

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
