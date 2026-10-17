import { beforeEach, describe, expect, it } from "vitest";
import type { ControlIntent } from "@/input/intents";
import { FakeGazeSource } from "@/test/fakeGaze";
import { useEyeActions, type UseEyeActionsOptions } from "./useEyeActions";

const room = { width: 1000, height: 1000 };

let source: FakeGazeSource;
let intents: ControlIntent[];
let overrides: Partial<UseEyeActionsOptions>;

function create() {
  return useEyeActions({
    source,
    getRoom: () => room,
    onIntent: (intent) => intents.push(intent),
    tracking: { recoverAfterMs: 0, lostAfterMs: 500 },
    ...overrides,
  });
}

async function started() {
  const actions = create();
  await actions.start();
  return actions;
}

const camera = (t: number, extra: Record<string, unknown> = {}) => ({ t, x: 0.5, y: 0.5, present: true, blink: 0, ...extra });

beforeEach(() => {
  source = new FakeGazeSource();
  intents = [];
  overrides = {};
});

describe("useEyeActions", () => {
  it("shoots after a deliberate blink", async () => {
    await started();
    source.emit(camera(0));
    source.emit(camera(100, { blink: 0.9 }));

    source.emit(camera(700, { blink: 0.1 }));

    expect(intents).toEqual([{ type: "shoot" }]);
  });

  it("boosts while the gaze stays far from the middle and stops when it returns", async () => {
    await started();
    source.emit(camera(0, { x: 1 }));
    source.emit(camera(400, { x: 1 }));
    source.emit(camera(500));

    expect(intents).toEqual([
      { type: "boost", active: true },
      { type: "boost", active: false },
    ]);
  });

  it("exposes the tracking state", async () => {
    const actions = await started();
    expect(actions.tracking.value).toBe("lost");

    source.emit(camera(0));
    expect(actions.tracking.value).toBe("tracking");

    source.emit(camera(100, { present: false }));
    source.emit(camera(700, { present: false }));
    expect(actions.tracking.value).toBe("lost");
  });

  it("follows the face without a room, as on the start page, but never acts", async () => {
    overrides.getRoom = () => null;
    const actions = await started();

    source.emit(camera(0, { x: 1 }));
    source.emit(camera(400, { x: 1 }));

    expect(actions.tracking.value).toBe("tracking");
    expect(intents).toEqual([]);
  });

  it("does nothing without a room", async () => {
    overrides.getRoom = () => null;
    await started();

    source.emit(camera(0, { x: 1 }));
    source.emit(camera(400, { x: 1 }));

    expect(intents).toEqual([]);
  });

  it("keeps following the face while disabled, because calibration needs to know", async () => {
    overrides.enabled = () => false;
    const actions = await started();

    source.emit(camera(0));

    expect(actions.tracking.value).toBe("tracking");
    expect(intents).toEqual([]);
  });

  it("does nothing while disabled", async () => {
    overrides.enabled = () => false;
    await started();

    source.emit(camera(0, { x: 1 }));
    source.emit(camera(400, { x: 1 }));

    expect(intents).toEqual([]);
  });

  it("ends a running boost when it stops", async () => {
    const actions = await started();
    source.emit(camera(0, { x: 1 }));
    source.emit(camera(400, { x: 1 }));

    actions.stop();

    expect(intents[intents.length - 1]).toEqual({ type: "boost", active: false });
    expect(source.listenerCount).toBe(0);
  });

  it("goes back to lost when it stops", async () => {
    const actions = await started();
    source.emit(camera(0));

    actions.stop();

    expect(actions.tracking.value).toBe("lost");
  });

  it("reports a source that cannot start", async () => {
    source.startError = new Error("camera denied");
    const actions = create();

    await actions.start();

    expect(actions.status.value).toBe("error");
    expect(actions.error.value).toBe("camera denied");
  });

  it("does not subscribe twice when started twice", async () => {
    const actions = create();

    await actions.start();
    await actions.start();

    expect(source.listenerCount).toBe(1);
  });
});
