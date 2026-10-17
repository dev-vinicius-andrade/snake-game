/** A 4x4 face transformation matrix as MediaPipe returns it: column-major, `data` holds 16 numbers. */
export interface HeadMatrix {
  data: ArrayLike<number>;
}

/**
 * Where the head points, in radians: `x` is the turn left or right (yaw), `y` the nod up or down (pitch). Zero
 * is a face looking straight into the camera. Signs follow the matrix; calibration decides the screen direction.
 */
export interface HeadSignals {
  x: number;
  y: number;
}

const FORWARD_X = 8;
const FORWARD_Y = 9;
const FORWARD_Z = 10;
const MATRIX_LENGTH = 16;

/** Null when the matrix is missing, too short, not finite, or has no forward direction. Position is ignored. */
export function extractHeadSignals(matrix: HeadMatrix | null | undefined): HeadSignals | null {
  if (!matrix || matrix.data.length < MATRIX_LENGTH) return null;
  const forwardX = matrix.data[FORWARD_X];
  const forwardY = matrix.data[FORWARD_Y];
  const forwardZ = matrix.data[FORWARD_Z];
  if (![forwardX, forwardY, forwardZ].every(Number.isFinite)) return null;
  if (Math.hypot(forwardX, forwardY, forwardZ) < 1e-9) return null;

  return { x: Math.atan2(forwardX, forwardZ), y: Math.atan2(forwardY, Math.hypot(forwardX, forwardZ)) };
}
