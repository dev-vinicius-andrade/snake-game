import { describe, expect, it } from "vitest";
import { directionForKey, initialKeyboardState, reduceKey, reduceMouse, releaseAll, type KeyboardState } from "./keyboard";

function press(state: KeyboardState, ...codes: string[]) {
  return codes.reduce(
    (result, code) => {
      const next = reduceKey(result.state, { code, pressed: true });
      return { state: next.state, intents: [...result.intents, ...next.intents] };
    },
    { state, intents: [] as ReturnType<typeof reduceKey>["intents"] }
  );
}

describe("directionForKey", () => {
  it.each([
    ["ArrowUp", "up"],
    ["KeyW", "up"],
    ["ArrowDown", "down"],
    ["KeyS", "down"],
    ["ArrowLeft", "left"],
    ["KeyA", "left"],
    ["ArrowRight", "right"],
    ["KeyD", "right"],
  ])("maps %s to %s", (code, direction) => {
    expect(directionForKey(code)).toBe(direction);
  });

  it.each(["Space", "KeyQ", "Enter", ""])("returns null for %j", (code) => {
    expect(directionForKey(code)).toBeNull();
  });
});

describe("reduceKey directions", () => {
  it("emits a direction intent when a direction key goes down", () => {
    const result = reduceKey(initialKeyboardState, { code: "ArrowUp", pressed: true });

    expect(result.intents).toEqual([{ type: "direction", direction: "up" }]);
    expect(result.handled).toBe(true);
  });

  it("makes the latest pressed key win", () => {
    const { intents } = press(initialKeyboardState, "ArrowUp", "ArrowLeft");

    expect(intents).toEqual([
      { type: "direction", direction: "up" },
      { type: "direction", direction: "left" },
    ]);
  });

  it("falls back to the previous held key when the active key is released", () => {
    const held = press(initialKeyboardState, "ArrowUp", "ArrowLeft").state;

    const result = reduceKey(held, { code: "ArrowLeft", pressed: false });

    expect(result.intents).toEqual([{ type: "direction", direction: "up" }]);
  });

  it("emits nothing when a non active key is released", () => {
    const held = press(initialKeyboardState, "ArrowUp", "ArrowLeft").state;

    const result = reduceKey(held, { code: "ArrowUp", pressed: false });

    expect(result.intents).toEqual([]);
  });

  it("keeps moving when the last direction key is released", () => {
    const held = press(initialKeyboardState, "ArrowUp").state;

    const result = reduceKey(held, { code: "ArrowUp", pressed: false });

    expect(result.intents).toEqual([]);
    expect(result.state.held).toEqual([]);
  });

  it("does not repeat an intent while a key auto repeats", () => {
    const held = press(initialKeyboardState, "ArrowUp").state;

    const result = reduceKey(held, { code: "ArrowUp", pressed: true, repeat: true });

    expect(result.intents).toEqual([]);
    expect(result.handled).toBe(true);
  });

  it("treats arrow and letter keys for the same direction as separate keys", () => {
    const held = press(initialKeyboardState, "ArrowUp", "KeyW").state;

    const afterOneRelease = reduceKey(held, { code: "KeyW", pressed: false });

    expect(afterOneRelease.state.held).toEqual(["ArrowUp"]);
    expect(afterOneRelease.intents).toEqual([]);
  });

  it("does not emit when the new active key means the same direction", () => {
    const { intents } = press(initialKeyboardState, "ArrowUp", "KeyW");

    expect(intents).toEqual([{ type: "direction", direction: "up" }]);
  });
});

describe("reduceKey shoot release", () => {
  it("tells when the space key that shot is let go", () => {
    const down = reduceKey(initialKeyboardState, { code: "Space", pressed: true });
    const up = reduceKey(down.state, { code: "Space", pressed: false });

    expect(up.intents).toEqual([{ type: "shootRelease" }]);
  });

  it("says nothing about a release that never shot", () => {
    expect(reduceKey(initialKeyboardState, { code: "Space", pressed: false }).intents).toEqual([]);
  });

  it("lets go of the shoot control when the window loses focus", () => {
    const down = reduceKey(initialKeyboardState, { code: "Space", pressed: true });

    expect(releaseAll(down.state).intents).toEqual([{ type: "shootRelease" }]);
  });

  it("tells when the left mouse button that shot is let go", () => {
    const down = reduceMouse(initialKeyboardState, { button: 0, pressed: true });

    expect(reduceMouse(down.state, { button: 0, pressed: false }).intents).toEqual([{ type: "shootRelease" }]);
  });
});

describe("reduceKey shoot and boost", () => {
  it("shoots on a fresh space press", () => {
    expect(reduceKey(initialKeyboardState, { code: "Space", pressed: true }).intents).toEqual([{ type: "shoot" }]);
  });

  it("does not shoot on auto repeat or release", () => {
    expect(reduceKey(initialKeyboardState, { code: "Space", pressed: true, repeat: true }).intents).toEqual([]);
    expect(reduceKey(initialKeyboardState, { code: "Space", pressed: false }).intents).toEqual([]);
  });

  it("still marks space as handled so the page does not scroll", () => {
    expect(reduceKey(initialKeyboardState, { code: "Space", pressed: false }).handled).toBe(true);
  });

  it.each(["ShiftLeft", "ShiftRight"])("boosts while %s is held", (code) => {
    const down = reduceKey(initialKeyboardState, { code, pressed: true });
    const up = reduceKey(down.state, { code, pressed: false });

    expect(down.intents).toEqual([{ type: "boost", active: true }]);
    expect(up.intents).toEqual([{ type: "boost", active: false }]);
  });

  it("does not emit boost twice", () => {
    const down = reduceKey(initialKeyboardState, { code: "ShiftLeft", pressed: true });

    expect(reduceKey(down.state, { code: "ShiftRight", pressed: true }).intents).toEqual([]);
  });

  it("does not emit boost off when not boosting", () => {
    expect(reduceKey(initialKeyboardState, { code: "ShiftLeft", pressed: false }).intents).toEqual([]);
  });
});

describe("reduceKey misc", () => {
  it("ignores keys that mean nothing", () => {
    const result = reduceKey(initialKeyboardState, { code: "KeyQ", pressed: true });

    expect(result.handled).toBe(false);
    expect(result.intents).toEqual([]);
    expect(result.state).toBe(initialKeyboardState);
  });

  it("never mutates the previous state", () => {
    const before = press(initialKeyboardState, "ArrowUp").state;
    const snapshot = JSON.stringify(before);

    reduceKey(before, { code: "ArrowLeft", pressed: true });

    expect(JSON.stringify(before)).toBe(snapshot);
  });
});

describe("releaseAll", () => {
  it("stops boosting and forgets held keys", () => {
    const state = press(press(initialKeyboardState, "ArrowUp").state, "ShiftLeft").state;

    const result = releaseAll(state);

    expect(result.intents).toEqual([{ type: "boost", active: false }]);
    expect(result.state).toEqual(initialKeyboardState);
  });

  it("emits nothing when nothing was active", () => {
    expect(releaseAll(initialKeyboardState).intents).toEqual([]);
  });
});

describe("reduceMouse", () => {
  it("shoots on a left button press", () => {
    const result = reduceMouse(initialKeyboardState, { button: 0, pressed: true });

    expect(result.intents).toEqual([{ type: "shoot" }]);
    expect(result.handled).toBe(true);
  });

  it("does not shoot when the left button is released", () => {
    expect(reduceMouse(initialKeyboardState, { button: 0, pressed: false }).intents).toEqual([]);
  });

  it("boosts while the right button is held", () => {
    const down = reduceMouse(initialKeyboardState, { button: 2, pressed: true });
    const up = reduceMouse(down.state, { button: 2, pressed: false });

    expect(down.intents).toEqual([{ type: "boost", active: true }]);
    expect(up.intents).toEqual([{ type: "boost", active: false }]);
  });

  it("leaves other buttons alone", () => {
    const result = reduceMouse(initialKeyboardState, { button: 1, pressed: true });

    expect(result.intents).toEqual([]);
    expect(result.handled).toBe(false);
  });
});

