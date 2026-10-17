import { describe, expect, it } from "vitest";
import type { GazeSample } from "./gazeSource";
import { HeadActions } from "./headActions";

const sample = (t: number, overrides: Partial<GazeSample> = {}): GazeSample => ({ t, x: 0.5, y: 0.5, present: true, jaw: 0, brows: 0, ...overrides });
const create = () => new HeadActions({ tracking: { recoverAfterMs: 0, lostAfterMs: 500 } });

describe("HeadActions tracking", () => {
  it("is lost until a face was seen for a moment", () => {
    const actions = new HeadActions();

    expect(actions.process(sample(0)).tracking).toBeNull();
    expect(actions.state).toBe("lost");
    expect(actions.process(sample(150)).tracking).toBe("tracking");
  });

  it("reports when the face is lost and found again", () => {
    const actions = create();
    actions.process(sample(0));

    expect(actions.process(sample(100, { present: false })).tracking).toBeNull();
    expect(actions.process(sample(700, { present: false })).tracking).toBe("lost");
    expect(actions.process(sample(800)).tracking).toBe("tracking");
  });

  it("gives no intents while the face is not tracked", () => {
    const actions = new HeadActions();

    expect(actions.process(sample(0, { jaw: 1, brows: 1 })).intents).toEqual([]);
  });
});

describe("HeadActions open mouth to shoot", () => {
  it("shoots when the mouth was open long enough", () => {
    const actions = create();
    actions.process(sample(0));
    actions.process(sample(100, { jaw: 0.9 }));

    expect(actions.process(sample(200, { jaw: 0.9 })).intents).toEqual([{ type: "shoot" }]);
  });

  it("shoots once per mouth opening", () => {
    const actions = create();
    actions.process(sample(0));
    actions.process(sample(100, { jaw: 0.9 }));
    actions.process(sample(200, { jaw: 0.9 }));

    expect(actions.process(sample(900, { jaw: 0.9 })).intents).toEqual([]);
  });

  it("ignores talking or a short twitch", () => {
    const actions = create();
    actions.process(sample(0));
    actions.process(sample(100, { jaw: 0.9 }));

    expect(actions.process(sample(160, { jaw: 0.1 })).intents).toEqual([]);
  });

  it("does not shoot for samples without a mouth reading", () => {
    const actions = create();
    const pointer = (t: number): GazeSample => ({ t, x: 0.5, y: 0.5 });
    actions.process(pointer(0));

    expect([100, 800, 1500].flatMap((t) => actions.process(pointer(t)).intents)).toEqual([]);
  });
});

describe("HeadActions raised eyebrows to boost", () => {
  it("boosts while the eyebrows stay raised", () => {
    const actions = create();
    actions.process(sample(0, { brows: 0.9 }));

    expect(actions.process(sample(200, { brows: 0.9 })).intents).toEqual([]);
    expect(actions.process(sample(300, { brows: 0.9 })).intents).toEqual([{ type: "boost", active: true }]);
  });

  it("stops when the eyebrows come down", () => {
    const actions = create();
    actions.process(sample(0, { brows: 0.9 }));
    actions.process(sample(300, { brows: 0.9 }));

    expect(actions.process(sample(400, { brows: 0.1 })).intents).toEqual([{ type: "boost", active: false }]);
  });

  it("ends the boost when the face is lost", () => {
    const actions = create();
    actions.process(sample(0, { brows: 0.9 }));
    actions.process(sample(300, { brows: 0.9 }));
    actions.process(sample(400, { present: false }));

    const result = actions.process(sample(1000, { present: false }));

    expect(result.tracking).toBe("lost");
    expect(result.intents).toEqual([{ type: "boost", active: false }]);
  });
});

describe("HeadActions when not acting", () => {
  it("still follows the face but fires nothing", () => {
    const actions = create();

    const result = actions.process(sample(0, { jaw: 0.9, brows: 0.9 }), { act: false });

    expect(result).toEqual({ intents: [], tracking: "tracking" });
  });

  it("does not count the time it was not acting", () => {
    const actions = create();
    actions.process(sample(0, { brows: 0.9 }), { act: false });
    actions.process(sample(1000, { brows: 0.9 }), { act: false });

    expect(actions.process(sample(1100, { brows: 0.9 })).intents).toEqual([]);
    expect(actions.process(sample(1400, { brows: 0.9 })).intents).toEqual([{ type: "boost", active: true }]);
  });

  it("ends a running boost when it stops acting", () => {
    const actions = create();
    actions.process(sample(0, { brows: 0.9 }));
    actions.process(sample(300, { brows: 0.9 }));

    expect(actions.process(sample(400, { brows: 0.9 }), { act: false }).intents).toEqual([{ type: "boost", active: false }]);
  });
});

describe("HeadActions reset", () => {
  it("ends a running boost and forgets the face", () => {
    const actions = create();
    actions.process(sample(0, { brows: 0.9 }));
    actions.process(sample(300, { brows: 0.9 }));

    expect(actions.reset()).toEqual([{ type: "boost", active: false }]);
    expect(actions.reset()).toEqual([]);
    expect(actions.state).toBe("lost");
  });
});
