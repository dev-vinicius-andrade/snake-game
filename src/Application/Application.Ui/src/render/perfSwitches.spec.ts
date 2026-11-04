import { beforeEach, describe, expect, it } from "vitest";
import { PERF_SWITCHES, perfSwitches, presetPerfSwitches, togglePerfSwitch } from "./perfSwitches";

describe("perfSwitches", () => {
  beforeEach(() => PERF_SWITCHES.forEach((name) => (perfSwitches[name] = true)));

  it("has everything on", () => {
    expect(PERF_SWITCHES.every((name) => perfSwitches[name])).toBe(true);
  });

  it("can turn off the drawing of the canvas too", () => {
    expect(PERF_SWITCHES).toContain("draw");
  });

  it("turns one part off and on again", () => {
    togglePerfSwitch("perks");
    expect(perfSwitches.perks).toBe(false);
    expect(perfSwitches.touch).toBe(true);

    togglePerfSwitch("perks");
    expect(perfSwitches.perks).toBe(true);
  });
});

describe("presetPerfSwitches", () => {
  beforeEach(() => PERF_SWITCHES.forEach((name) => (perfSwitches[name] = true)));

  it("turns off the parts the address names in ?off=", () => {
    presetPerfSwitches("?perf=hide&off=perks,draw");

    expect(perfSwitches.perks).toBe(false);
    expect(perfSwitches.draw).toBe(false);
    expect(perfSwitches.hud).toBe(true);
  });

  it("ignores names it does not know and addresses without ?off=", () => {
    presetPerfSwitches("?perf=hide&off=nothing");
    presetPerfSwitches("?perf=hide");

    expect(PERF_SWITCHES.every((name) => perfSwitches[name])).toBe(true);
  });
});
