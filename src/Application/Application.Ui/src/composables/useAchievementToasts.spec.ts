import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { shallowRef } from "vue";
import { TOAST_GAP_MS, TOAST_MS } from "@/game/toastQueue";
import { useAchievementToasts, type UnlockEvent } from "./useAchievementToasts";

describe("useAchievementToasts", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });
  afterEach(() => {
    vi.useRealTimers();
  });

  function setup() {
    const events = shallowRef<UnlockEvent[]>([]);
    const toasts = useAchievementToasts({ events, now: () => Date.now() });
    return { events, toasts };
  }

  it("shows a toast at once and hides it after its time", () => {
    const { events, toasts } = setup();

    events.value = [{ seq: 1, ids: ["first-blood"] }];
    expect(toasts.current.value).toBe("first-blood");

    vi.advanceTimersByTime(TOAST_MS + 5);
    expect(toasts.current.value).toBeNull();
  });

  it("shows several one after the other without overlap", () => {
    const { events, toasts } = setup();

    events.value = [{ seq: 1, ids: ["a", "b"] }];
    expect(toasts.current.value).toBe("a");

    vi.advanceTimersByTime(TOAST_MS + 5);
    expect(toasts.current.value).toBeNull();

    vi.advanceTimersByTime(TOAST_GAP_MS + 5);
    expect(toasts.current.value).toBe("b");

    vi.advanceTimersByTime(TOAST_MS + 5);
    expect(toasts.current.value).toBeNull();
  });

  it("queues an event that arrives while one is showing, and ignores events already seen", () => {
    const { events, toasts } = setup();
    events.value = [{ seq: 1, ids: ["a"] }];
    vi.advanceTimersByTime(1000);

    events.value = [...events.value, { seq: 2, ids: ["b"] }];
    expect(toasts.current.value).toBe("a");

    vi.advanceTimersByTime(TOAST_MS - 1000 + TOAST_GAP_MS + 10);
    expect(toasts.current.value).toBe("b");
  });

  it("starts over when the list is cleared for a new session", () => {
    const { events, toasts } = setup();
    events.value = [{ seq: 1, ids: ["a"] }];
    vi.advanceTimersByTime(TOAST_MS + TOAST_GAP_MS + 10);

    events.value = [];
    events.value = [{ seq: 1, ids: ["b"] }];

    expect(toasts.current.value).toBe("b");
  });
});
