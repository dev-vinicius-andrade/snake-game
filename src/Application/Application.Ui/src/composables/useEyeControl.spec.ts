import { beforeEach, describe, expect, it } from "vitest";
import { CALIBRATION_TARGETS, type Calibration } from "@/input/gaze/calibration";
import type { CalibrationStorage } from "@/input/gaze/calibrationStorage";
import { CameraError } from "@/input/gaze/cameraErrors";
import type { ControlIntent } from "@/input/intents";
import { FakeCameraSource } from "@/test/fakeGaze";
import { useEyeControl, type UseEyeControlOptions } from "./useEyeControl";

const room = { width: 1000, height: 1000 };
const saved: Calibration = { x: { offset: 0.5, gain: 0.6 }, y: { offset: 0.5, gain: 0.6 } };

class MemoryStorage implements CalibrationStorage {
  value: Calibration | null;
  clears = 0;
  constructor(initial: Calibration | null = null) {
    this.value = initial;
  }
  load() {
    return this.value;
  }
  save(calibration: Calibration) {
    this.value = calibration;
  }
  clear() {
    this.value = null;
    this.clears++;
  }
}

let source: FakeCameraSource;
let storage: MemoryStorage;
let intents: ControlIntent[];
let overrides: Partial<UseEyeControlOptions>;
let clock: number;

function create() {
  return useEyeControl({
    source,
    storage,
    getRoom: () => room,
    onIntent: (intent) => intents.push(intent),
    calibration: { framesPerTarget: 3, settleFrames: 0 },
    tracking: { recoverAfterMs: 0, lostAfterMs: 500 },
    ...overrides,
  });
}

const frame = (extra: Record<string, unknown> = {}) => ({ t: (clock += 33), x: 0.5, y: 0.5, present: true, blink: 0, raw: { x: 0, y: 0 }, ...extra });

/** Emits the frames of a user whose eyes follow the targets with the given eye model. */
function lookAtTargets(eye: eyeModel, control: ReturnType<typeof create>) {
  let guard = 0;
  while (control.calibrationTarget.value && guard++ < 100) {
    const target = control.calibrationTarget.value;
    for (let i = 0; i < 3; i++) source.emit(frame({ raw: eye(target) }));
  }
}
type eyeModel = (stage: { x: number; y: number }) => { x: number; y: number };
const goodEye: eyeModel = (stage) => ({ x: (stage.x - 0.5) / 0.8, y: (stage.y - 0.5) / 0.6 });

beforeEach(() => {
  source = new FakeCameraSource();
  storage = new MemoryStorage();
  intents = [];
  overrides = {};
  clock = 0;
});

describe("useEyeControl enable", () => {
  it("starts off", () => {
    expect(create().phase.value).toBe("off");
  });

  it("starts the camera and asks for a calibration when none was saved", async () => {
    const control = create();

    await control.enable();

    expect(source.started).toBe(true);
    expect(control.phase.value).toBe("calibrating");
    expect(control.calibrationTarget.value?.id).toBe("center");
  });

  it("goes straight to active with a saved calibration and applies it", async () => {
    storage.value = saved;
    const control = create();

    await control.enable();

    expect(control.phase.value).toBe("active");
    expect(control.calibrationTarget.value).toBeNull();
    expect(source.calibrations).toEqual([saved]);
  });

  it("is busy once, however often it is called", async () => {
    const control = create();

    await Promise.all([control.enable(), control.enable()]);
    await control.enable();

    expect(source.startCalls).toBe(1);
  });

  it.each([
    ["NotAllowedError", "denied"],
    ["NotFoundError", "no-camera"],
    ["NotReadableError", "busy"],
    ["Whatever", "unknown"],
  ])("explains a %s as %s", async (name, kind) => {
    source.startError = Object.assign(new Error("nope"), { name });
    const control = create();

    await control.enable();

    expect(control.phase.value).toBe("error");
    expect(control.errorKind.value).toBe(kind);
  });

  it("keeps the kind of a CameraError", async () => {
    source.startError = new CameraError("model", "could not load");
    const control = create();

    await control.enable();

    expect(control.errorKind.value).toBe("model");
    expect(control.errorMessage.value).toBe("could not load");
  });

  it("can try again after an error", async () => {
    source.startError = new CameraError("denied", "no");
    const control = create();
    await control.enable();

    source.startError = undefined;
    await control.enable();

    expect(control.phase.value).toBe("calibrating");
    expect(control.errorKind.value).toBeNull();
  });
});

describe("useEyeControl calibration", () => {
  it("finishes when every target was recorded, then saves and applies the result", async () => {
    const control = create();
    await control.enable();

    lookAtTargets(goodEye, control);

    expect(control.phase.value).toBe("active");
    expect(storage.value!.x.gain).toBeCloseTo(0.8, 6);
    expect(source.calibrations[source.calibrations.length - 1]).toEqual(storage.value);
    expect(control.calibrationFailed.value).toBe(false);
  });

  it("reports progress and the current target while calibrating", async () => {
    const control = create();
    await control.enable();

    for (let i = 0; i < 3; i++) source.emit(frame());

    expect(control.calibrationProgress.value).toBeCloseTo(1 / CALIBRATION_TARGETS.length, 9);
    expect(control.calibrationTarget.value?.id).toBe("left");
  });

  it("starts over and says so when the eyes did not move enough", async () => {
    const control = create();
    await control.enable();

    lookAtTargets(() => ({ x: 0.1, y: 0.1 }), control);

    expect(control.phase.value).toBe("calibrating");
    expect(control.calibrationFailed.value).toBe(true);
    expect(control.calibrationTarget.value?.id).toBe("center");
    expect(control.calibrationProgress.value).toBe(0);
    expect(storage.value).toBeNull();
  });

  it("knows the face is tracked while calibrating, so the overlay does not ask for the camera", async () => {
    const control = create();
    await control.enable();
    expect(control.tracking.value).toBe("lost");

    source.emit(frame());

    expect(control.phase.value).toBe("calibrating");
    expect(control.tracking.value).toBe("tracking");
  });

  it("knows when the face is lost while calibrating", async () => {
    const control = create();
    await control.enable();
    source.emit(frame());

    source.emit(frame({ present: false, raw: undefined }));
    source.emit({ t: 5000, x: 0.5, y: 0.5, present: false });

    expect(control.tracking.value).toBe("lost");
  });

  it("waits while there is no face", async () => {
    const control = create();
    await control.enable();

    for (let i = 0; i < 10; i++) source.emit(frame({ present: false, raw: undefined }));

    expect(control.calibrationProgress.value).toBe(0);
  });

  it("recalibrates on request, dropping the saved calibration", async () => {
    storage.value = saved;
    const control = create();
    await control.enable();

    control.recalibrate();

    expect(control.phase.value).toBe("calibrating");
    expect(control.calibrationTarget.value?.id).toBe("center");
    expect(storage.clears).toBe(1);
  });

  it("gives no steering or actions while calibrating", async () => {
    const control = create();
    await control.enable();

    source.emit(frame({ x: 0.9 }));
    source.emit(frame({ x: 0.9, blink: 0.9 }));

    expect(intents).toEqual([]);
  });
});

describe("useEyeControl active", () => {
  beforeEach(() => {
    storage.value = saved;
  });

  it("steers from the middle of the stage", async () => {
    const control = create();
    await control.enable();

    source.emit(frame({ x: 0.9, y: 0.5 }));

    expect(intents).toEqual([{ type: "steer", angle: 0 }]);
  });

  it("shoots after a deliberate blink", async () => {
    const control = create();
    await control.enable();
    source.emit({ t: 0, x: 0.5, y: 0.5, present: true, blink: 0 });
    source.emit({ t: 100, x: 0.5, y: 0.5, present: true, blink: 0.9 });

    source.emit({ t: 700, x: 0.5, y: 0.5, present: true, blink: 0.1 });

    expect(intents).toContainEqual({ type: "shoot" });
  });

  it("holds steering back while the keyboard is steering", async () => {
    overrides.canSteer = () => false;
    const control = create();
    await control.enable();

    source.emit(frame({ x: 0.9, y: 0.5 }));

    expect(intents).toEqual([]);
  });

  it("exposes whether the face is tracked", async () => {
    const control = create();
    await control.enable();
    expect(control.tracking.value).toBe("lost");

    source.emit(frame());

    expect(control.tracking.value).toBe("tracking");
  });
});

describe("useEyeControl disable", () => {
  it("stops the camera and goes off", async () => {
    storage.value = saved;
    const control = create();
    await control.enable();

    control.disable();

    expect(control.phase.value).toBe("off");
    expect(source.started).toBe(false);
    expect(source.listenerCount).toBe(0);
  });

  it("ends a running boost", async () => {
    storage.value = saved;
    const control = create();
    await control.enable();
    source.emit({ t: 0, x: 1, y: 0.5, present: true, blink: 0 });
    source.emit({ t: 400, x: 1, y: 0.5, present: true, blink: 0 });

    control.disable();

    expect(intents[intents.length - 1]).toEqual({ type: "boost", active: false });
  });

  it("can be enabled again", async () => {
    storage.value = saved;
    const control = create();
    await control.enable();
    control.disable();

    await control.enable();

    expect(control.phase.value).toBe("active");
    expect(source.startCalls).toBe(2);
  });

  it("is safe to call when it never ran", () => {
    expect(() => create().disable()).not.toThrow();
  });

  it("does not come up if it was disabled while the camera was starting", async () => {
    const control = create();

    const enabling = control.enable();
    control.disable();
    await enabling;

    expect(control.phase.value).toBe("off");
    expect(source.listenerCount).toBe(0);
  });
});
