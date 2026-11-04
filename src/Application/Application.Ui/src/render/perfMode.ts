/**
 * What `?perf` asks of the page: nothing (`off`), the frame meter with its overlay (`show`), or the meter and its reports to the manager
 * without the overlay in the way of the game (`hide`, from `?perf=hide`). `?perf=off` turns it off again.
 */
export type PerfMode = "off" | "show" | "hide";

const STORAGE_KEY = "snake.perf";

type ModeStorage = Pick<Storage, "getItem" | "setItem" | "removeItem">;

export function perfModeOf(search: string): PerfMode {
  const params = new URLSearchParams(search);
  if (!params.has("perf")) return "off";
  const value = params.get("perf");
  if (value === "off") return "off";
  return value === "hide" ? "hide" : "show";
}

/**
 * The mode of this tab: what the address asks for, kept for the rest of the tab because going from the home page to the game drops the
 * query. Only an address with `?perf` changes it.
 */
export function resolvePerfMode(search: string, storage: ModeStorage | null): PerfMode {
  if (!new URLSearchParams(search).has("perf")) return recall(storage);
  const asked = perfModeOf(search);
  remember(storage, asked);
  return asked;
}

/** The mode of this page, from its address and what the tab remembers. */
export function currentPerfMode(): PerfMode {
  if (typeof window === "undefined") return "off";
  return resolvePerfMode(window.location.search, tabStorage());
}

function tabStorage(): ModeStorage | null {
  try {
    return window.sessionStorage;
  } catch {
    return null;
  }
}

function remember(storage: ModeStorage | null, mode: PerfMode) {
  try {
    if (mode === "off") storage?.removeItem(STORAGE_KEY);
    else storage?.setItem(STORAGE_KEY, mode);
  } catch {
    // Without storage the mode lasts as long as the address has it.
  }
}

function recall(storage: ModeStorage | null): PerfMode {
  try {
    const saved = storage?.getItem(STORAGE_KEY);
    return saved === "show" || saved === "hide" ? saved : "off";
  } catch {
    return "off";
  }
}
