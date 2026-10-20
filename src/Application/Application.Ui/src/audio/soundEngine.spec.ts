import { describe, expect, it } from "vitest";
import { FakeAudioContext } from "@/test/fakeAudio";
import { MASTER_CEILING, MIN_REPEAT_MS, WebAudioEngine } from "./soundEngine";
import { SOUNDS } from "./sounds";

function setup(options: { volume?: number; muted?: boolean } = {}) {
  const context = new FakeAudioContext();
  let created = 0;
  let clock = 1000;
  const engine = new WebAudioEngine({
    createContext: () => {
      created++;
      return context;
    },
    now: () => clock,
    ...options,
  });
  return { context, engine, created: () => created, advance: (ms: number) => (clock += ms) };
}

describe("web audio engine", () => {
  it("makes no audio context before the first gesture", () => {
    const { engine, created, context } = setup();
    engine.play("eat");
    expect(created()).toBe(0);
    expect(context.oscillators).toHaveLength(0);
  });
  it("unlocking creates the context once and resumes it", () => {
    const { engine, created, context } = setup();
    engine.unlock();
    engine.unlock();
    expect(created()).toBe(1);
    expect(context.resumed).toBe(1);
  });
  it("plays the notes of a sound as oscillators", () => {
    const { engine, context } = setup();
    engine.unlock();
    engine.play("kill");
    expect(context.oscillators).toHaveLength(SOUNDS.kill.length);
    expect(context.oscillators[0].type).toBe("square");
    expect(context.oscillators[0].started).toBeCloseTo(context.currentTime + SOUNDS.kill[0].start);
    expect(context.oscillators[0].stopped).toBeCloseTo(context.currentTime + SOUNDS.kill[0].start + SOUNDS.kill[0].duration);
  });
  it("slides the pitch when a note has an end pitch", () => {
    const { engine, context } = setup();
    engine.unlock();
    engine.play("eat");
    expect(context.oscillators[0].from).toEqual([SOUNDS.eat[0].from]);
    expect(context.oscillators[0].ramps).toContain(SOUNDS.eat[0].to);
  });
  it("uses one shared noise buffer for noise notes", () => {
    const { engine, context } = setup();
    engine.unlock();
    engine.play("mine");
    engine.play("lightning");
    expect(context.noiseSources).toBe(2);
    expect(context.buffersMade).toBe(1);
  });
  it("stays silent while muted, and plays again when unmuted", () => {
    const { engine, context } = setup({ muted: true });
    engine.unlock();
    engine.play("eat");
    expect(context.oscillators).toHaveLength(0);
    engine.setMuted(false);
    engine.play("eat");
    expect(context.oscillators.length).toBeGreaterThan(0);
  });
  it("stays silent at volume zero", () => {
    const { engine, context } = setup({ volume: 0 });
    engine.unlock();
    engine.play("eat");
    expect(context.oscillators).toHaveLength(0);
  });
  it("sets the master gain from volume and mute, never above the ceiling", () => {
    const { engine, context } = setup({ volume: 0.5 });
    engine.unlock();
    const master = context.gains[0];
    expect(master.value).toBeCloseTo(0.5 * MASTER_CEILING);
    engine.setVolume(5);
    expect(master.value).toBeCloseTo(MASTER_CEILING);
    engine.setMuted(true);
    expect(master.value).toBe(0);
    engine.setMuted(false);
    expect(master.value).toBeCloseTo(MASTER_CEILING);
  });
  it("applies a volume chosen before the first gesture", () => {
    const { engine, context } = setup();
    engine.setVolume(0.25);
    engine.unlock();
    expect(context.gains[0].value).toBeCloseTo(0.25 * MASTER_CEILING);
  });
  it("does not repeat the same sound inside the throttle window, but lets another through", () => {
    const { engine, context, advance } = setup();
    engine.unlock();
    engine.play("eat");
    advance(MIN_REPEAT_MS - 1);
    engine.play("eat");
    expect(context.oscillators).toHaveLength(SOUNDS.eat.length);
    engine.play("hit");
    expect(context.oscillators).toHaveLength(SOUNDS.eat.length + SOUNDS.hit.length);
    advance(2);
    engine.play("eat");
    expect(context.oscillators).toHaveLength(2 * SOUNDS.eat.length + SOUNDS.hit.length);
  });
  it("is silent and does not throw where there is no audio", () => {
    const none = new WebAudioEngine({ createContext: () => null });
    expect(() => {
      none.unlock();
      none.play("eat");
    }).not.toThrow();
    const broken = new WebAudioEngine({
      createContext: () => {
        throw new Error("no audio");
      },
    });
    expect(() => {
      broken.unlock();
      broken.play("eat");
    }).not.toThrow();
  });
});
