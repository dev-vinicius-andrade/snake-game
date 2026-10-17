import { describe, expect, it } from "vitest";
import { createFaceModePreference } from "./faceModePreference";

const memory = (initial: Record<string, string> = {}) => {
  const values = new Map(Object.entries(initial));
  return {
    getItem: (key: string) => values.get(key) ?? null,
    setItem: (key: string, value: string) => void values.set(key, value),
    removeItem: (key: string) => void values.delete(key),
  };
};

describe("createFaceModePreference", () => {
  it("is empty at first", () => {
    expect(createFaceModePreference(memory()).load()).toBeNull();
  });

  it.each(["eyes", "head"] as const)("remembers %s", (mode) => {
    const storage = memory();
    createFaceModePreference(storage).save(mode);

    expect(createFaceModePreference(storage).load()).toBe(mode);
  });

  it("ignores values it does not know", () => {
    expect(createFaceModePreference(memory({ "snake.faceMode": "feet" })).load()).toBeNull();
  });

  it("survives storage that throws or is missing", () => {
    const broken = {
      getItem: () => {
        throw new Error("blocked");
      },
      setItem: () => {
        throw new Error("blocked");
      },
      removeItem: () => undefined,
    };

    expect(createFaceModePreference(broken).load()).toBeNull();
    expect(() => createFaceModePreference(broken).save("head")).not.toThrow();
    expect(createFaceModePreference(null).load()).toBeNull();
  });
});
