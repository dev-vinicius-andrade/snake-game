/** A camera the browser knows about. */
export interface CameraDevice {
  id: string;
  label: string;
  /** Looks like software (OBS, Snap Camera, ...) rather than a real webcam. */
  virtual: boolean;
}

/** The part of `MediaDeviceInfo` this module reads. */
export interface MediaDeviceInfoLike {
  kind: string;
  deviceId: string;
  label: string;
}

/**
 * Stream and capture software installs cameras that deliver whatever it produces, not what is in front of the lens.
 * `\bobs\b` leaves real cameras such as the OBSBOT alone.
 */
const VIRTUAL_CAMERA = /virtual|\bobs\b|\bndi\b|snap camera|vcam|manycam|streamlabs/i;

export const isLikelyVirtualCamera = (label: string): boolean => VIRTUAL_CAMERA.test(label);

/**
 * The video inputs among the devices, real cameras first. Browsers hide labels until the camera permission is
 * given, so unnamed cameras are called "Camera 1", "Camera 2" and so on.
 */
export function toCameraDevices(devices: readonly MediaDeviceInfoLike[]): CameraDevice[] {
  const cameras = devices
    .filter((device) => device.kind === "videoinput" && device.deviceId)
    .map((device, index) => {
      const label = device.label || `Camera ${index + 1}`;
      return { id: device.deviceId, label, virtual: isLikelyVirtualCamera(label) };
    });
  return [...cameras.filter((camera) => !camera.virtual), ...cameras.filter((camera) => camera.virtual)];
}

/** The saved camera if it is still there, else the first real camera, else whatever there is. */
export function chooseCamera(devices: readonly CameraDevice[], preferredId: string | null): CameraDevice | null {
  return (
    devices.find((device) => device.id === preferredId) ??
    devices.find((device) => !device.virtual) ??
    devices[0] ??
    null
  );
}
