/**
 * What `?perf` asks of the page: nothing (`off`), the frame meter with its overlay (`show`), or the meter and its reports to the manager
 * without the overlay in the way of the game (`hide`, from `?perf=hide`).
 */
export type PerfMode = "off" | "show" | "hide";

export function perfModeOf(search: string): PerfMode {
  const params = new URLSearchParams(search);
  if (!params.has("perf")) return "off";
  return params.get("perf") === "hide" ? "hide" : "show";
}
