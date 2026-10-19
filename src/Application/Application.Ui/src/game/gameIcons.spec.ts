import { describe, expect, it } from "vitest";
import names from "./gameIconNames.json";
import bodies from "./gameIcons.json";
import { GAME_ICON_PREFIX, gameIconBody, gameIconName, iconSource } from "./gameIcons";

describe("the game icons the game ships", () => {
  it("are listed once each", () => {
    expect(new Set(names).size).toBe(names.length);
  });

  it("are all drawn: the generated file has a body for every name on the list, and nothing else", () => {
    expect(Object.keys(bodies.icons).sort()).toEqual([...names].sort());
    for (const body of Object.values(bodies.icons)) expect(body).toContain("<path");
  });

  it("are the size the bodies are drawn in", () => {
    expect([bodies.width, bodies.height]).toEqual([512, 512]);
  });

  it("have bodies with nothing in them that could run: no scripts, no handlers", () => {
    for (const body of Object.values(bodies.icons)) expect(body).not.toMatch(/<script|on\w+=|javascript:/i);
  });
});

describe("gameIconName", () => {
  it("is the name of a game icon without its prefix, or null", () => {
    expect(gameIconName(`${GAME_ICON_PREFIX}arrow-cluster`)).toBe("arrow-cluster");
    expect(gameIconName("mdi-shield")).toBeNull();
    expect(gameIconName("game-icons:")).toBeNull();
    expect(gameIconName("")).toBeNull();
    expect(gameIconName(null)).toBeNull();
  });
});

describe("gameIconBody", () => {
  it("is the body of a game icon we ship", () => {
    expect(gameIconBody("game-icons:magnet")).toContain("<path");
  });

  it("is nothing for one we do not ship, or something else", () => {
    expect(gameIconBody("game-icons:not-an-icon")).toBeNull();
    expect(gameIconBody("mdi-magnet")).toBeNull();
  });
});

describe("iconSource", () => {
  it("says to draw a game icon from its body", () => {
    expect(iconSource("game-icons:magnet")).toEqual({ kind: "game", body: gameIconBody("game-icons:magnet") });
  });

  it("says to draw a Material Design icon by its name", () => {
    expect(iconSource("mdi-skull")).toEqual({ kind: "mdi", name: "mdi-skull" });
  });

  it("falls back to a Material Design star for a game icon we do not ship, so a perk is never shown without one", () => {
    expect(iconSource("game-icons:not-an-icon")).toEqual({ kind: "mdi", name: "mdi-star-four-points" });
    expect(iconSource("")).toEqual({ kind: "mdi", name: "mdi-star-four-points" });
  });
});
