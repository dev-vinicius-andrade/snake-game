import { onBeforeUnmount, onMounted } from "vue";
import { resolveManagerUrl } from "@/config/env";
import type { FramePerf } from "@/render/framePerf";
import { currentPerfMode } from "@/render/perfMode";
import { startPerfReporting, type PerfContext } from "@/render/perfReport";

/** What the page knows of the player's game that the frame meter does not. */
export type PerfGameState = Pick<PerfContext, "score" | "ping" | "switches">;

const newSession = () => (typeof crypto !== "undefined" && crypto.randomUUID ? crypto.randomUUID() : `s${Date.now().toString(36)}${Math.random().toString(36).slice(2, 8)}`);

/**
 * With `?perf` or `?perf=hide`: tells the manager how the game runs on this device every few seconds, so it can be read there instead of from
 * screenshots. The report holds numbers and a random id of this page; nothing of the player.
 */
export function usePerfReporting(state: () => PerfGameState) {
  if (currentPerfMode() === "off") return;

  const session = newSession();
  const url = `${resolveManagerUrl()}/telemetry/perf`;
  let stop: (() => void) | null = null;

  onMounted(() => {
    stop = startPerfReporting({
      perf: () => (window as unknown as { __snakePerf?: FramePerf }).__snakePerf,
      context: () => ({ session, userAgent: navigator.userAgent, ...state() }),
      send: (report) =>
        fetch(url, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(report), keepalive: true }),
    });
  });
  onBeforeUnmount(() => stop?.());
}
