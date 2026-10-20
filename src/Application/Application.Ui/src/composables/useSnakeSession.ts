import { profileModal } from "./useProfileModal";
import { shallowRef, watch } from "vue";
import { InputArbiter } from "@/input/arbiter";
import type { ControlIntent } from "@/input/intents";
import type { useGameConnection } from "./useGameConnection";
import { useGamepadControls } from "./useGamepadControls";
import { useKeyboardControls, type KeyEventTarget } from "./useKeyboardControls";
import type { JoinTarget, useManagerJoin } from "./useManagerJoin";

export type SessionPhase = "idle" | "joining" | "playing" | "dead" | "error";

export interface SnakeSessionDeps {
  joiner: Pick<ReturnType<typeof useManagerJoin>, "join" | "cancel">;
  game: Pick<
    ReturnType<typeof useGameConnection>,
    "death" | "roomClosed" | "connect" | "changeDirection" | "steer" | "shoot" | "boost" | "leave"
  >;
  /** Where key events come from. Defaults to `window`. */
  keyboardTarget?: KeyEventTarget;
  /** True while the snake has the auto fire perk: holding the shoot control then keeps shooting. */
  autoFire?: () => boolean;
  /** Clock for input arbitration, in milliseconds. Defaults to `Date.now`. */
  now?: () => number;
}

type IntentHandlers = { [K in ControlIntent["type"]]: (intent: Extract<ControlIntent, { type: K }>) => void };

/**
 * One play session from the player's point of view: ask the manager for a game server, connect,
 * route controls to the game, notice death. Views only call `start`, `stop` and read the state.
 */
/** How often a held shoot control shoots with auto fire. The server's cooldown decides how many shots actually go. */
const AUTO_FIRE_MS = 90;

export function useSnakeSession({ joiner, game, keyboardTarget, autoFire = () => false, now = Date.now }: SnakeSessionDeps) {
  const arbiter = new InputArbiter();
  const phase = shallowRef<SessionPhase>("idle");
  const error = shallowRef<string | null>(null);
  let stopped = false;
  let boosting = false;
  let autoFireTimer: ReturnType<typeof setInterval> | null = null;

  function stopAutoFire() {
    if (autoFireTimer) clearInterval(autoFireTimer);
    autoFireTimer = null;
  }

  function startAutoFire() {
    stopAutoFire();
    if (autoFire()) autoFireTimer = setInterval(() => game.shoot(), AUTO_FIRE_MS);
  }

  const handlers: IntentHandlers = {
    direction: (intent) => game.changeDirection(intent.direction),
    steer: (intent) => game.steer(intent.angle),
    shoot: () => {
      game.shoot();
      startAutoFire();
    },
    shootRelease: () => stopAutoFire(),
    boost: (intent) => {
      boosting = intent.active;
      game.boost(intent.active);
    },
  };

  function handleIntent(intent: ControlIntent) {
    if (phase.value !== "playing") return;
    (handlers[intent.type] as (intent: ControlIntent) => void)(intent);
  }

  const keyboard = useKeyboardControls({
    target: keyboardTarget,
    onIntent: (intent) => {
      arbiter.noteKeyboard(intent, now());
      handleIntent(intent);
    },
    enabled: () => phase.value === "playing" && !profileModal.open.value,
  });

  const gamepad = useGamepadControls({
    onIntent: (intent) => {
      if (intent.type === "steer" && !arbiter.allowsGazeSteer(now())) return;
      arbiter.noteKeyboard(intent, now());
      handleIntent(intent);
    },
    enabled: () => phase.value === "playing" && !profileModal.open.value,
  });

  // The profile modal over the game: let go of what is held, so nothing keeps boosting or shooting behind it, and take the keys back after.
  watch(profileModal.open, (open) => {
    if (phase.value !== "playing") return;
    if (open) {
      if (boosting) handlers.boost({ type: "boost", active: false });
      stopAutoFire();
      keyboard.detach();
    } else keyboard.attach();
  });

  watch(
    game.death,
    (death) => {
      if (!death || phase.value !== "playing") return;
      if (boosting) handlers.boost({ type: "boost", active: false });
      stopAutoFire();
      phase.value = "dead";
    },
    { flush: "sync" }
  );

  watch(
    game.roomClosed,
    (closed) => {
      if (!closed || phase.value !== "playing") return;
      if (boosting) handlers.boost({ type: "boost", active: false });
      keyboard.detach();
      gamepad.detach();
      stopAutoFire();
      phase.value = "error";
      error.value = "The room was closed by an administrator";
    },
    { flush: "sync" }
  );

  async function start(nickname: string, target?: JoinTarget) {
    stopped = false;
    boosting = false;
    error.value = null;
    phase.value = "joining";
    keyboard.detach();
    gamepad.detach();
    try {
      const assignment = await joiner.join(nickname.trim(), target);
      if (stopped) return;
      await game.connect(assignment);
      if (stopped) return;
      phase.value = "playing";
      keyboard.attach();
      gamepad.attach();
    } catch (failure) {
      if (stopped) return;
      phase.value = "error";
      error.value = failure instanceof Error ? failure.message : String(failure);
    }
  }

  async function stop() {
    stopped = true;
    keyboard.detach();
    gamepad.detach();
    stopAutoFire();
    phase.value = "idle";
    await joiner.cancel();
    await game.leave();
  }

  /** False while a recent keyboard turn should hold pointer and gaze steering back. */
  const allowsGazeSteer = (at: number) => arbiter.allowsGazeSteer(at);

  return { phase, error, start, stop, handleIntent, allowsGazeSteer };
}
