import { describe, expect, it } from "vitest";
import { CameraError, classifyCameraError, isCameraSupported } from "./cameraErrors";

const named = (name: string) => Object.assign(new Error("boom"), { name });

describe("classifyCameraError", () => {
  it.each([
    ["NotAllowedError", "denied"],
    ["PermissionDeniedError", "denied"],
    ["SecurityError", "denied"],
    ["NotFoundError", "no-camera"],
    ["DevicesNotFoundError", "no-camera"],
    ["OverconstrainedError", "no-camera"],
    ["NotReadableError", "busy"],
    ["TrackStartError", "busy"],
    ["AbortError", "busy"],
    ["SomethingElse", "unknown"],
  ])("maps %s to %s", (name, kind) => {
    expect(classifyCameraError(named(name))).toBe(kind);
  });

  it("keeps the kind of a CameraError", () => {
    expect(classifyCameraError(new CameraError("model", "could not load"))).toBe("model");
  });

  it.each([null, undefined, 5, "text", {}])("returns unknown for %j", (value) => {
    expect(classifyCameraError(value)).toBe("unknown");
  });
});

describe("CameraError", () => {
  it("carries its kind and message and is an Error", () => {
    const error = new CameraError("denied", "no permission");

    expect(error).toBeInstanceOf(Error);
    expect(error.kind).toBe("denied");
    expect(error.message).toBe("no permission");
  });

  it("keeps the original failure as cause", () => {
    const cause = new Error("inner");

    expect(new CameraError("model", "failed", cause).cause).toBe(cause);
  });
});

describe("isCameraSupported", () => {
  it("is true when getUserMedia exists", () => {
    expect(isCameraSupported({ mediaDevices: { getUserMedia: () => Promise.resolve({}) } })).toBe(true);
  });

  it.each([undefined, null, {}, { mediaDevices: {} }, { mediaDevices: { getUserMedia: 5 } }])("is false for %j", (navigatorLike) => {
    expect(isCameraSupported(navigatorLike as never)).toBe(false);
  });
});
