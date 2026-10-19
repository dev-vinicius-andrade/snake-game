import type { DamageEvent, DamageKind } from "@/types/game";

const KINDS: readonly DamageKind[] = ["hit", "broke", "kill", "lost"];

const isDictionary = (value: unknown): value is Record<string, unknown> =>
  typeof value === "object" && value !== null && !Array.isArray(value);
const isNumber = (value: unknown): value is number => typeof value === "number" && Number.isFinite(value);

/** Turns the `DamageDealt` payload into the client model. Null when it says too little to draw. */
export function parseDamage(raw: unknown): DamageEvent | null {
  if (!isDictionary(raw)) return null;
  if (typeof raw.shooterGuid !== "string" || typeof raw.targetGuid !== "string") return null;
  if (!isNumber(raw.x) || !isNumber(raw.y)) return null;
  return {
    shooterGuid: raw.shooterGuid,
    targetGuid: raw.targetGuid,
    x: raw.x,
    y: raw.y,
    damage: isNumber(raw.damage) && raw.damage > 0 ? raw.damage : 0,
    kind: KINDS.find((kind) => kind === raw.kind) ?? "hit",
  };
}
