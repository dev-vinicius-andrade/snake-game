import { describe, expect, it } from "vitest";
import { createCameraPreferenceStorage } from "./cameraPreference";
import type { StorageLike } from "./calibrationStorage";

function memoryStorage(initial: Record<string, string> = {}): StorageLike & { data: Record<string, string> } {
  const data = { ...initial };
  return {
    data,
    getItem: (key) => data[key] ?? null,
    setItem: (key, value) => {
      data[key] = value;
    },
    removeItem: (key) => {
      delete data[key];
    },
  };
}

describe("createCameraPreferenceStorage", () => {
  it("has no preference at first", () => {
    expect(createCameraPreferenceStorage(memoryStorage()).load()).toBeNull();
  });

  it("loads what it saved", () => {
    const storage = createCameraPreferenceStorage(memoryStorage());

    storage.save("cam-1");

    expect(storage.load()).toBe("cam-1");
  });

  it("forgets the preference when saving null", () => {
    const backing = memoryStorage();
    const storage = createCameraPreferenceStorage(backing);
    storage.save("cam-1");

    storage.save(null);

    expect(storage.load()).toBeNull();
    expect(backing.data).toEqual({});
  });

  it("stores under the given key", () => {
    const backing = memoryStorage();

    createCameraPreferenceStorage(backing, "my.camera").save("cam-1");

    expect(Object.keys(backing.data)).toEqual(["my.camera"]);
  });

  it("survives a storage that throws", () => {
    const broken: StorageLike = {
      getItem: () => {
        throw new Error("blocked");
      },
      setItem: () => {
        throw new Error("blocked");
      },
      removeItem: () => {
        throw new Error("blocked");
      },
    };
    const storage = createCameraPreferenceStorage(broken);

    expect(storage.load()).toBeNull();
    expect(() => storage.save("a")).not.toThrow();
    expect(() => storage.save(null)).not.toThrow();
  });

  it("works without any storage", () => {
    const storage = createCameraPreferenceStorage(null);

    expect(storage.load()).toBeNull();
    expect(() => storage.save("a")).not.toThrow();
  });
});
