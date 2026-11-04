import { reactive } from "vue";

/** The parts of the game screen the frame meter's overlay (`?perf`) can turn off, to find out which one makes the page slow. */
export const PERF_SWITCHES = ["perks", "touch", "hud", "hints"] as const;

export type PerfSwitch = (typeof PERF_SWITCHES)[number];

/** Everything is on, so the game is the same without `?perf`. */
export const perfSwitches = reactive<Record<PerfSwitch, boolean>>({ perks: true, touch: true, hud: true, hints: true });

export function togglePerfSwitch(name: PerfSwitch) {
  perfSwitches[name] = !perfSwitches[name];
}
