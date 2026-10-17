import { beforeEach, describe, expect, it } from "vitest";
import type { ControlIntent } from "@/input/intents";
import { FakeGazeSource } from "@/test/fakeGaze";
import { useGazeControls, type UseGazeControlsOptions } from "./useGazeControls";

const room = { width: 1000, height: 1000 };
/** Top-left corner of the head box; its centre is (500, 500). */
const head = { x: 492.5, y: 492.5 };

let source: FakeGazeSource;
let intents: ControlIntent[];
let overrides: Partial<UseGazeControlsOptions>;

function create() {
  return useGazeControls({
    source,
    getHead: () => head,
    getRoom: () => room,
    onIntent: (intent) => intents.push(intent),
    now: () => 0,
    ...overrides,
  });
}

async function started() {
  const controls = create();
  await controls.start();
  return controls;
}

beforeEach(() => {
  source = new FakeGazeSource();
  intents = [];
  overrides = {};
});

describe("useGazeControls steering", () => {
  it("steers toward a point outside the dead zone", async () => {
    await started();

    source.emit({ t: 0, x: 0.9, y: 0.5 });

    expect(intents).toEqual([{ type: "steer", angle: 0 }]);
  });

  it.each([
    ["down", 0.5, 0.9, Math.PI / 2],
    ["up", 0.5, 0.1, -Math.PI / 2],
    ["left", 0.1, 0.5, Math.PI],
  ])("steers %s", async (_name, x, y, angle) => {
    await started();

    source.emit({ t: 0, x, y });

    expect(intents).toHaveLength(1);
    expect((intents[0] as { angle: number }).angle).toBeCloseTo(angle, 9);
  });

  it("measures from the centre of the head box, not from its corner", async () => {
    overrides.getHead = () => ({ x: 500, y: 500 });
    await started();

    source.emit({ t: 0, x: 0.9075, y: 0.5075 });

    expect((intents[0] as { angle: number }).angle).toBeCloseTo(0, 9);
  });

  it("stays quiet inside the dead zone", async () => {
    await started();

    source.emit({ t: 0, x: 0.52, y: 0.52 });

    expect(intents).toEqual([]);
  });

  it("does not repeat the same heading", async () => {
    await started();

    source.emit({ t: 0, x: 0.9, y: 0.5 });
    source.emit({ t: 16, x: 0.95, y: 0.5 });

    expect(intents).toHaveLength(1);
  });

  it("smooths the pointer before computing the angle", async () => {
    await started();
    source.emit({ t: 0, x: 0.9, y: 0.5 });

    source.emit({ t: 16, x: 0.9, y: 0.9 });

    const raw = Math.atan2(400, 400);
    const emitted = (intents[intents.length - 1] as { angle: number }).angle;
    expect(emitted).toBeGreaterThan(0);
    expect(emitted).toBeLessThan(raw);
  });

  it("ignores samples without a head or a room", async () => {
    overrides.getHead = () => null;
    await started();
    source.emit({ t: 0, x: 0.9, y: 0.5 });

    expect(intents).toEqual([]);
  });

  it("ignores samples when the room is unknown", async () => {
    overrides.getRoom = () => null;
    await started();

    source.emit({ t: 0, x: 0.9, y: 0.5 });

    expect(intents).toEqual([]);
  });

  it("ignores samples while disabled", async () => {
    overrides.enabled = () => false;
    await started();

    source.emit({ t: 0, x: 0.9, y: 0.5 });

    expect(intents).toEqual([]);
  });

  it("honours a custom dead radius", async () => {
    overrides.aim = { deadRadius: 400 };
    await started();

    source.emit({ t: 0, x: 0.8, y: 0.5 });
    expect(intents).toEqual([]);

    // The pointer is smoothed, so it takes a few samples at the far edge to get past the radius.
    for (let sample = 1; sample <= 30; sample++) source.emit({ t: sample * 16, x: 0.99, y: 0.5 });
    expect(intents).toHaveLength(1);
  });
});

describe("useGazeControls joystick mode", () => {
  beforeEach(() => {
    overrides.origin = "center";
    overrides.getHead = () => null;
  });

  it("steers from the middle of the stage and needs no snake", async () => {
    await started();

    source.emit({ t: 0, x: 0.9, y: 0.5 });

    expect(intents).toEqual([{ type: "steer", angle: 0 }]);
  });

  it.each([
    ["down", 0.5, 0.9, Math.PI / 2],
    ["up", 0.5, 0.1, -Math.PI / 2],
    ["left", 0.1, 0.5, Math.PI],
  ])("steers %s", async (_name, x, y, angle) => {
    await started();

    source.emit({ t: 0, x, y });

    expect((intents[0] as { angle: number }).angle).toBeCloseTo(angle, 9);
  });

  it("has a dead zone of about a tenth of the room around the middle", async () => {
    await started();

    source.emit({ t: 0, x: 0.55, y: 0.5 });
    expect(intents).toEqual([]);

    source.emit({ t: 16, x: 0.9, y: 0.5 });
    expect(intents).toHaveLength(1);
  });

  it("scales the dead zone with the smaller side of the room", async () => {
    overrides.getRoom = () => ({ width: 2000, height: 500 });
    await started();

    // 0.54 of the width is 80 units from the middle: outside 0.12 * 500 = 60 units.
    source.emit({ t: 0, x: 0.54, y: 0.5 });

    expect(intents).toHaveLength(1);
  });

  it("honours an explicit dead radius", async () => {
    overrides.aim = { deadRadius: 450 };
    await started();

    source.emit({ t: 0, x: 0.9, y: 0.5 });

    expect(intents).toEqual([]);
  });
});

describe("useGazeControls lost face", () => {
  it("ignores samples without a face", async () => {
    await started();

    source.emit({ t: 0, x: 0.9, y: 0.5, present: false });

    expect(intents).toEqual([]);
  });

  it("starts from fresh smoothing and heading after the face comes back", async () => {
    await started();
    source.emit({ t: 0, x: 0.9, y: 0.5, present: true });
    source.emit({ t: 16, x: 0.5, y: 0.5, present: false });

    source.emit({ t: 1000, x: 0.5, y: 0.9, present: true });

    expect(intents).toHaveLength(2);
    expect((intents[1] as { angle: number }).angle).toBeCloseTo(Math.PI / 2, 9);
  });

  it("reports the same heading again after the face came back", async () => {
    await started();
    source.emit({ t: 0, x: 0.9, y: 0.5, present: true });
    source.emit({ t: 16, x: 0.5, y: 0.5, present: false });

    source.emit({ t: 1000, x: 0.9, y: 0.5, present: true });

    expect(intents).toEqual([
      { type: "steer", angle: 0 },
      { type: "steer", angle: 0 },
    ]);
  });
});

describe("useGazeControls arbitration", () => {
  it("holds steering back while the arbiter says no", async () => {
    overrides.canSteer = () => false;
    await started();

    source.emit({ t: 0, x: 0.9, y: 0.5 });

    expect(intents).toEqual([]);
  });

  it("steers as soon as the arbiter allows it, even if the pointer did not move", async () => {
    let allowed = false;
    overrides.canSteer = () => allowed;
    await started();
    source.emit({ t: 0, x: 0.9, y: 0.5 });

    allowed = true;
    source.emit({ t: 16, x: 0.9, y: 0.5 });

    expect(intents).toEqual([{ type: "steer", angle: 0 }]);
  });

  it("asks the arbiter with the current time", async () => {
    const asked: number[] = [];
    overrides.canSteer = (now) => (asked.push(now), true);
    overrides.now = () => 777;
    await started();

    source.emit({ t: 0, x: 0.9, y: 0.5 });

    expect(asked).toEqual([777]);
  });
});

describe("useGazeControls lifecycle", () => {
  it("starts off and reports tracking once the source runs", async () => {
    const controls = create();
    expect(controls.status.value).toBe("off");

    await controls.start();

    expect(controls.status.value).toBe("tracking");
    expect(source.started).toBe(true);
  });

  it("reports an error and keeps listening off when the source cannot start", async () => {
    source.startError = new Error("camera denied");
    const controls = create();

    await controls.start();

    expect(controls.status.value).toBe("error");
    expect(controls.error.value).toBe("camera denied");
    expect(source.listenerCount).toBe(0);
  });

  it("does not subscribe twice when started twice", async () => {
    const controls = create();

    await controls.start();
    await controls.start();

    expect(source.listenerCount).toBe(1);
  });

  it("stop unsubscribes, stops the source and goes off", async () => {
    const controls = await started();

    controls.stop();
    source.emit({ t: 0, x: 0.9, y: 0.5 });

    expect(intents).toEqual([]);
    expect(source.stopCalls).toBe(1);
    expect(source.listenerCount).toBe(0);
    expect(controls.status.value).toBe("off");
  });

  it("forgets the previous heading after a restart", async () => {
    const controls = await started();
    source.emit({ t: 0, x: 0.9, y: 0.5 });
    controls.stop();

    await controls.start();
    source.emit({ t: 1000, x: 0.9, y: 0.5 });

    expect(intents).toHaveLength(2);
  });

  it("can be stopped when it never started", () => {
    expect(() => create().stop()).not.toThrow();
  });
});
