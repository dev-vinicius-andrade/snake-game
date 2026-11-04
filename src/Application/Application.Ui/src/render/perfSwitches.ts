import { reactive } from "vue";

/** The parts of the game screen the frame meter's overlay (`?perf`) can turn off, to find out which one makes the page slow. */
export const PERF_SWITCHES = ["perks", "touch", "hud", "hints", "draw"] as const;

export type PerfSwitch = (typeof PERF_SWITCHES)[number];

/** Everything is on, so the game is the same without `?perf`. */
export const perfSwitches = reactive<Record<PerfSwitch, boolean>>({ perks: true, touch: true, hud: true, hints: true, draw: true });

export function togglePerfSwitch(name: PerfSwitch) {
  perfSwitches[name] = !perfSwitches[name];
}

/** `?off=perks,draw` turns those parts off from the start, for a run where the overlay is hidden (`?perf=hide`). */
export function presetPerfSwitches(search: string) {
  const named = new URLSearchParams(search).get("off")?.split(",") ?? [];
  for (const name of PERF_SWITCHES) if (named.includes(name)) perfSwitches[name] = false;
}
