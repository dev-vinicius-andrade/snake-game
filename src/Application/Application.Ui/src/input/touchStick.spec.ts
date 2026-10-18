import { describe, expect, it } from "vitest";
import { endStick, moveStick, startStick } from "./touchStick";

describe("touch stick", () => {
  it("starts where the thumb lands and has not steered yet", () => {
    expect(startStick({ x: 100, y: 300 })).toEqual({ origin: { x: 100, y: 300 }, angle: null });
  });

  it("steers toward where the thumb moved, in the angle the rest of the game uses", () => {
    const start = startStick({ x: 100, y: 300 });

    expect(moveStick(start, { x: 160, y: 300 }).intent).toEqual({ type: "steer", angle: 0 });
    expect(moveStick(start, { x: 100, y: 360 }).intent).toEqual({ type: "steer", angle: Math.PI / 2 });
    expect(moveStick(start, { x: 100, y: 240 }).intent).toEqual({ type: "steer", angle: -Math.PI / 2 });
  });

  it("ignores a thumb that has hardly moved", () => {
    const start = startStick({ x: 100, y: 300 });

    expect(moveStick(start, { x: 105, y: 304 }).intent).toBeUndefined();
  });

  it("does not repeat the same angle on every move event", () => {
    const first = moveStick(startStick({ x: 0, y: 0 }), { x: 60, y: 0 });
    const second = moveStick(first.state, { x: 61, y: 1 });

    expect(second.intent).toBeUndefined();
  });

  it("steers again once the thumb turns enough", () => {
    const first = moveStick(startStick({ x: 0, y: 0 }), { x: 60, y: 0 });
    const second = moveStick(first.state, { x: 0, y: 60 });

    expect(second.intent).toEqual({ type: "steer", angle: Math.PI / 2 });
  });

  it("keeps the last angle when the thumb drifts back to the middle", () => {
    const first = moveStick(startStick({ x: 0, y: 0 }), { x: 60, y: 0 });
    const back = moveStick(first.state, { x: 2, y: 1 });

    expect(back.intent).toBeUndefined();
    expect(back.state.angle).toBe(0);
  });

  it("knob stays inside the ring however far the thumb goes", () => {
    const moved = moveStick(startStick({ x: 0, y: 0 }), { x: 300, y: 0 }, { radius: 50 });

    expect(moved.knob).toEqual({ x: 50, y: 0 });
  });

  it("puts the knob where the thumb is while inside the ring", () => {
    expect(moveStick(startStick({ x: 10, y: 10 }), { x: 30, y: 10 }).knob).toEqual({ x: 20, y: 0 });
  });

  it("is gone after the thumb lifts", () => {
    expect(endStick()).toEqual({ origin: null, angle: null });
  });
});
