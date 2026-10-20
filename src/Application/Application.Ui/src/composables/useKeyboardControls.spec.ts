import { describe, expect, it, vi } from "vitest";
import type { ControlIntent } from "@/input/keyboard";
import { useKeyboardControls, type KeyEventTarget } from "./useKeyboardControls";

class FakeTarget implements KeyEventTarget {
  private readonly listeners = new Map<string, Set<(event: any) => void>>();

  addEventListener(type: string, listener: (event: any) => void) {
    const set = this.listeners.get(type) ?? new Set();
    set.add(listener);
    this.listeners.set(type, set);
  }

  removeEventListener(type: string, listener: (event: any) => void) {
    this.listeners.get(type)?.delete(listener);
  }

  listenerCount(type: string) {
    return this.listeners.get(type)?.size ?? 0;
  }

  key(type: "keydown" | "keyup", code: string, repeat = false) {
    const event = { code, repeat, preventDefault: vi.fn() };
    this.listeners.get(type)?.forEach((listener) => listener(event));
    return event;
  }

  mouse(type: "mousedown" | "mouseup", button: number, element: unknown = { closest: () => null }) {
    const event = { button, target: element, preventDefault: vi.fn() };
    this.listeners.get(type)?.forEach((listener) => listener(event));
    return event;
  }

  contextMenu() {
    const event = { preventDefault: vi.fn() };
    this.listeners.get("contextmenu")?.forEach((listener) => listener(event));
    return event;
  }

  blur() {
    this.listeners.get("blur")?.forEach((listener) => listener({}));
  }
}

function setup(enabled?: () => boolean) {
  const target = new FakeTarget();
  const intents: ControlIntent[] = [];
  const controls = useKeyboardControls({ target, onIntent: (intent) => intents.push(intent), enabled });
  return { target, intents, controls };
}

describe("useKeyboardControls", () => {
  it("does nothing until attached", () => {
    const { target, intents } = setup();

    target.key("keydown", "ArrowUp");

    expect(intents).toEqual([]);
  });

  it("turns key presses into intents once attached", () => {
    const { target, intents, controls } = setup();
    controls.attach();

    target.key("keydown", "ArrowUp");
    target.key("keydown", "Space");
    target.key("keyup", "Space");
    target.key("keydown", "ShiftLeft");
    target.key("keyup", "ShiftLeft");

    expect(intents).toEqual([
      { type: "direction", direction: "up" },
      { type: "shoot" },
      { type: "shootRelease" },
      { type: "boost", active: true },
      { type: "boost", active: false },
    ]);
  });

  it("prevents the browser default only for keys it handles", () => {
    const { target, controls } = setup();
    controls.attach();

    expect(target.key("keydown", "ArrowDown").preventDefault).toHaveBeenCalled();
    expect(target.key("keydown", "Space").preventDefault).toHaveBeenCalled();
    expect(target.key("keydown", "KeyZ").preventDefault).not.toHaveBeenCalled();
  });

  it("ignores auto repeat shots", () => {
    const { target, intents, controls } = setup();
    controls.attach();

    target.key("keydown", "Space");
    target.key("keydown", "Space", true);

    expect(intents).toEqual([{ type: "shoot" }]);
  });

  it("stops boosting when the window loses focus", () => {
    const { target, intents, controls } = setup();
    controls.attach();
    target.key("keydown", "ShiftLeft");

    target.blur();

    expect(intents[intents.length - 1]).toEqual({ type: "boost", active: false });
  });

  it("ignores keys while disabled but keeps tracking nothing", () => {
    let enabled = false;
    const { target, intents, controls } = setup(() => enabled);
    controls.attach();

    target.key("keydown", "ArrowUp");
    enabled = true;
    target.key("keydown", "ArrowLeft");

    expect(intents).toEqual([{ type: "direction", direction: "left" }]);
  });

  it("does not prevent the default for keys pressed while disabled", () => {
    const { target, controls } = setup(() => false);
    controls.attach();

    expect(target.key("keydown", "Space").preventDefault).not.toHaveBeenCalled();
  });

  it("detach removes every listener", () => {
    const { target, intents, controls } = setup();
    controls.attach();

    controls.detach();
    target.key("keydown", "ArrowUp");

    expect(intents).toEqual([]);
    expect(["keydown", "keyup", "blur"].map((type) => target.listenerCount(type))).toEqual([0, 0, 0]);
  });

  it("attaching twice does not double the listeners", () => {
    const { target, intents, controls } = setup();

    controls.attach();
    controls.attach();
    target.key("keydown", "ArrowUp");

    expect(intents).toHaveLength(1);
  });

  it("a fresh attach starts from a clean key state", () => {
    const { target, intents, controls } = setup();
    controls.attach();
    target.key("keydown", "ArrowUp");
    controls.detach();

    controls.attach();
    target.key("keydown", "ArrowUp");

    expect(intents).toEqual([
      { type: "direction", direction: "up" },
      { type: "direction", direction: "up" },
    ]);
  });

  describe("mouse", () => {
    it("shoots on a left click and boosts while the right button is held", () => {
      const { target, intents, controls } = setup();
      controls.attach();

      target.mouse("mousedown", 0);
      target.mouse("mousedown", 2);
      target.mouse("mouseup", 2);

      expect(intents).toEqual([{ type: "shoot" }, { type: "boost", active: true }, { type: "boost", active: false }]);
    });

    it("leaves clicks on buttons and dialogs to the page", () => {
      const { target, intents, controls } = setup();
      controls.attach();

      const event = target.mouse("mousedown", 0, { closest: () => ({}) });

      expect(intents).toEqual([]);
      expect(event.preventDefault).not.toHaveBeenCalled();
    });

    it("ignores mouse events that only echo a touch", () => {
      const { target, intents, controls } = setup();
      controls.attach();
      const echo = { button: 0, target: { closest: () => null }, sourceCapabilities: { firesTouchEvents: true }, preventDefault: () => {} };

      (target as any).listeners.get("mousedown")?.forEach((listener: (event: unknown) => void) => listener(echo));

      expect(intents).toEqual([]);
    });

    it("does nothing while disabled", () => {
      const { target, intents, controls } = setup(() => false);
      controls.attach();

      target.mouse("mousedown", 0);

      expect(intents).toEqual([]);
    });

    it("keeps the right click menu out of the way while playing", () => {
      const { target, controls } = setup();
      controls.attach();

      expect(target.contextMenu().preventDefault).toHaveBeenCalled();
    });

    it("stops listening once detached", () => {
      const { target, controls } = setup();
      controls.attach();
      controls.detach();

      expect(target.listenerCount("mousedown")).toBe(0);
      expect(target.listenerCount("contextmenu")).toBe(0);
    });
  });
});
