import { describe, expect, it } from "vitest";
import type { ControlIntent } from "@/input/intents";
import type { GamepadReading } from "@/input/gamepad";
import { useGamepadControls } from "./useGamepadControls";

const idle: GamepadReading = { axes: [0, 0], buttons: [] };
const shooting: GamepadReading = { axes: [0, 0], buttons: [{ pressed: true }] };

function setup(readings: Array<GamepadReading | null>, enabled = () => true) {
  const intents: ControlIntent[] = [];
  let frame: (() => void) | null = null;
  let index = 0;
  const controls = useGamepadControls({
    onIntent: (intent) => intents.push(intent),
    enabled,
    read: () => readings[Math.min(index++, readings.length - 1)],
    schedule: (callback) => {
      frame = callback;
      return 1;
    },
    cancel: () => {
      frame = null;
    },
  });
  return { intents, controls, tick: () => frame?.(), isScheduled: () => frame !== null };
}

describe("useGamepadControls", () => {
  it("does nothing until attached", () => {
    const { isScheduled } = setup([shooting]);

    expect(isScheduled()).toBe(false);
  });

  it("reports intents from every poll while attached", () => {
    const { intents, controls, tick } = setup([shooting, idle, shooting]);

    controls.attach();
    tick();
    tick();
    tick();

    expect(intents).toEqual([{ type: "shoot" }, { type: "shootRelease" }, { type: "shoot" }]);
  });

  it("does nothing when no pad is connected", () => {
    const { intents, controls, tick } = setup([null]);

    controls.attach();
    tick();

    expect(intents).toEqual([]);
  });

  it("does not report anything while disabled", () => {
    const { intents, controls, tick } = setup([shooting], () => false);

    controls.attach();
    tick();

    expect(intents).toEqual([]);
  });

  it("lets go of boost when the pad goes away", () => {
    const boosting: GamepadReading = { axes: [0, 0], buttons: [{ pressed: false }, { pressed: true }] };
    const { intents, controls, tick } = setup([boosting, null]);

    controls.attach();
    tick();
    tick();

    expect(intents).toEqual([{ type: "boost", active: true }, { type: "boost", active: false }]);
  });

  it("stops polling once detached", () => {
    const { controls, isScheduled } = setup([idle]);

    controls.attach();
    controls.detach();

    expect(isScheduled()).toBe(false);
  });
});
