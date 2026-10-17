import { describe, expect, it } from "vitest";
import { InputArbiter } from "./arbiter";

describe("InputArbiter", () => {
  it("lets gaze steer when the keyboard was never used", () => {
    expect(new InputArbiter().allowsGazeSteer(0)).toBe(true);
  });

  it("blocks gaze steering right after a keyboard direction", () => {
    const arbiter = new InputArbiter();
    arbiter.noteKeyboard({ type: "direction", direction: "up" }, 1000);

    expect(arbiter.allowsGazeSteer(1000)).toBe(false);
    expect(arbiter.allowsGazeSteer(1999)).toBe(false);
  });

  it("lets gaze steer again once the hold time passed", () => {
    const arbiter = new InputArbiter();
    arbiter.noteKeyboard({ type: "direction", direction: "up" }, 1000);

    expect(arbiter.allowsGazeSteer(2000)).toBe(true);
  });

  it("restarts the hold on every keyboard direction", () => {
    const arbiter = new InputArbiter();
    arbiter.noteKeyboard({ type: "direction", direction: "up" }, 1000);
    arbiter.noteKeyboard({ type: "direction", direction: "left" }, 1900);

    expect(arbiter.allowsGazeSteer(2500)).toBe(false);
    expect(arbiter.allowsGazeSteer(2900)).toBe(true);
  });

  it("does not treat shooting or boosting as steering", () => {
    const arbiter = new InputArbiter();
    arbiter.noteKeyboard({ type: "shoot" }, 1000);
    arbiter.noteKeyboard({ type: "boost", active: true }, 1000);

    expect(arbiter.allowsGazeSteer(1001)).toBe(true);
  });

  it("honours a custom hold time", () => {
    const arbiter = new InputArbiter({ holdMs: 200 });
    arbiter.noteKeyboard({ type: "direction", direction: "down" }, 0);

    expect(arbiter.allowsGazeSteer(199)).toBe(false);
    expect(arbiter.allowsGazeSteer(200)).toBe(true);
  });

  it("rejects a negative hold time", () => {
    expect(() => new InputArbiter({ holdMs: -1 })).toThrow(RangeError);
  });
});
