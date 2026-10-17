import type { Calibration } from "@/input/gaze/calibration";
import type { GazeSample, GazeSource } from "@/input/gaze/gazeSource";
import type { CameraGazeSource, GazeDiagnostics } from "@/input/gaze/mediapipeGazeSource";

/** In-memory GazeSource for specs. Tests push samples with `emit`. */
export class FakeGazeSource implements GazeSource {
  started = false;
  startCalls = 0;
  stopCalls = 0;
  startError?: Error;
  private listeners = new Set<(sample: GazeSample) => void>();

  async start() {
    this.startCalls++;
    if (this.startError) throw this.startError;
    this.started = true;
  }

  stop() {
    this.stopCalls++;
    this.started = false;
  }

  subscribe(listener: (sample: GazeSample) => void) {
    this.listeners.add(listener);
    return () => {
      this.listeners.delete(listener);
    };
  }

  get listenerCount() {
    return this.listeners.size;
  }

  emit(sample: GazeSample) {
    this.listeners.forEach((listener) => listener(sample));
  }
}

/** FakeGazeSource that also records the calibrations it was given, like the camera source. */
export class FakeCameraSource extends FakeGazeSource implements CameraGazeSource {
  readonly calibrations: Calibration[] = [];

  setCalibration(calibration: Calibration) {
    this.calibrations.push(calibration);
  }

  getDiagnostics(): GazeDiagnostics {
    return { frames: 0, withLandmarks: 0, withFace: 0, incomplete: 0, missing: [], skipped: 0, videoWidth: 0, videoHeight: 0, readyState: 0, brightness: null };
  }
}
