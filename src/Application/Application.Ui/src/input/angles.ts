const FULL_TURN = 2 * Math.PI;

/** Smallest absolute difference between two angles in radians, in [0, PI]. PI and -PI are the same direction. */
export function angleDistance(a: number, b: number): number {
  const difference = Math.abs(a - b) % FULL_TURN;
  return difference > Math.PI ? FULL_TURN - difference : difference;
}
