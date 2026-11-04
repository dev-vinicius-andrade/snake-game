import { nextTick, shallowRef } from "vue";
import { describe, expect, it } from "vitest";
import { useStableValue } from "./useStableValue";

describe("useStableValue", () => {
  const joined = (list: string[]) => list.join(",");

  it("starts with the value of the source", () => {
    const source = shallowRef(["a", "b"]);

    expect(useStableValue(() => source.value, joined).value).toEqual(["a", "b"]);
  });

  it("keeps the same array while a new one says the same", async () => {
    const source = shallowRef(["a", "b"]);
    const stable = useStableValue(() => source.value, joined);
    const first = stable.value;

    source.value = ["a", "b"];
    await nextTick();

    expect(stable.value).toBe(first);
  });

  it("takes the new array when what it says changes", async () => {
    const source = shallowRef(["a", "b"]);
    const stable = useStableValue(() => source.value, joined);

    source.value = ["a", "b", "c"];
    await nextTick();

    expect(stable.value).toEqual(["a", "b", "c"]);
  });

  it("does not tell its readers of a new array that says the same", async () => {
    const source = shallowRef(["a"]);
    const stable = useStableValue(() => source.value, joined);
    let reads = 0;
    const { watch } = await import("vue");
    watch(stable, () => reads++, { flush: "sync" });

    source.value = ["a"];
    source.value = ["a"];
    await nextTick();

    expect(reads).toBe(0);
  });
});
