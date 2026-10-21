import { describe, expect, it } from "vitest";
import en from "@/locales/messages/en";
import pt from "@/locales/messages/pt";
import { GAMEPLAY_FEATURES, INTENSITY_PRESETS, choicesBody, defaultChoices, isDefaultChoices, parseChoices, presetsOf, toggleKey, toggleOf, withToggle } from "./roomGameplay";

describe("toggles of single things", () => {
  it("are named kind and id, like the server reads them", () => {
    expect(toggleKey("perk", "magnet")).toBe("perk:magnet");
  });

  it("follow the default until the room chooses", () => {
    expect(toggleOf(defaultChoices(), "perk", "magnet")).toBe("inherit");
  });

  it("switch a thing on or off, and back to the default by removing the choice", () => {
    const off = withToggle(defaultChoices(), "perk", "magnet", "off");
    const on = withToggle(off, "event", "maze", "on");

    expect(toggleOf(on, "perk", "magnet")).toBe("off");
    expect(toggleOf(on, "event", "maze")).toBe("on");
    expect(isDefaultChoices(on)).toBe(false);
    const back = withToggle(withToggle(on, "perk", "magnet", "inherit"), "event", "maze", "inherit");
    expect(isDefaultChoices(back)).toBe(true);
    expect("perk:magnet" in back).toBe(false);
  });

  it("do not change the choices they are given", () => {
    const original = defaultChoices();

    withToggle(original, "prize", "gold", "off");

    expect(isDefaultChoices(original)).toBe(true);
  });

  it("are sent with the rest of what differs, as on and off", () => {
    const choices = withToggle(withToggle({ ...defaultChoices(), zone: "calm" }, "reward", "life", "off"), "prize", "gold", "on");

    expect(choicesBody(choices)).toEqual({ zone: "calm", "reward:life": "off", "prize:gold": "on" });
  });

  it("are read back from what the manager says a room does, and only the sane ones", () => {
    const choices = parseChoices({ zone: "off", "perk:magnet": "off", "event:maze": "on", "ghost:x": "on", "perk:y": "maybe", "perk:": "on" });

    expect(toggleOf(choices, "perk", "magnet")).toBe("off");
    expect(toggleOf(choices, "event", "maze")).toBe("on");
    expect("ghost:x" in choices).toBe(false);
    expect("perk:y" in choices).toBe(false);
    expect("perk:" in choices).toBe(false);
    expect(choices.zone).toBe("off");
  });

  it("leave the intensity of an event to the presets that are not off", () => {
    expect(INTENSITY_PRESETS).toEqual(["inherit", "calm", "normal", "wild"]);
  });
});

describe("GAMEPLAY_FEATURES", () => {
  it("are the six the game server knows, in the order they are shown", () => {
    expect(GAMEPLAY_FEATURES.map((feature) => feature.key)).toEqual(["perks", "prizes", "lightning", "meteors", "turret", "zone"]);
  });

  it("offer perks and prizes only on and off, and the events calm and wild as well", () => {
    expect(presetsOf("perks")).toEqual(["inherit", "off", "normal"]);
    expect(presetsOf("prizes")).toEqual(["inherit", "off", "normal"]);
    for (const key of ["lightning", "meteors", "turret", "zone"]) expect(presetsOf(key)).toEqual(["inherit", "off", "calm", "normal", "wild"]);
  });

  it("have a name and a preset text in every language", () => {
    for (const messages of [en, pt]) {
      const picker = (messages as any).gameplayPicker;
      for (const feature of GAMEPLAY_FEATURES) expect(picker.features[feature.key], feature.key).toBeTruthy();
      for (const preset of ["inherit", "off", "calm", "normal", "wild"]) expect(picker.presets[preset], preset).toBeTruthy();
    }
  });
});

describe("defaultChoices", () => {
  it("inherit everything", () => {
    const choices = defaultChoices();

    expect(Object.keys(choices)).toHaveLength(6);
    expect(Object.values(choices).every((preset) => preset === "inherit")).toBe(true);
    expect(isDefaultChoices(choices)).toBe(true);
  });

  it("are a new object each time", () => {
    expect(defaultChoices()).not.toBe(defaultChoices());
  });
});

describe("choicesBody", () => {
  it("is nothing to send for a room that changes nothing", () => {
    expect(choicesBody(defaultChoices())).toBeUndefined();
  });

  it("is only what was changed", () => {
    expect(choicesBody({ ...defaultChoices(), turret: "off", lightning: "wild" })).toEqual({ turret: "off", lightning: "wild" });
  });
});

describe("isDefaultChoices", () => {
  it("is false once one feature is changed", () => {
    expect(isDefaultChoices({ ...defaultChoices(), zone: "calm" })).toBe(false);
  });
});

describe("parseChoices", () => {
  it("reads what the manager sends for a room", () => {
    expect(parseChoices({ perks: "off", prizes: "inherit", lightning: "wild", meteors: "inherit", turret: "calm", zone: "normal" })).toEqual({
      perks: "off",
      prizes: "inherit",
      lightning: "wild",
      meteors: "inherit",
      turret: "calm",
      zone: "normal",
    });
  });

  it("is the default for a room that sends nothing, or something that is not a map", () => {
    for (const raw of [undefined, null, "nope", 7, []]) expect(isDefaultChoices(parseChoices(raw))).toBe(true);
  });

  it("ignores what it does not know, and a preset a feature does not offer", () => {
    const choices = parseChoices({ lightning: "maybe", weather: "wild", perks: "wild", zone: "off" });

    expect(choices.lightning).toBe("inherit");
    expect(choices.perks).toBe("inherit");
    expect(choices.zone).toBe("off");
    expect("weather" in choices).toBe(false);
  });
});
