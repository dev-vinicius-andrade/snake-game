import { onBeforeUnmount, ref } from "vue";

/**
 * A button that needs two clicks, so a click by mistake does not throw away something that cannot be undone (leaving a game). The first
 * click only arms it, and it disarms by itself after `waitMs`; the second click inside that time does the action.
 */
export function useConfirmClick(waitMs = 3000) {
  const armed = ref(false);
  let timer: ReturnType<typeof setTimeout> | undefined;

  function cancel() {
    clearTimeout(timer);
    armed.value = false;
  }

  function click(action: () => void) {
    if (armed.value) {
      cancel();
      action();
      return;
    }
    armed.value = true;
    timer = setTimeout(cancel, waitMs);
  }

  onBeforeUnmount(() => clearTimeout(timer));
  return { armed, click, cancel };
}
