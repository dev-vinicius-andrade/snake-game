import { beforeEach, describe, expect, it } from "vitest";
import { CALIBRATION_TARGETS, type Calibration } from "@/input/gaze/calibration";
import type { CalibrationStorage } from "@/input/gaze/calibrationStorage";
import type { CameraDevice } from "@/input/gaze/cameraDevices";
import { CameraError } from "@/input/gaze/cameraErrors";
import type { CameraPreferenceStorage } from "@/input/gaze/cameraPreference";
import type { FaceModePreference } from "@/input/gaze/faceModePreference";
import type { FaceMode } from "@/input/gaze/mediapipeGazeSource";
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

class MemoryCameraPreference implements CameraPreferenceStorage {
  value: string | null = null;
  load() {
    return this.value;
  }
  save(cameraId: string | null) {
    this.value = cameraId;
  }
}

class MemoryModePreference implements FaceModePreference {
  value: FaceMode | null = null;
  load() {
    return this.value;
  }
  save(mode: FaceMode) {
    this.value = mode;
  }
}

let headStorage: MemoryStorage;
let modePreference: MemoryModePreference;
let source: FakeCameraSource;
let cameraPreference: MemoryCameraPreference;
let storage: MemoryStorage;
let intents: ControlIntent[];
let overrides: Partial<UseEyeControlOptions>;
let clock: number;

function create() {
  return useEyeControl({
    source,
    storage,
    headStorage,
    modeStorage: modePreference,
    cameraStorage: cameraPreference,
    getRoom: () => room,
    getHead: () => ({ x: 492.5, y: 492.5 }),
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
  headStorage = new MemoryStorage();
  modePreference = new MemoryModePreference();
  cameraPreference = new MemoryCameraPreference();
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
    overrides.aim = { dwellMs: 0 };
  });

  it("steers toward where the gaze is, from the snake's head", async () => {
    overrides.getHead = () => ({ x: 92.5, y: 492.5 });
    const control = create();
    await control.enable();

    source.emit(frame({ x: 0.5, y: 0.5 }));

    expect(intents).toEqual([{ type: "steer", angle: 0 }]);
  });

  it("does nothing when you look at the snake, wherever it is on the screen", async () => {
    overrides.getHead = () => ({ x: 892.5, y: 92.5 });
    const control = create();
    await control.enable();

    source.emit(frame({ x: 0.9, y: 0.1 }));

    expect(intents).toEqual([]);
  });

  it("waits a moment before turning, so a glance does not steer", async () => {
    overrides.aim = undefined;
    const control = create();
    await control.enable();

    source.emit({ t: 0, x: 0.9, y: 0.5, present: true, blink: 0, raw: { x: 0, y: 0 } });
    source.emit({ t: 100, x: 0.9, y: 0.5, present: true, blink: 0, raw: { x: 0, y: 0 } });
    expect(intents.filter((intent) => intent.type === "steer")).toEqual([]);

    source.emit({ t: 400, x: 0.9, y: 0.5, present: true, blink: 0, raw: { x: 0, y: 0 } });
    expect(intents.filter((intent) => intent.type === "steer")).toEqual([{ type: "steer", angle: 0 }]);
  });

  it("does not steer while there is no snake to aim from", async () => {
    overrides.getHead = () => null;
    const control = create();
    await control.enable();

    source.emit(frame({ x: 0.9, y: 0.5 }));

    expect(intents).toEqual([]);
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

describe("useEyeControl without a game, as on the start page", () => {
  it("calibrates and saves with no room and no snake", async () => {
    overrides.getRoom = () => null;
    overrides.getHead = () => null;
    const control = create();
    await control.enable();

    lookAtTargets(goodEye, control);

    expect(control.phase.value).toBe("active");
    expect(storage.value).not.toBeNull();
    expect(intents).toEqual([]);
  });

  it("knows the face is tracked while calibrating with no room", async () => {
    overrides.getRoom = () => null;
    const control = create();
    await control.enable();

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

describe("useEyeControl camera choice", () => {
  const webcam: CameraDevice = { id: "cam-2", label: "Real Webcam", virtual: false };
  const obs: CameraDevice = { id: "obs", label: "OBS Virtual Camera", virtual: true };

  beforeEach(() => {
    source.cameras = [webcam, obs];
    source.active = obs;
    storage.value = saved;
  });

  it("hands the saved camera choice to the source before it starts", async () => {
    cameraPreference.value = "cam-2";
    const control = create();

    await control.enable();

    expect(source.preferred).toEqual(["cam-2"]);
  });

  it("starts with the browser default when nothing was chosen", async () => {
    const control = create();

    await control.enable();

    expect(source.preferred).toEqual([null]);
  });

  it("lists the cameras and the one in use once the camera is on", async () => {
    const control = create();
    expect(control.cameras.value).toEqual([]);

    await control.enable();

    expect(control.cameras.value).toEqual([webcam, obs]);
    expect(control.activeCamera.value).toEqual(obs);
  });

  it("warns when the camera in use looks virtual and a real one exists", async () => {
    const control = create();

    await control.enable();

    expect(control.virtualCameraWarning.value).toBe(true);
  });

  it("does not warn when a real camera is in use", async () => {
    source.active = webcam;
    const control = create();

    await control.enable();

    expect(control.virtualCameraWarning.value).toBe(false);
  });

  it("does not warn when the virtual camera is the only one", async () => {
    source.cameras = [obs];
    const control = create();

    await control.enable();

    expect(control.virtualCameraWarning.value).toBe(false);
  });

  it("switching saves the choice and moves the running camera over", async () => {
    const control = create();
    await control.enable();

    await control.selectCamera("cam-2");

    expect(cameraPreference.value).toBe("cam-2");
    expect(source.selected).toEqual(["cam-2"]);
    expect(control.activeCamera.value).toEqual(webcam);
    expect(control.virtualCameraWarning.value).toBe(false);
    expect(control.phase.value).toBe("active");
  });

  it("keeps calibrating through a switch", async () => {
    storage.value = null;
    const control = create();
    await control.enable();

    await control.selectCamera("cam-2");

    expect(control.phase.value).toBe("calibrating");
  });

  it("before the camera is on, switching only remembers the choice", async () => {
    const control = create();

    await control.selectCamera("cam-2");

    expect(cameraPreference.value).toBe("cam-2");
    expect(source.selected).toEqual([]);
    expect(source.preferred).toEqual(["cam-2"]);
  });

  it("ends in an error when the chosen camera cannot be opened, and keeps the choice", async () => {
    const control = create();
    await control.enable();
    source.selectError = new CameraError("busy", "in use");

    await control.selectCamera("cam-2");

    expect(control.phase.value).toBe("error");
    expect(control.errorKind.value).toBe("busy");
    expect(cameraPreference.value).toBe("cam-2");
  });

  it("can be turned on again after a failed switch without listening twice", async () => {
    overrides.aim = { dwellMs: 0 };
    const control = create();
    await control.enable();
    source.selectError = new CameraError("busy", "in use");
    await control.selectCamera("cam-2");
    source.selectError = undefined;

    await control.enable();
    source.emit(frame({ x: 0.9, y: 0.5 }));

    expect(control.phase.value).toBe("active");
    expect(intents.filter((intent) => intent.type === "steer")).toHaveLength(1);
  });
});

const headFrame = (extra: Record<string, unknown> = {}) => ({ t: (clock += 33), x: 0.5, y: 0.5, present: true, jaw: 0, brows: 0, raw: { x: 0, y: 0 }, ...extra });
const headSaved: Calibration = { x: { offset: 0.5, gain: 3 }, y: { offset: 0.5, gain: 3 } };

describe("useEyeControl head mode", () => {
  beforeEach(() => {
    overrides.mode = "head";
    headStorage.value = headSaved;
  });

  it("uses the head calibration and tells the camera source the mode", async () => {
    const control = create();

    await control.enable();

    expect(control.mode.value).toBe("head");
    expect(source.modes).toEqual(["head"]);
    expect(source.calibrations).toEqual([headSaved]);
    expect(control.phase.value).toBe("active");
  });

  it("calibrates when there is only an eye calibration", async () => {
    headStorage.value = null;
    storage.value = saved;
    const control = create();

    await control.enable();

    expect(control.phase.value).toBe("calibrating");
  });

  it("saves the calibration in the head store", async () => {
    headStorage.value = null;
    const control = create();
    await control.enable();

    lookAtTargets((stage) => ({ x: (stage.x - 0.5) / 4, y: (stage.y - 0.5) / 4 }), control);

    expect(headStorage.value).not.toBeNull();
    expect(storage.value).toBeNull();
    expect(control.phase.value).toBe("active");
  });

  it("steers like a joystick from the middle of the stage, even with a snake elsewhere", async () => {
    overrides.getHead = () => ({ x: 92.5, y: 92.5 });
    overrides.aim = { dwellMs: 0 };
    const control = create();
    await control.enable();

    source.emit(headFrame({ x: 0.9, y: 0.5 }));

    expect(intents.filter((intent) => intent.type === "steer")).toEqual([{ type: "steer", angle: 0 }]);
  });

  it("shoots with an open mouth and boosts with raised eyebrows", async () => {
    const control = create();
    await control.enable();

    for (let i = 0; i < 12; i++) source.emit(headFrame({ jaw: 0.9, brows: 0.9 }));

    expect(intents).toContainEqual({ type: "shoot" });
    expect(intents).toContainEqual({ type: "boost", active: true });
  });

  it("does not shoot on a blink", async () => {
    const control = create();
    await control.enable();
    source.emit({ t: 0, x: 0.5, y: 0.5, present: true, blink: 0 });
    source.emit({ t: 100, x: 0.5, y: 0.5, present: true, blink: 0.9 });

    source.emit({ t: 700, x: 0.5, y: 0.5, present: true, blink: 0.1 });

    expect(intents).not.toContainEqual({ type: "shoot" });
  });

  it("recalibrate forgets the head calibration only", async () => {
    storage.value = saved;
    const control = create();
    await control.enable();

    control.recalibrate();

    expect(headStorage.clears).toBe(1);
    expect(storage.clears).toBe(0);
    expect(control.phase.value).toBe("calibrating");
  });
});

describe("useEyeControl mode choice", () => {
  it("starts in the remembered mode", () => {
    modePreference.value = "head";

    expect(create().mode.value).toBe("head");
  });

  it("starts in eyes mode when nothing was chosen", () => {
    expect(create().mode.value).toBe("eyes");
  });

  it("remembers a new choice, also while off, without starting the camera", () => {
    const control = create();

    control.setMode("head");

    expect(control.mode.value).toBe("head");
    expect(modePreference.value).toBe("head");
    expect(source.started).toBe(false);
  });

  it("switches while running to the calibration of the new mode", async () => {
    storage.value = saved;
    headStorage.value = headSaved;
    const control = create();
    await control.enable();

    control.setMode("head");

    expect(source.modes.at(-1)).toBe("head");
    expect(source.calibrations.at(-1)).toEqual(headSaved);
    expect(control.phase.value).toBe("active");
  });

  it("asks for a calibration when the new mode has none", async () => {
    storage.value = saved;
    const control = create();
    await control.enable();

    control.setMode("head");

    expect(control.phase.value).toBe("calibrating");
    expect(control.calibrationTarget.value?.id).toBe("center");
  });

  it("changing to the same mode does nothing", async () => {
    storage.value = saved;
    const control = create();
    await control.enable();
    const calibrations = source.calibrations.length;

    control.setMode("eyes");

    expect(source.calibrations).toHaveLength(calibrations);
  });
});
