import { computed, getCurrentScope, onScopeDispose, shallowRef, watch, type Ref } from "vue";
import { advanceToasts, emptyToastQueue, enqueueToasts, nextToastChange, type ToastQueueState } from "@/game/toastQueue";

export interface UnlockEvent {
  seq: number;
  ids: string[];
}

export interface UseAchievementToastsOptions {
  /** The events from `useGameConnection().unlocked`. */
  events: Ref<readonly UnlockEvent[]>;
  /** Clock in milliseconds. Defaults to `Date.now`. */
  now?: () => number;
}

/** Turns unlock events into one toast at a time: `current` is the id to show, or null. */
export function useAchievementToasts({ events, now = Date.now }: UseAchievementToastsOptions) {
  const state = shallowRef<ToastQueueState>(emptyToastQueue());
  const current = computed(() => state.value.current);
  let lastSeq = 0;
  let timer: ReturnType<typeof setTimeout> | null = null;

  function schedule() {
    if (timer !== null) clearTimeout(timer);
    timer = null;
    const wait = nextToastChange(state.value, now());
    if (wait === null) return;
    timer = setTimeout(() => {
      timer = null;
      state.value = advanceToasts(state.value, now());
      schedule();
    }, wait + 1);
  }

  watch(
    events,
    (list) => {
      if (list.length === 0) lastSeq = 0; // A new game session starts from zero.
      const fresh = list.filter((event) => event.seq > lastSeq);
      if (fresh.length === 0) return;
      lastSeq = fresh[fresh.length - 1].seq;
      state.value = enqueueToasts(state.value, fresh.flatMap((event) => event.ids), now());
      schedule();
    },
    { flush: "sync" }
  );

  function dispose() {
    if (timer !== null) clearTimeout(timer);
    timer = null;
  }
  if (getCurrentScope()) onScopeDispose(dispose);

  return { current, dispose };
}
