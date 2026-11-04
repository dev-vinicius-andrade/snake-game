import { beforeEach, describe, expect, it } from "vitest";
import { PERF_SWITCHES, perfSwitches, togglePerfSwitch } from "./perfSwitches";

describe("perfSwitches", () => {
  beforeEach(() => PERF_SWITCHES.forEach((name) => (perfSwitches[name] = true)));

  it("has everything on", () => {
    expect(PERF_SWITCHES.every((name) => perfSwitches[name])).toBe(true);
  });

  it("turns one part off and on again", () => {
    togglePerfSwitch("perks");
    expect(perfSwitches.perks).toBe(false);
    expect(perfSwitches.touch).toBe(true);

    togglePerfSwitch("perks");
    expect(perfSwitches.perks).toBe(true);
  });
});
