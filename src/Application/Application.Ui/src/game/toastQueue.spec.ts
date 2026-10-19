import { describe, expect, it } from "vitest";
import { TOAST_GAP_MS, TOAST_MS, advanceToasts, emptyToastQueue, enqueueToasts, nextToastChange } from "./toastQueue";

describe("toast queue", () => {
  it("shows the first at once", () => {
    const state = enqueueToasts(emptyToastQueue(), ["a", "b"], 1000);
    expect(state).toEqual({ current: "a", until: 1000 + TOAST_MS, waiting: ["b"] });
  });
  it("keeps the current one until its time is up", () => {
    const state = enqueueToasts(emptyToastQueue(), ["a", "b"], 0);
    expect(advanceToasts(state, TOAST_MS - 1).current).toBe("a");
  });
  it("starts the next one after the gap", () => {
    const state = enqueueToasts(emptyToastQueue(), ["a", "b"], 0);
    const gap = advanceToasts(state, TOAST_MS);
    expect(gap.current).toBeNull();
    expect(advanceToasts(gap, TOAST_MS + TOAST_GAP_MS).current).toBe("b");
  });
  it("catches up when the clock jumped past several toasts", () => {
    const state = enqueueToasts(emptyToastQueue(), ["a", "b"], 0);
    const later = advanceToasts(state, 100_000);
    expect(later.current).toBe("b");
    expect(later.until).toBe(100_000 + TOAST_MS);
  });
  it("ignores an id already showing or waiting", () => {
    const state = enqueueToasts(enqueueToasts(emptyToastQueue(), ["a", "b"], 0), ["a", "b", "c"], 10);
    expect([state.current, ...state.waiting]).toEqual(["a", "b", "c"]);
  });
  it("tells when to wake up", () => {
    expect(nextToastChange(emptyToastQueue(), 0)).toBeNull();
    expect(nextToastChange(enqueueToasts(emptyToastQueue(), ["a"], 0), 1000)).toBe(TOAST_MS - 1000);
  });
});
