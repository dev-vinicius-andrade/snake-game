import { describe, expect, it } from "vitest";
import { completionsFor, SCRIPT_MEMBERS } from "./scriptCompletions";

const labels = (found: ReturnType<typeof completionsFor>) => found?.options.map((option) => option.label) ?? [];

describe("completionsFor", () => {
  it("offers what a snake holds after snake.", () => {
    const found = completionsFor("function modify(snake, room) {\n  return snake.");

    expect(labels(found)).toEqual(SCRIPT_MEMBERS.snake.map((member) => member.label));
    expect(found?.from).toBe("function modify(snake, room) {\n  return snake.".length);
  });

  it("starts at the word being typed", () => {
    const before = "var x = room.pla";

    expect(completionsFor(before)?.from).toBe(before.length - 3);
    expect(labels(completionsFor(before))).toContain("players");
  });

  it.each(["room", "food", "victim"])("knows what %s holds", (name) => {
    expect(labels(completionsFor(`${name}.`))).toEqual(SCRIPT_MEMBERS[name].map((member) => member.label));
  });

  it("knows the clock, the age, the kills and the state of a snake", () => {
    expect(labels(completionsFor("snake."))).toEqual(expect.arrayContaining(["now", "ageMs", "kills", "state"]));
  });

  it("offers the effects a modify may return, inside return { }", () => {
    const found = completionsFor("function modify(snake) {\n  return { ");

    expect(labels(found)).toEqual(expect.arrayContaining(["extraShots", "invulnerable", "freeShots"]));
    expect(labels(found)).not.toContain("mass");
  });

  it("offers mass and heal for onEat and onKill, and dropPrize for onTick", () => {
    expect(labels(completionsFor("function onEat(s, f) { return { "))).toEqual(["mass", "heal"]);
    expect(labels(completionsFor("function onKill(s, v) { return { ma"))).toEqual(["mass", "heal"]);
    expect(labels(completionsFor("function onTick(r) { return { "))).toEqual(["dropPrize"]);
  });

  it("offers keys again after a comma, but not while a value is written", () => {
    expect(labels(completionsFor("function modify() { return { extraShots: 1, "))).toContain("magnetRadius");
    expect(completionsFor("function modify() { return { extraShots: ")).toBeNull();
  });

  it("offers nothing once the object is closed", () => {
    expect(completionsFor("function modify() { return { extraShots: 1 }; }\nvar a = ")).toBeNull();
  });

  it("offers the four functions after function, with the lines that start them", () => {
    const found = completionsFor("function ");

    expect(labels(found)).toEqual(["modify", "onEat", "onKill", "onTick"]);
    expect(found?.options[2].apply).toContain("onKill(snake, victim, room)");
  });

  it("offers nothing in other places", () => {
    expect(completionsFor("var a = 1 + ")).toBeNull();
    expect(completionsFor("")).toBeNull();
  });

  it("adds the colon to a key it inserts", () => {
    expect(completionsFor("function modify() { return { ")?.options[0].apply).toBe("extraShots: ");
  });
});
