import { describe, expect, it } from "vitest";
import type { Calibration } from "./calibration";
import { createCalibrationStorage, type StorageLike } from "./calibrationStorage";

const calibration: Calibration = { x: { offset: 0.45, gain: 0.7 }, y: { offset: 0.55, gain: 0.6 } };

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

describe("createCalibrationStorage", () => {
  it("has nothing stored at first", () => {
    expect(createCalibrationStorage(memoryStorage()).load()).toBeNull();
  });

  it("loads what it saved", () => {
    const storage = createCalibrationStorage(memoryStorage());

    storage.save(calibration);

    expect(storage.load()).toEqual(calibration);
  });

  it("stores under the given key", () => {
    const backing = memoryStorage();

    createCalibrationStorage(backing, "my.key").save(calibration);

    expect(Object.keys(backing.data)).toEqual(["my.key"]);
  });

  it("forgets after clear", () => {
    const storage = createCalibrationStorage(memoryStorage());
    storage.save(calibration);

    storage.clear();

    expect(storage.load()).toBeNull();
  });

  it.each(["not json", "{}", "null", '{"x":{"offset":0.5,"gain":0},"y":{"offset":0.5,"gain":0.5}}'])("ignores the stored value %s", (value) => {
    const storage = createCalibrationStorage(memoryStorage({ "snake.gazeCalibration": value }));

    expect(storage.load()).toBeNull();
  });

  it("survives a storage that throws, such as private browsing", () => {
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
    const storage = createCalibrationStorage(broken);

    expect(storage.load()).toBeNull();
    expect(() => storage.save(calibration)).not.toThrow();
    expect(() => storage.clear()).not.toThrow();
  });

  it("works without any storage", () => {
    const storage = createCalibrationStorage(null);

    expect(storage.load()).toBeNull();
    expect(() => storage.save(calibration)).not.toThrow();
  });
});
