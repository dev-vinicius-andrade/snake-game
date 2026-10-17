export type CameraErrorKind = "denied" | "no-camera" | "busy" | "unsupported" | "model" | "unknown";

/** A failure to get the camera or the face model running, with a kind the UI can explain. */
export class CameraError extends Error {
  constructor(readonly kind: CameraErrorKind, message: string, cause?: unknown) {
    super(message);
    this.name = "CameraError";
    if (cause !== undefined) this.cause = cause;
  }
}

const KIND_BY_NAME: Readonly<Record<string, CameraErrorKind>> = {
  NotAllowedError: "denied",
  PermissionDeniedError: "denied",
  SecurityError: "denied",
  NotFoundError: "no-camera",
  DevicesNotFoundError: "no-camera",
  OverconstrainedError: "no-camera",
  NotReadableError: "busy",
  TrackStartError: "busy",
  AbortError: "busy",
};

/** Sorts what `getUserMedia` and friends throw into the kinds the UI knows. */
export function classifyCameraError(error: unknown): CameraErrorKind {
  if (error instanceof CameraError) return error.kind;
  const name = typeof error === "object" && error !== null ? (error as { name?: unknown }).name : undefined;
  return typeof name === "string" && Object.hasOwn(KIND_BY_NAME, name) ? KIND_BY_NAME[name] : "unknown";
}

export interface NavigatorLike {
  mediaDevices?: { getUserMedia?: unknown };
}

/** False on insecure pages (not https or localhost) and old browsers, where the camera is not available at all. */
export function isCameraSupported(navigatorLike: NavigatorLike | null | undefined): boolean {
  return typeof navigatorLike?.mediaDevices?.getUserMedia === "function";
}
