import { describe, expect, it } from "vitest";
import { initialGamepadState, reduceGamepad, type GamepadReading } from "./gamepad";

function reading(parts: { axes?: [number, number]; pressed?: number[] } = {}): GamepadReading {
  const buttons = Array.from({ length: 17 }, (_, index) => ({ pressed: parts.pressed?.includes(index) ?? false }));
  return { axes: parts.axes ?? [0, 0], buttons };
}

describe("reduceGamepad", () => {
  it("steers toward where the left stick points, in the same angle as the rest of the game", () => {
    const right = reduceGamepad(initialGamepadState, reading({ axes: [1, 0] }));
    const down = reduceGamepad(initialGamepadState, reading({ axes: [0, 1] }));
    const up = reduceGamepad(initialGamepadState, reading({ axes: [0, -1] }));

    expect(right.intents).toEqual([{ type: "steer", angle: 0 }]);
    expect(down.intents).toEqual([{ type: "steer", angle: Math.PI / 2 }]);
    expect(up.intents).toEqual([{ type: "steer", angle: -Math.PI / 2 }]);
  });

  it("ignores a stick that rests near the middle", () => {
    expect(reduceGamepad(initialGamepadState, reading({ axes: [0.1, -0.12] })).intents).toEqual([]);
  });

  it("does not repeat the same angle on every poll", () => {
    const first = reduceGamepad(initialGamepadState, reading({ axes: [1, 0] }));
    const second = reduceGamepad(first.state, reading({ axes: [1, 0.01] }));

    expect(second.intents).toEqual([]);
  });

  it("steers again once the stick moves enough", () => {
    const first = reduceGamepad(initialGamepadState, reading({ axes: [1, 0] }));
    const second = reduceGamepad(first.state, reading({ axes: [0, 1] }));

    expect(second.intents).toEqual([{ type: "steer", angle: Math.PI / 2 }]);
  });

  it("steers again after the stick was let go and pushed the same way", () => {
    const first = reduceGamepad(initialGamepadState, reading({ axes: [1, 0] }));
    const released = reduceGamepad(first.state, reading());
    const again = reduceGamepad(released.state, reading({ axes: [1, 0] }));

    expect(again.intents).toEqual([{ type: "steer", angle: 0 }]);
  });

  it.each([
    [12, "up"],
    [13, "down"],
    [14, "left"],
    [15, "right"],
  ])("turns d-pad button %i into a %s direction, once per press", (button, direction) => {
    const down = reduceGamepad(initialGamepadState, reading({ pressed: [button] }));
    const held = reduceGamepad(down.state, reading({ pressed: [button] }));

    expect(down.intents).toEqual([{ type: "direction", direction }]);
    expect(held.intents).toEqual([]);
  });

  it.each([0, 5, 7])("shoots once when button %i goes down", (button) => {
    const down = reduceGamepad(initialGamepadState, reading({ pressed: [button] }));
    const held = reduceGamepad(down.state, reading({ pressed: [button] }));

    expect(down.intents).toEqual([{ type: "shoot" }]);
    expect(held.intents).toEqual([]);
  });

  it.each([0, 5, 7])("tells when button %i that shot is let go", (button) => {
    const down = reduceGamepad(initialGamepadState, reading({ pressed: [button] }));
    const up = reduceGamepad(down.state, reading());

    expect(up.intents).toEqual([{ type: "shootRelease" }]);
  });

  it.each([2, 3])("uses a cooldown perk when button %s is pressed, once per press", (button) => {
    const first = reduceGamepad(initialGamepadState, reading({ pressed: [button] }));
    const held = reduceGamepad(first.state, reading({ pressed: [button] }));
    const again = reduceGamepad(reduceGamepad(held.state, reading()).state, reading({ pressed: [button] }));

    expect(first.intents).toEqual([{ type: "ability" }]);
    expect(held.intents).toEqual([]);
    expect(again.intents).toEqual([{ type: "ability" }]);
  });

  it("keeps shooting while one of two shoot buttons is let go", () => {
    const both = reduceGamepad(initialGamepadState, reading({ pressed: [0, 7] }));
    const one = reduceGamepad(both.state, reading({ pressed: [7] }));

    expect(one.intents).toEqual([]);
  });

  it.each([1, 4, 6])("boosts while button %i is held", (button) => {
    const down = reduceGamepad(initialGamepadState, reading({ pressed: [button] }));
    const up = reduceGamepad(down.state, reading());

    expect(down.intents).toEqual([{ type: "boost", active: true }]);
    expect(up.intents).toEqual([{ type: "boost", active: false }]);
  });

  it("keeps boosting while one of two boost buttons is let go", () => {
    const both = reduceGamepad(initialGamepadState, reading({ pressed: [1, 6] }));
    const one = reduceGamepad(both.state, reading({ pressed: [6] }));

    expect(one.intents).toEqual([]);
  });

  it("copes with a pad that has fewer buttons and axes", () => {
    const small: GamepadReading = { axes: [], buttons: [{ pressed: true }] };

    expect(reduceGamepad(initialGamepadState, small).intents).toEqual([{ type: "shoot" }]);
  });
});
