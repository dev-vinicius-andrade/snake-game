import { shallowRef, watch } from "vue";
import type { ControlIntent } from "@/input/keyboard";
import type { useGameConnection } from "./useGameConnection";
import { useKeyboardControls, type KeyEventTarget } from "./useKeyboardControls";
import type { useManagerJoin } from "./useManagerJoin";

export type SessionPhase = "idle" | "joining" | "playing" | "dead" | "error";

export interface SnakeSessionDeps {
  joiner: Pick<ReturnType<typeof useManagerJoin>, "join" | "cancel">;
  game: Pick<
    ReturnType<typeof useGameConnection>,
    "death" | "connect" | "changeDirection" | "shoot" | "boost" | "leave"
  >;
  /** Where key events come from. Defaults to `window`. */
  keyboardTarget?: KeyEventTarget;
}

type IntentHandlers = { [K in ControlIntent["type"]]: (intent: Extract<ControlIntent, { type: K }>) => void };

/**
 * One play session from the player's point of view: ask the manager for a game server, connect,
 * route controls to the game, notice death. Views only call `start`, `stop` and read the state.
 */
export function useSnakeSession({ joiner, game, keyboardTarget }: SnakeSessionDeps) {
  const phase = shallowRef<SessionPhase>("idle");
  const error = shallowRef<string | null>(null);
  let stopped = false;
  let boosting = false;

  const handlers: IntentHandlers = {
    direction: (intent) => game.changeDirection(intent.direction),
    shoot: () => game.shoot(),
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
    onIntent: handleIntent,
    enabled: () => phase.value === "playing",
  });

  watch(
    game.death,
    (death) => {
      if (!death || phase.value !== "playing") return;
      if (boosting) handlers.boost({ type: "boost", active: false });
      phase.value = "dead";
    },
    { flush: "sync" }
  );

  async function start(nickname: string, roomId?: string) {
    stopped = false;
    boosting = false;
    error.value = null;
    phase.value = "joining";
    keyboard.detach();
    try {
      const assignment = await joiner.join(nickname.trim(), roomId);
      if (stopped) return;
      await game.connect(assignment);
      if (stopped) return;
      phase.value = "playing";
      keyboard.attach();
    } catch (failure) {
      if (stopped) return;
      phase.value = "error";
      error.value = failure instanceof Error ? failure.message : String(failure);
    }
  }

  async function stop() {
    stopped = true;
    keyboard.detach();
    phase.value = "idle";
    await joiner.cancel();
    await game.leave();
  }

  return { phase, error, start, stop, handleIntent };
}
