import { beforeEach, describe, expect, it, vi } from "vitest";
import type { BlendshapeScore } from "./blendshapes";
import { CameraError } from "./cameraErrors";
import type { GazeSample } from "./gazeSource";
import {
  createMediaPipeGazeSource,
  type CameraStreamLike,
  type LandmarkerLike,
  type MediaPipeGazeSourceOptions,
  type VideoLike,
} from "./mediapipeGazeSource";

const NAMES = [
  "eyeLookInLeft", "eyeLookOutLeft", "eyeLookUpLeft", "eyeLookDownLeft",
  "eyeLookInRight", "eyeLookOutRight", "eyeLookUpRight", "eyeLookDownRight",
  "eyeBlinkLeft", "eyeBlinkRight",
];
const shapes = (overrides: Record<string, number> = {}): BlendshapeScore[] =>
  NAMES.map((categoryName) => ({ categoryName, score: overrides[categoryName] ?? 0 }));
const named = (name: string) => Object.assign(new Error(name), { name });

class Fixture {
  trackStops = 0;
  landmarkerClosed = 0;
  streamRequests = 0;
  landmarkerLoads = 0;
  result: { faceBlendshapes?: Array<{ categories: BlendshapeScore[] }>; faceLandmarks?: unknown[] } = {
    faceBlendshapes: [{ categories: shapes() }],
    faceLandmarks: [[{ x: 0, y: 0, z: 0 }]],
  };
  rafRequests = 0;
  videoFrameCancels: number[] = [];
  requestedCameras: Array<string | null> = [];
  cameraLabel = "Fake Cam";
  failFor: (cameraId: string | null) => Error | null = () => null;
  devices: Array<{ kind: string; deviceId: string; label: string }> = [
    { kind: "videoinput", deviceId: "obs", label: "OBS Virtual Camera" },
    { kind: "videoinput", deviceId: "cam-2", label: "Real Webcam" },
    { kind: "audioinput", deviceId: "mic", label: "Mic" },
  ];
  detectError: Error | null = null;
  streamError: Error | null = null;
  loadError: Error | null = null;
  playError: Error | null = null;
  clock = 1000;
  nextFrame: (() => void) | null = null;
  cancelled: number[] = [];
  video: VideoLike & { paused: boolean; removed: boolean } = { removed: false, remove() { this.removed = true; }, readyState: 4, currentTime: 0, videoWidth: 640, videoHeight: 480, srcObject: null, muted: false, playsInline: false, paused: false, play: async () => {}, pause() { this.paused = true; } };

  private nextHandle = 1;

  options() {
    return {
      navigatorLike: { mediaDevices: { getUserMedia: () => undefined } },
      getStream: async (cameraId: string | null = null): Promise<CameraStreamLike> => {
        this.streamRequests++;
        this.requestedCameras.push(cameraId);
        const failure = this.failFor(cameraId);
        if (failure) throw failure;
        if (this.streamError) throw this.streamError;
        const track = { label: this.cameraLabel, getSettings: () => ({ deviceId: cameraId ?? "cam-default" }), stop: () => this.trackStops++ };
        return { getTracks: () => [track], getVideoTracks: () => [track] };
      },
      enumerateDevices: async () => this.devices,
      loadLandmarker: async (): Promise<LandmarkerLike> => {
        this.landmarkerLoads++;
        if (this.loadError) throw this.loadError;
        return {
          detectForVideo: () => {
            if (this.detectError) throw this.detectError;
            return this.result;
          },
          close: () => this.landmarkerClosed++,
        };
      },
      createVideo: () => {
        this.video.play = async () => {
          if (this.playError) throw this.playError;
        };
        return this.video;
      },
      requestFrame: (callback: () => void) => {
        this.rafRequests++;
        this.nextFrame = callback;
        return this.nextHandle++;
      },
      cancelFrame: (handle: number) => {
        this.cancelled.push(handle);
        this.nextFrame = null;
      },
      now: () => this.clock,
    };
  }

  /** The loop runs again but the video has no new frame. */
  repeatFrame() {
    const run = this.nextFrame;
    this.nextFrame = null;
    run?.();
  }

  /** A new video frame arrives and the loop runs once. */
  frame(advanceMs = 33) {
    this.clock += advanceMs;
    this.video.currentTime += 0.033;
    const run = this.nextFrame;
    this.nextFrame = null;
    run?.();
  }
}

let fx: Fixture;
let samples: GazeSample[];

beforeEach(() => {
  fx = new Fixture();
  samples = [];
});

async function started(overrides: Partial<MediaPipeGazeSourceOptions> = {}) {
  const source = createMediaPipeGazeSource({ ...fx.options(), ...overrides });
  source.subscribe((sample) => samples.push(sample));
  await source.start();
  return source;
}

describe("createMediaPipeGazeSource start", () => {
  it("asks for the camera, loads the face model and starts the video", async () => {
    await started();

    expect(fx.streamRequests).toBe(1);
    expect(fx.landmarkerLoads).toBe(1);
    expect(fx.video.srcObject).not.toBeNull();
    expect(fx.video.muted).toBe(true);
    expect(fx.video.playsInline).toBe(true);
  });

  it("asks for the camera before loading the model, so the permission prompt comes first", async () => {
    const order: string[] = [];
    await started({
      getStream: async () => {
        order.push("camera");
        return { getTracks: () => [] };
      },
      loadLandmarker: async () => {
        order.push("model");
        return { detectForVideo: () => ({}), close: () => undefined };
      },
    });

    expect(order).toEqual(["camera", "model"]);
  });

  it("does nothing a second time", async () => {
    const source = await started();

    await source.start();

    expect(fx.streamRequests).toBe(1);
  });

  it("reports a refused permission", async () => {
    fx.streamError = named("NotAllowedError");

    await expect(started()).rejects.toMatchObject({ kind: "denied" });
    expect(fx.landmarkerLoads).toBe(0);
  });

  it("reports a missing camera", async () => {
    fx.streamError = named("NotFoundError");

    await expect(started()).rejects.toMatchObject({ kind: "no-camera" });
  });

  it("reports a camera in use elsewhere", async () => {
    fx.streamError = named("NotReadableError");

    await expect(started()).rejects.toMatchObject({ kind: "busy" });
  });

  it("reports a browser without camera support and never asks for the stream", async () => {
    const source = createMediaPipeGazeSource({ ...fx.options(), navigatorLike: {} });

    await expect(source.start()).rejects.toMatchObject({ kind: "unsupported" });
    expect(fx.streamRequests).toBe(0);
  });

  it("reports a model that cannot be loaded and releases the camera", async () => {
    fx.loadError = new Error("404");

    await expect(started()).rejects.toMatchObject({ kind: "model" });
    expect(fx.trackStops).toBe(1);
  });

  it("releases the camera and the model when the video cannot play", async () => {
    fx.playError = named("NotAllowedError");

    await expect(started()).rejects.toBeInstanceOf(CameraError);
    expect(fx.trackStops).toBe(1);
    expect(fx.landmarkerClosed).toBe(1);
  });

  it("can start again after a failure", async () => {
    fx.streamError = named("NotAllowedError");
    const source = createMediaPipeGazeSource(fx.options());
    await expect(source.start()).rejects.toBeInstanceOf(CameraError);

    fx.streamError = null;
    await source.start();

    expect(fx.streamRequests).toBe(2);
  });
});

describe("createMediaPipeGazeSource frames", () => {
  it("emits one calibrated sample per new video frame", async () => {
    await started();

    fx.frame();
    fx.frame();

    expect(samples).toHaveLength(2);
    expect(samples[0]).toMatchObject({ x: 0.5, y: 0.5, present: true, blink: 0 });
  });

  it("stamps samples with the clock used for detection", async () => {
    await started();

    fx.frame(40);

    expect(samples[0].t).toBe(1040);
  });

  it("skips a frame when the video has not moved on", async () => {
    await started();
    fx.frame();

    fx.repeatFrame();
    fx.repeatFrame();

    expect(samples).toHaveLength(1);
  });

  it("skips frames while the video has no data yet", async () => {
    await started();
    fx.video.readyState = 1;

    fx.frame();

    expect(samples).toEqual([]);
  });

  it("keeps the loop running after a skipped frame", async () => {
    await started();
    fx.video.readyState = 1;
    fx.frame();
    fx.video.readyState = 4;

    fx.frame();

    expect(samples).toHaveLength(1);
  });

  it("reports eye movement through the calibration", async () => {
    fx.result = { faceBlendshapes: [{ categories: shapes({ eyeLookOutRight: 1, eyeLookInLeft: 1 }) }] };
    await started();

    fx.frame();

    expect(samples[0].x).toBeCloseTo(1, 9);
    expect(samples[0].raw).toEqual({ x: 1, y: 0 });
  });

  it("reports a missing face", async () => {
    fx.result = { faceBlendshapes: [] };
    await started();

    fx.frame();

    expect(samples[0]).toMatchObject({ present: false });
  });

  it("reports a missing face when the result has no blendshapes at all", async () => {
    fx.result = {};
    await started();

    fx.frame();

    expect(samples[0].present).toBe(false);
  });

  it("treats a detection error as a missing face and keeps going", async () => {
    await started();
    fx.detectError = new Error("gpu lost");
    fx.frame();
    fx.detectError = null;

    fx.frame();

    expect(samples.map((sample) => sample.present)).toEqual([false, true]);
  });

  it("tells who is listening about a detection error, once per failure", async () => {
    const errors: unknown[] = [];
    await started({ onDetectError: (error: unknown) => errors.push(error) });
    fx.detectError = new Error("gpu lost");

    fx.frame();
    fx.frame();

    expect(errors).toHaveLength(2);
    expect((errors[0] as Error).message).toBe("gpu lost");
  });

  it("does not report anything when detection works", async () => {
    const errors: unknown[] = [];
    await started({ onDetectError: (error: unknown) => errors.push(error) });

    fx.frame();

    expect(errors).toEqual([]);
  });

  it("applies a new calibration to the next samples", async () => {
    fx.result = { faceBlendshapes: [{ categories: shapes({ eyeLookOutRight: 1, eyeLookInLeft: 1 }) }] };
    const source = await started();

    source.setCalibration({ x: { offset: 0.5, gain: 0.1 }, y: { offset: 0.5, gain: 0.1 } });
    fx.frame();

    expect(samples[0].x).toBeCloseTo(0.6, 9);
  });

  it("delivers samples to every subscriber until it unsubscribes", async () => {
    const source = createMediaPipeGazeSource(fx.options());
    const second = vi.fn();
    const unsubscribe = source.subscribe(second);
    await source.start();

    fx.frame();
    unsubscribe();
    fx.frame();

    expect(second).toHaveBeenCalledTimes(1);
  });
});

describe("createMediaPipeGazeSource diagnostics", () => {
  it("starts with nothing counted", async () => {
    const source = await started();

    expect(source.getDiagnostics()).toMatchObject({ frames: 0, withFace: 0, incomplete: 0, missing: [], skipped: 0 });
  });

  it("counts frames and frames with a face", async () => {
    const source = await started();
    fx.frame();
    fx.result = { faceBlendshapes: [] };
    fx.frame();

    expect(source.getDiagnostics()).toMatchObject({ frames: 2, withFace: 1, incomplete: 0 });
  });

  it("counts a face whose blendshapes are incomplete and names what is missing", async () => {
    fx.result = { faceBlendshapes: [{ categories: shapes().filter((shape) => shape.categoryName !== "eyeBlinkLeft") }] };
    const source = await started();

    fx.frame();

    expect(source.getDiagnostics()).toMatchObject({ frames: 1, withFace: 1, incomplete: 1, missing: ["eyeBlinkLeft"] });
  });

  it("counts frames it skipped because the video had no data or no new frame", async () => {
    const source = await started();
    fx.frame();
    fx.repeatFrame();
    fx.video.readyState = 1;
    fx.frame();

    expect(source.getDiagnostics()).toMatchObject({ frames: 1, skipped: 2 });
  });

  it("reports the video size and state", async () => {
    fx.video.videoWidth = 640;
    fx.video.videoHeight = 480;
    const source = await started();

    expect(source.getDiagnostics()).toMatchObject({ videoWidth: 640, videoHeight: 480, readyState: 4 });
  });

  it("counts frames with face landmarks separately from frames with blendshapes", async () => {
    fx.result = { faceBlendshapes: [], faceLandmarks: [[{ x: 0, y: 0, z: 0 }]] };
    const source = await started();

    fx.frame();

    expect(source.getDiagnostics()).toMatchObject({ frames: 1, withLandmarks: 1, withFace: 0 });
  });

  it("counts no landmarks when the model sees nobody", async () => {
    fx.result = { faceBlendshapes: [], faceLandmarks: [] };
    const source = await started();

    fx.frame();

    expect(source.getDiagnostics()).toMatchObject({ withLandmarks: 0, withFace: 0 });
  });

  it("puts the frame brightness in the summary, to tell a black picture from an empty one", async () => {
    const logged: string[] = [];
    await started({ log: (summary: string) => logged.push(summary), measureBrightness: () => 0.42 });

    fx.frame(2100);

    expect(logged[0]).toContain("brightness=0.42");
  });

  it("says the brightness is unknown when it cannot be measured", async () => {
    const logged: string[] = [];
    await started({ log: (summary: string) => logged.push(summary), measureBrightness: () => null });

    fx.frame(2100);

    expect(logged[0]).toContain("brightness=n/a");
  });

  it("hints that no face was seen once enough frames went by", async () => {
    fx.result = { faceBlendshapes: [], faceLandmarks: [] };
    const logged: string[] = [];
    await started({ log: (summary: string) => logged.push(summary), measureBrightness: () => 0.5 });
    for (let i = 0; i < 200; i++) fx.frame(1);

    fx.frame(2100);

    expect(logged[0]).toContain("no face found");
  });

  it("does not hint while a face is found", async () => {
    const logged: string[] = [];
    await started({ log: (summary: string) => logged.push(summary), measureBrightness: () => 0.5 });
    for (let i = 0; i < 200; i++) fx.frame(1);

    fx.frame(2100);

    expect(logged[0]).not.toContain("no face found");
  });

  it("logs a summary every two seconds while running", async () => {
    const logged: unknown[] = [];
    await started({ log: (summary: unknown) => logged.push(summary) });

    fx.frame(500);
    fx.frame(500);
    expect(logged).toEqual([]);

    fx.frame(1100);
    expect(logged).toHaveLength(1);
    fx.frame(500);
    expect(logged).toHaveLength(1);
    fx.frame(2000);
    expect(logged).toHaveLength(2);
  });

  it("puts the counts in the summary", async () => {
    const logged: string[] = [];
    await started({ log: (summary: string) => logged.push(summary) });

    fx.frame(2100);

    expect(logged[0]).toContain("frames=1");
    expect(logged[0]).toContain("withFace=1");
    expect(logged[0]).toContain("video=640x480");
  });

  it("counts again from zero after a restart", async () => {
    const source = await started();
    fx.frame();
    source.stop();

    await source.start();

    expect(source.getDiagnostics().frames).toBe(0);
  });
});

describe("createMediaPipeGazeSource video frame callbacks", () => {
  it("uses requestVideoFrameCallback when the video has it, so the model runs once per decoded frame", async () => {
    fx.video.requestVideoFrameCallback = (callback) => {
      fx.nextFrame = () => callback();
      return 77;
    };
    fx.video.cancelVideoFrameCallback = (handle) => fx.videoFrameCancels.push(handle);
    await started();

    fx.frame();
    fx.frame();

    expect(fx.rafRequests).toBe(0);
    expect(samples).toHaveLength(2);
  });

  it("cancels the video frame callback when it stops", async () => {
    fx.video.requestVideoFrameCallback = (callback) => {
      fx.nextFrame = () => callback();
      return 77;
    };
    fx.video.cancelVideoFrameCallback = (handle) => fx.videoFrameCancels.push(handle);
    const source = await started();

    source.stop();

    expect(fx.videoFrameCancels).toEqual([77]);
  });

  it("falls back to animation frames when the video has no such callback", async () => {
    await started();

    expect(fx.rafRequests).toBeGreaterThan(0);
  });
});

describe("createMediaPipeGazeSource camera choice", () => {
  it("asks for the default camera when none was chosen", async () => {
    await started();

    expect(fx.requestedCameras).toEqual([null]);
  });

  it("asks for the preferred camera by id", async () => {
    const source = createMediaPipeGazeSource(fx.options());
    source.setPreferredCamera("cam-2");

    await source.start();

    expect(fx.requestedCameras).toEqual(["cam-2"]);
  });

  it("takes the preferred camera from the options too", async () => {
    await started({ cameraId: "cam-2" });

    expect(fx.requestedCameras).toEqual(["cam-2"]);
  });

  it("falls back to the default camera when the preferred one is gone", async () => {
    fx.failFor = (id) => (id === "gone" ? named("NotFoundError") : null);
    const source = await started({ cameraId: "gone" });

    expect(fx.requestedCameras).toEqual(["gone", null]);
    expect(source.getActiveCamera()?.id).toBe("cam-default");
  });

  it("does not retry for other failures, such as a refused permission", async () => {
    fx.failFor = () => named("NotAllowedError");

    await expect(started({ cameraId: "cam-2" })).rejects.toMatchObject({ kind: "denied" });
    expect(fx.requestedCameras).toEqual(["cam-2"]);
  });

  it("reports the camera that is in use, with its name", async () => {
    fx.cameraLabel = "Real Webcam";
    const source = await started({ cameraId: "cam-2" });

    expect(source.getActiveCamera()).toEqual({ id: "cam-2", label: "Real Webcam", virtual: false });
  });

  it("flags the active camera when it is virtual", async () => {
    fx.cameraLabel = "OBS Virtual Camera";
    const source = await started();

    expect(source.getActiveCamera()?.virtual).toBe(true);
  });

  it("has no active camera before it started and after it stopped", async () => {
    const source = createMediaPipeGazeSource(fx.options());
    expect(source.getActiveCamera()).toBeNull();

    await source.start();
    source.stop();

    expect(source.getActiveCamera()).toBeNull();
  });

  it("lists the video cameras, real ones first", async () => {
    const source = createMediaPipeGazeSource(fx.options());

    const cameras = await source.listCameras();

    expect(cameras.map((camera) => camera.id)).toEqual(["cam-2", "obs"]);
  });

  it("lists nothing when the browser refuses to enumerate", async () => {
    const source = createMediaPipeGazeSource({
      ...fx.options(),
      enumerateDevices: async () => {
        throw new Error("blocked");
      },
    });

    expect(await source.listCameras()).toEqual([]);
  });

  it("switches camera while running and keeps delivering to the same listeners", async () => {
    const source = await started();
    fx.frame();

    await source.selectCamera("cam-2");
    fx.frame();

    expect(fx.requestedCameras).toEqual([null, "cam-2"]);
    expect(fx.trackStops).toBe(1);
    expect(samples).toHaveLength(2);
    expect(source.getActiveCamera()?.id).toBe("cam-2");
  });

  it("keeps the calibration across a camera switch", async () => {
    fx.result = { faceBlendshapes: [{ categories: shapes({ eyeLookOutRight: 1, eyeLookInLeft: 1 }) }], faceLandmarks: [[{ x: 0, y: 0, z: 0 }]] };
    const source = await started();
    source.setCalibration({ x: { offset: 0.5, gain: 0.1 }, y: { offset: 0.5, gain: 0.1 } });

    await source.selectCamera("cam-2");
    fx.frame();

    expect(samples[0].x).toBeCloseTo(0.6, 9);
  });

  it("only remembers the choice when it is not running yet", async () => {
    const source = createMediaPipeGazeSource(fx.options());

    await source.selectCamera("cam-2");
    expect(fx.streamRequests).toBe(0);

    await source.start();
    expect(fx.requestedCameras).toEqual(["cam-2"]);
  });

  it("reports a camera that cannot be opened when switching, and stays stopped", async () => {
    const source = await started();
    fx.failFor = (id) => (id === "cam-2" ? named("NotReadableError") : null);

    await expect(source.selectCamera("cam-2")).rejects.toMatchObject({ kind: "busy" });
    fx.frame();

    expect(samples).toHaveLength(0);
  });
});

describe("createMediaPipeGazeSource stop", () => {
  it("stops the loop, the camera and the model", async () => {
    const source = await started();

    source.stop();

    expect(fx.cancelled).toHaveLength(1);
    expect(fx.trackStops).toBe(1);
    expect(fx.landmarkerClosed).toBe(1);
    expect(fx.video.paused).toBe(true);
    expect(fx.video.srcObject).toBeNull();
  });

  it("takes the video element out of the page", async () => {
    const source = await started();

    source.stop();

    expect(fx.video.removed).toBe(true);
  });

  it("emits nothing after it stopped", async () => {
    const source = await started();
    fx.frame();

    source.stop();
    fx.frame();

    expect(samples).toHaveLength(1);
  });

  it("is safe before start and when called twice", async () => {
    const source = createMediaPipeGazeSource(fx.options());

    expect(() => source.stop()).not.toThrow();
    await source.start();
    source.stop();
    expect(() => source.stop()).not.toThrow();
    expect(fx.trackStops).toBe(1);
  });

  it("can be started again", async () => {
    const source = await started();
    source.stop();

    await source.start();
    fx.frame();

    expect(fx.streamRequests).toBe(2);
    expect(samples.length).toBeGreaterThan(0);
  });

  it("releases the camera when stopped while still loading the model", async () => {
    let release!: () => void;
    const gate = new Promise<void>((resolve) => (release = resolve));
    let loading = false;
    const source = createMediaPipeGazeSource({
      ...fx.options(),
      loadLandmarker: async () => {
        loading = true;
        await gate;
        return { detectForVideo: () => ({}), close: () => fx.landmarkerClosed++ };
      },
    });

    const starting = source.start();
    while (!loading) await Promise.resolve();
    source.stop();
    release();
    await starting.catch(() => undefined);

    expect(fx.trackStops).toBe(1);
    expect(fx.landmarkerClosed).toBe(1);
  });
});
