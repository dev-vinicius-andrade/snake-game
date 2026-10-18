import { beforeEach, describe, expect, it } from "vitest";
import type { FoodSnapshot, GameSnapshot, ShotSnapshot, SnakeSnapshot } from "@/types/game";
import { drawFrame, logicalSize, type PixelContext } from "./pixelRenderer";
import { headColor, segmentColor } from "./segmentColor";

class RecordingContext implements PixelContext {
  fillStyle = "";
  calls: Array<{ style: string; x: number; y: number; w: number; h: number }> = [];
  fillRect(x: number, y: number, w: number, h: number) {
    this.calls.push({ style: this.fillStyle, x, y, w, h });
  }
}

const room = { guid: null, width: 1920, height: 1080 };

function snake(overrides: Partial<SnakeSnapshot> = {}): SnakeSnapshot {
  const path = overrides.path ?? [
    { x: 0, y: 0 },
    { x: 15, y: 0 },
    { x: 30, y: 0 },
  ];
  return {
    guid: "s1",
    connectionId: "c1",
    name: "ana",
    head: path[path.length - 1],
    path,
    segmentHealth: path.map(() => 1),
    isAlive: true,
    isBoosting: false,
    score: 0,
    color: { fillColor: "#00ff00", border: "#007700" },
    direction: "right",
    ...overrides,
  };
}

function snapshot(parts: Partial<GameSnapshot> = {}): GameSnapshot {
  return { room, snakes: [], foods: [], shots: [], ...parts };
}

const food = (value = 1, x = 100, y = 50): FoodSnapshot => ({
  guid: `f${x}${y}`,
  position: { x, y },
  color: { fillColor: "#ffaa00", border: "#aa7700" },
  value,
});
const shot = (x = 200, y = 100): ShotSnapshot => ({ guid: "sh", position: { x, y }, size: 15 });

describe("logicalSize", () => {
  it("divides the world by the pixel size, rounding up", () => {
    expect(logicalSize({ width: 1920, height: 1080 })).toEqual({ width: 384, height: 216 });
    expect(logicalSize({ width: 1921, height: 1081 })).toEqual({ width: 385, height: 217 });
  });

  it("honours a custom pixel size", () => {
    expect(logicalSize({ width: 100, height: 50 }, 10)).toEqual({ width: 10, height: 5 });
  });
});

describe("drawFrame", () => {
  let ctx: RecordingContext;
  beforeEach(() => {
    ctx = new RecordingContext();
  });

  it("clears the whole logical canvas first", () => {
    drawFrame(ctx, snapshot());

    expect(ctx.calls[0]).toEqual({ style: "#16161d", x: 0, y: 0, w: 384, h: 216 });
  });

  it("uses a custom background", () => {
    drawFrame(ctx, snapshot(), { backgroundColor: "#000000" });

    expect(ctx.calls[0].style).toBe("#000000");
  });

  it("draws every segment as a 3x3 pixel block snapped to the pixel grid", () => {
    drawFrame(ctx, snapshot({ snakes: [snake()] }));

    const blocks = ctx.calls.slice(1).map(({ x, y, w, h }) => ({ x, y, w, h }));
    expect(blocks).toEqual([
      { x: 0, y: 0, w: 3, h: 3 },
      { x: 3, y: 0, w: 3, h: 3 },
      { x: 6, y: 0, w: 3, h: 3 },
    ]);
  });

  it("snaps fractional world positions down to the grid", () => {
    drawFrame(ctx, snapshot({ snakes: [snake({ path: [{ x: 503.75, y: 99.9 }] })] }));

    expect(ctx.calls[1]).toMatchObject({ x: 100, y: 19 });
  });

  it("colours body segments by their health and the head in its own colour", () => {
    drawFrame(ctx, snapshot({ snakes: [snake({ segmentHealth: [0.2, 1, 1] })] }));

    const styles = ctx.calls.slice(1).map((call) => call.style);
    expect(styles).toEqual([segmentColor("#00ff00", 0.2), "#00ff00", headColor("#00ff00")]);
  });

  it("makes the head differ from the body even when the server sends the same fill and border", () => {
    drawFrame(ctx, snapshot({ snakes: [snake({ color: { fillColor: "#3366ff", border: "#3366ff" } })] }));

    const [body, , head] = ctx.calls.slice(1).map((call) => call.style);
    expect(head).not.toBe(body);
  });

  it("draws the head last so it stays on top", () => {
    drawFrame(ctx, snapshot({ snakes: [snake()] }));

    expect(ctx.calls[ctx.calls.length - 1]).toMatchObject({ x: 6, style: headColor("#00ff00") });
  });

  it("does not draw dead snakes", () => {
    drawFrame(ctx, snapshot({ snakes: [snake({ isAlive: false })] }));

    expect(ctx.calls).toHaveLength(1);
  });

  it("draws small food as 2x2 and big food as 3x3", () => {
    drawFrame(ctx, snapshot({ foods: [food(1, 100, 50), food(5, 200, 50)] }));

    expect(ctx.calls[1]).toEqual({ style: "#ffaa00", x: 20, y: 10, w: 2, h: 2 });
    expect(ctx.calls[2]).toEqual({ style: "#ffaa00", x: 40, y: 10, w: 3, h: 3 });
  });

  it("draws shots as white 2x2 blocks", () => {
    drawFrame(ctx, snapshot({ shots: [shot(200, 100)] }));

    expect(ctx.calls[1]).toEqual({ style: "#ffffff", x: 40, y: 20, w: 2, h: 2 });
  });

  it("draws foods, then shots, then snakes", () => {
    drawFrame(ctx, snapshot({ foods: [food()], shots: [shot()], snakes: [snake()] }));

    const styles = ctx.calls.slice(1).map((call) => call.style);
    expect(styles[0]).toBe("#ffaa00");
    expect(styles[1]).toBe("#ffffff");
    expect(styles[2]).toBe(segmentColor("#00ff00", 1));
  });

  it("draws the own snake after the others", () => {
    const mine = snake({ guid: "me", color: { fillColor: "#0000ff", border: "#000077" } });
    const other = snake({ guid: "other", path: [{ x: 600, y: 600 }] });

    drawFrame(ctx, snapshot({ snakes: [mine, other] }), { selfGuid: "me" });

    expect(ctx.calls[ctx.calls.length - 1].style).toBe(headColor("#0000ff"));
  });

  it("does not mutate the snapshot", () => {
    const frame = snapshot({ foods: [food()], snakes: [snake()] });
    const before = JSON.stringify(frame);

    drawFrame(ctx, frame);

    expect(JSON.stringify(frame)).toBe(before);
  });
});

describe("drawFrame with a camera", () => {
  let ctx: RecordingContext;
  beforeEach(() => {
    ctx = new RecordingContext();
  });

  const view = { width: 100, height: 60 };

  it("fills the whole view with the void colour first", () => {
    drawFrame(ctx, snapshot(), { view, origin: { x: 0, y: 0 } });

    expect(ctx.calls[0]).toMatchObject({ x: 0, y: 0, w: 100, h: 60 });
  });

  it("then fills the arena where it is in the view", () => {
    drawFrame(ctx, snapshot(), { view, origin: { x: 100, y: 50 } });

    // The world starts 100 / 5 = 20 pixels left of and 50 / 5 = 10 pixels above the view, and is 384 x 216 pixels.
    expect(ctx.calls[1]).toMatchObject({ x: -20, y: -10, w: 384, h: 216 });
  });

  it("draws the arena in another colour than the void, with a border around it", () => {
    drawFrame(ctx, snapshot(), { view, origin: { x: 0, y: 0 } });

    const styles = ctx.calls.slice(0, 6).map((call) => call.style);
    expect(styles[0]).not.toBe(styles[1]);
    expect(new Set(styles.slice(2, 6)).size).toBe(1);
    expect(styles[2]).not.toBe(styles[1]);
  });

  it("shifts everything by the camera origin", () => {
    drawFrame(ctx, snapshot({ foods: [food(1, 600, 350)] }), { view, origin: { x: 500, y: 300 } });

    const foodCall = ctx.calls[ctx.calls.length - 1];
    expect(foodCall).toMatchObject({ x: 20, y: 10 });
  });

  it("draws the head where the snake is relative to the camera", () => {
    drawFrame(ctx, snapshot({ snakes: [snake({ path: [{ x: 500, y: 300 }] })] }), { view, origin: { x: 450, y: 250 } });

    expect(ctx.calls[ctx.calls.length - 1]).toMatchObject({ x: 10, y: 10 });
  });

  it("keeps drawing things that are partly outside the view: the canvas clips them", () => {
    drawFrame(ctx, snapshot({ foods: [food(1, 0, 0)] }), { view, origin: { x: 50, y: 50 } });

    expect(ctx.calls[ctx.calls.length - 1]).toMatchObject({ x: -10, y: -10 });
  });

  it("without a view it still clears the whole room with one fill", () => {
    drawFrame(ctx, snapshot());

    expect(ctx.calls).toHaveLength(1);
    expect(ctx.calls[0]).toMatchObject({ x: 0, y: 0, w: 384, h: 216 });
  });
});
