import { describe, expect, it } from "vitest";
import { chooseCamera, isLikelyVirtualCamera, toCameraDevices, type CameraDevice } from "./cameraDevices";

const device = (id: string, label: string, virtual = false): CameraDevice => ({ id, label, virtual });

describe("isLikelyVirtualCamera", () => {
  it.each([
    "OBS Virtual Camera",
    "obs virtual camera",
    "Snap Camera",
    "ManyCam Virtual Webcam",
    "NDI Video",
    "XSplit VCam",
    "Streamlabs Desktop Virtual Webcam",
    "Some Virtual Camera",
  ])("recognises %s", (label) => {
    expect(isLikelyVirtualCamera(label)).toBe(true);
  });

  it.each(["Integrated Webcam", "HD Pro Webcam C920 (046d:082d)", "FaceTime HD Camera", "Iriun Webcam", "DroidCam Source 3", "USB2.0 HD UVC WebCam", ""])(
    "does not flag %j",
    (label) => {
      expect(isLikelyVirtualCamera(label)).toBe(false);
    }
  );
});

describe("toCameraDevices", () => {
  it("keeps only video inputs", () => {
    const devices = toCameraDevices([
      { kind: "videoinput", deviceId: "a", label: "Cam A" },
      { kind: "audioinput", deviceId: "mic", label: "Mic" },
      { kind: "audiooutput", deviceId: "spk", label: "Speaker" },
    ]);

    expect(devices.map((d) => d.id)).toEqual(["a"]);
  });

  it("keeps the label and flags virtual cameras", () => {
    const [webcam, obs] = toCameraDevices([
      { kind: "videoinput", deviceId: "a", label: "Integrated Webcam" },
      { kind: "videoinput", deviceId: "b", label: "OBS Virtual Camera" },
    ]);

    expect(webcam).toEqual({ id: "a", label: "Integrated Webcam", virtual: false });
    expect(obs.virtual).toBe(true);
  });

  it("names cameras whose label is hidden until the camera permission is given", () => {
    const devices = toCameraDevices([
      { kind: "videoinput", deviceId: "a", label: "" },
      { kind: "videoinput", deviceId: "b", label: "" },
    ]);

    expect(devices.map((d) => d.label)).toEqual(["Camera 1", "Camera 2"]);
  });

  it("puts real cameras before virtual ones and keeps the order inside each group", () => {
    const devices = toCameraDevices([
      { kind: "videoinput", deviceId: "obs", label: "OBS Virtual Camera" },
      { kind: "videoinput", deviceId: "a", label: "Cam A" },
      { kind: "videoinput", deviceId: "snap", label: "Snap Camera" },
      { kind: "videoinput", deviceId: "b", label: "Cam B" },
    ]);

    expect(devices.map((d) => d.id)).toEqual(["a", "b", "obs", "snap"]);
  });

  it("skips entries without an id", () => {
    expect(toCameraDevices([{ kind: "videoinput", deviceId: "", label: "Ghost" }])).toEqual([]);
  });

  it("returns an empty list for nothing", () => {
    expect(toCameraDevices([])).toEqual([]);
  });
});

describe("chooseCamera", () => {
  const webcam = device("a", "Webcam");
  const obs = device("obs", "OBS Virtual Camera", true);

  it("prefers the saved camera when it is there", () => {
    expect(chooseCamera([webcam, obs], "obs")).toBe(obs);
  });

  it("falls back to the first real camera when the saved one is gone", () => {
    expect(chooseCamera([obs, webcam], "unplugged")).toBe(webcam);
  });

  it("picks the first real camera when nothing was saved", () => {
    expect(chooseCamera([obs, webcam], null)).toBe(webcam);
  });

  it("settles for a virtual camera when it is all there is", () => {
    expect(chooseCamera([obs], null)).toBe(obs);
  });

  it("returns null without cameras", () => {
    expect(chooseCamera([], "a")).toBeNull();
  });
});
