import { describe, expect, it } from "vitest";
import { EyeActions } from "./eyeActions";
import type { GazeSample } from "./gazeSource";

const room = { width: 1000, height: 1000 };

/** A camera sample looking at the middle of the stage with open eyes, with the given fields changed. */
const sample = (t: number, overrides: Partial<GazeSample> = {}): GazeSample => ({ t, x: 0.5, y: 0.5, present: true, blink: 0, ...overrides });

/** An EyeActions that trusts the face at once, so specs do not need a warm up. */
const create = (options: ConstructorParameters<typeof EyeActions>[0] = {}) =>
  new EyeActions({ tracking: { recoverAfterMs: 0, lostAfterMs: 500 }, ...options });

describe("EyeActions tracking", () => {
  it("is lost until a face was seen for a moment", () => {
    const actions = new EyeActions();

    expect(actions.process(sample(0), room).tracking).toBeNull();
    expect(actions.state).toBe("lost");
    expect(actions.process(sample(150), room).tracking).toBe("tracking");
    expect(actions.state).toBe("tracking");
  });

  it("reports when the face is lost and found again", () => {
    const actions = create();
    actions.process(sample(0), room);

    expect(actions.process(sample(100, { present: false }), room).tracking).toBeNull();
    expect(actions.process(sample(700, { present: false }), room).tracking).toBe("lost");
    expect(actions.process(sample(800), room).tracking).toBe("tracking");
  });

  it("gives no intents while tracking is lost", () => {
    const actions = new EyeActions();

    const result = actions.process(sample(0, { blink: 0.9, x: 1 }), room);

    expect(result.intents).toEqual([]);
  });
});

describe("EyeActions blink to shoot", () => {
  it("shoots when a deliberate blink ends", () => {
    const actions = create();
    actions.process(sample(0), room);
    actions.process(sample(100, { blink: 0.9 }), room);

    expect(actions.process(sample(500, { blink: 0.9 }), room).intents).toEqual([]);
    expect(actions.process(sample(700, { blink: 0.1 }), room).intents).toEqual([{ type: "shoot" }]);
  });

  it("ignores a natural blink", () => {
    const actions = create();
    actions.process(sample(0), room);
    actions.process(sample(100, { blink: 0.9 }), room);

    expect(actions.process(sample(250, { blink: 0.1 }), room).intents).toEqual([]);
  });

  it("does not shoot for samples that carry no blink, such as the pointer", () => {
    const actions = create();
    const pointer = (t: number): GazeSample => ({ t, x: 0.5, y: 0.5 });
    actions.process(pointer(0), room);

    expect([100, 700, 1400].flatMap((t) => actions.process(pointer(t), room).intents)).toEqual([]);
  });

  it("forgets a blink that was under way when the face got lost", () => {
    const actions = create();
    actions.process(sample(0), room);
    actions.process(sample(100, { blink: 0.9 }), room);
    actions.process(sample(200, { present: false }), room);
    actions.process(sample(800, { present: false }), room);
    actions.process(sample(900), room);

    expect(actions.process(sample(1000, { blink: 0.1 }), room).intents).toEqual([]);
  });
});

describe("EyeActions far gaze boost", () => {
  const far = (t: number) => sample(t, { x: 1, y: 0.5 });

  it("boosts after the gaze stayed far from the middle", () => {
    const actions = create();
    actions.process(far(0), room);

    expect(actions.process(far(200), room).intents).toEqual([]);
    expect(actions.process(far(400), room).intents).toEqual([{ type: "boost", active: true }]);
  });

  it("stops boosting when the gaze comes back", () => {
    const actions = create();
    actions.process(far(0), room);
    actions.process(far(400), room);

    expect(actions.process(sample(500), room).intents).toEqual([{ type: "boost", active: false }]);
  });

  it("measures far in world units, so it is the same in every direction", () => {
    const actions = create();
    const wide = { width: 2000, height: 1000 };
    // 0.9 of the width is 800 units from the middle, 0.9 of the height only 400 units: both beyond 0.4 * 1000.
    actions.process(sample(0, { x: 0.9, y: 0.5 }), wide);

    expect(actions.process(sample(400, { x: 0.9, y: 0.5 }), wide).intents).toEqual([{ type: "boost", active: true }]);
  });

  it("does not boost for a gaze that stays inside the far radius", () => {
    const actions = create();
    actions.process(sample(0, { x: 0.8 }), room);

    expect(actions.process(sample(2000, { x: 0.8 }), room).intents).toEqual([]);
  });

  it("ends the boost when the face is lost", () => {
    const actions = create();
    actions.process(far(0), room);
    actions.process(far(400), room);
    actions.process(sample(500, { present: false }), room);

    const result = actions.process(sample(1100, { present: false }), room);

    expect(result.tracking).toBe("lost");
    expect(result.intents).toEqual([{ type: "boost", active: false }]);
  });

  it("reset ends a running boost", () => {
    const actions = create();
    actions.process(far(0), room);
    actions.process(far(400), room);

    expect(actions.reset()).toEqual([{ type: "boost", active: false }]);
    expect(actions.reset()).toEqual([]);
    expect(actions.state).toBe("lost");
  });
});
