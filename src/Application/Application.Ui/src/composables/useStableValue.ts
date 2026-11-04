import { shallowRef, watch, type ShallowRef } from "vue";

/**
 * A value that is only replaced when what it says changes. Every snapshot brings new arrays for the same perks, and a component that is
 * handed a new array re-renders even though nothing on screen would differ: with a dozen tooltips that was 40 ms of a phone's time per snapshot.
 */
export function useStableValue<T>(source: () => T, keyOf: (value: T) => string): Readonly<ShallowRef<T>> {
  const stable = shallowRef(source()) as ShallowRef<T>;
  let key = keyOf(stable.value);
  watch(
    source,
    (value) => {
      const next = keyOf(value);
      if (next === key) return;
      key = next;
      stable.value = value;
    },
    { flush: "sync" }
  );
  return stable;
}
