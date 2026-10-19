import { beforeEach, describe, expect, it } from "vitest";
import type { FloatingDamage, FoodSnapshot, GameSnapshot, ShotSnapshot, SnakeSnapshot } from "@/types/game";
import { drawFrame, logicalSize, type PixelContext } from "./pixelRenderer";
import { contrastColor, headColor, segmentColor, shadeColor } from "./segmentColor";

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
    perks: [],
    autoFire: false,
    foodsEaten: 0,
    score: 0,
    color: { fillColor: "#00ff00", border: "#007700" },
    direction: "right",
    ...overrides,
  };
}

function snapshot(parts: Partial<GameSnapshot> = {}): GameSnapshot {
  return { room, snakes: [], foods: [], shots: [], strikes: [], ...parts };
}

const food = (value = 1, x = 100, y = 50): FoodSnapshot => ({
  guid: `f${x}${y}`,
  position: { x, y },
  color: { fillColor: "#ffaa00", border: "#aa7700" },
  value,
  kind: "normal",
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

    const blocks = ctx.calls.slice(1).filter((call) => call.w === 3).map(({ x, y, w, h }) => ({ x, y, w, h }));
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

    const styles = ctx.calls.slice(1).filter((call) => call.w === 3).map((call) => call.style);
    // Every other segment, counting from the head, is a little darker so the body shows its segments.
    expect(styles).toEqual([segmentColor("#00ff00", 0.2), shadeColor("#00ff00", 0.82), headColor("#00ff00")]);
  });

  it("makes the head differ from the body even when the server sends the same fill and border", () => {
    drawFrame(ctx, snapshot({ snakes: [snake({ color: { fillColor: "#3366ff", border: "#3366ff" } })] }));

    const [body, , head] = ctx.calls.slice(1).filter((call) => call.w === 3).map((call) => call.style);
    expect(head).not.toBe(body);
  });

  it("draws the head last so it stays on top", () => {
    drawFrame(ctx, snapshot({ snakes: [snake()] }));

    const blocks = ctx.calls.filter((call) => call.w === 3);
    expect(blocks[blocks.length - 1]).toMatchObject({ x: 6, style: headColor("#00ff00") });
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

  describe("prize", () => {
    const prize = (x = 100, y = 50): FoodSnapshot => ({ ...food(20, x, y), kind: "prize" });

    it("is a 5x5 gold block, bigger than any food", () => {
      drawFrame(ctx, snapshot({ foods: [prize(100, 50)] }), { timeMs: 0 });

      const block = ctx.calls.find((call) => call.w === 5 && call.h === 5);
      expect(block).toMatchObject({ x: 20, y: 10 });
    });

    it("has a bright heart in the middle", () => {
      drawFrame(ctx, snapshot({ foods: [prize(100, 50)] }), { timeMs: 0 });

      expect(ctx.calls.find((call) => call.x === 22 && call.y === 12 && call.w === 1)?.style).toBe("#ffffff");
    });

    it("is in the colour of its kind: a darker ring and a lighter glow", () => {
      const pink: FoodSnapshot = { ...prize(100, 50), color: { fillColor: "#ff00aa", border: "#ff00aa" } };

      drawFrame(ctx, snapshot({ foods: [pink] }), { timeMs: 0 });

      expect(ctx.calls.find((call) => call.w === 5 && call.h === 5)?.style).toBe(shadeColor("#ff00aa", 0.7));
      expect(ctx.calls.filter((call) => call.w === 1 && call.style === "#ff00aa").length).toBeGreaterThan(5);
    });

    it("glows: the gold changes with time", () => {
      const first = new RecordingContext();
      const later = new RecordingContext();

      drawFrame(first, snapshot({ foods: [prize()] }), { timeMs: 0 });
      drawFrame(later, snapshot({ foods: [prize()] }), { timeMs: 400 });

      expect(later.calls).not.toEqual(first.calls);
    });

    it("does not make ordinary food glow", () => {
      const first = new RecordingContext();
      const later = new RecordingContext();

      drawFrame(first, snapshot({ foods: [food(5)] }), { timeMs: 0 });
      drawFrame(later, snapshot({ foods: [food(5)] }), { timeMs: 400 });

      expect(later.calls).toEqual(first.calls);
    });
  });

  describe("damage numbers", () => {
    const damage = (overrides: Partial<FloatingDamage> = {}): FloatingDamage => ({
      shooterGuid: "s1",
      targetGuid: "other",
      x: 100,
      y: 50,
      damage: 23,
      kind: "hit",
      bornMs: 1000,
      ...overrides,
    });

    it("draws the number in the colour of who it is for, over everything else", () => {
      drawFrame(ctx, snapshot({ snakes: [snake()] }), { selfGuid: "s1", timeMs: 1100, damage: [damage()] });

      const gold = ctx.calls.filter((call) => call.style === "#ffd23f" && call.w === 1 && call.h === 1);
      expect(gold.length).toBeGreaterThan(8);
      expect(ctx.calls.indexOf(gold[0])).toBeGreaterThan(ctx.calls.findIndex((call) => call.w === 3));
    });

    it("moves up as time goes by", () => {
      const early = new RecordingContext();
      const late = new RecordingContext();

      drawFrame(early, snapshot(), { selfGuid: "s1", timeMs: 1000, damage: [damage()] });
      drawFrame(late, snapshot(), { selfGuid: "s1", timeMs: 1400, damage: [damage()] });

      const top = (context: RecordingContext) => Math.min(...context.calls.filter((call) => call.style === "#ffd23f").map((call) => call.y));
      expect(top(late)).toBeLessThan(top(early));
    });

    it("is gone once its time is up", () => {
      drawFrame(ctx, snapshot(), { selfGuid: "s1", timeMs: 5000, damage: [damage()] });

      expect(ctx.calls.filter((call) => call.style === "#ffd23f")).toHaveLength(0);
    });

    it("draws nothing without a clock, so a still frame stays still", () => {
      drawFrame(ctx, snapshot(), { selfGuid: "s1", damage: [damage()] });

      expect(ctx.calls.filter((call) => call.style === "#ffd23f")).toHaveLength(0);
    });
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

    const blocks = ctx.calls.filter((call) => call.w === 3);
    expect(blocks[blocks.length - 1].style).toBe(headColor("#0000ff"));
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
  // The room is 1920 x 1080 world units: 384 x 216 logical pixels, and it wraps around.
  const draw = (parts: Partial<GameSnapshot>, origin: { x: number; y: number }) => drawFrame(ctx, snapshot(parts), { view, origin });
  /** Everything except the background and the grid dots. */
  const sprites = () => ctx.calls.slice(1).filter((call) => !(call.w === 1 && call.h === 1));

  it("fills the whole view with the arena colour first, and shows no void and no walls", () => {
    draw({}, { x: 0, y: 0 });

    expect(ctx.calls[0]).toMatchObject({ x: 0, y: 0, w: 100, h: 60 });
    expect(new Set(ctx.calls.map((call) => call.style).slice(1)).size).toBe(1);
  });

  it("shifts everything by the camera origin", () => {
    draw({ foods: [food(1, 600, 350)] }, { x: 500, y: 300 });

    expect(sprites()[0]).toMatchObject({ x: 20, y: 10 });
  });

  it("draws the head where the snake is relative to the camera", () => {
    draw({ snakes: [snake({ path: [{ x: 500, y: 300 }] })] }, { x: 450, y: 250 });

    expect(sprites()[0]).toMatchObject({ x: 10, y: 10 });
  });

  it("shows the far side of the world in front of an edge: past the right wall comes the left wall", () => {
    // The view covers x = 1700 .. 2200; 2200 - 1920 = 280 is where the left side of the world continues.
    draw({ foods: [food(1, 100, 0)] }, { x: 1700, y: 0 });

    expect(sprites()[0]).toMatchObject({ x: 64, y: 0 });
  });

  it("does the same past the left wall and the top and bottom walls", () => {
    draw({ foods: [food(1, 1900, 1070)] }, { x: -50, y: -50 });

    expect(sprites()[0]).toMatchObject({ x: 6, y: 8 });
  });

  it("draws a sprite across the seam on both sides of it", () => {
    // Logical x 383 is the last column of the world; a 3 pixel block there reaches 2 pixels into the next copy.
    draw({ snakes: [snake({ path: [{ x: 1915, y: 0 }] })] }, { x: 0, y: 0 });

    expect(sprites().map((call) => call.x)).toEqual([-1]);
  });

  it("does not draw what is far outside the view", () => {
    draw({ foods: [food(1, 1000, 500)] }, { x: 0, y: 0 });

    expect(sprites()).toHaveLength(0);
  });

  it("keeps the own snake in the same place on screen when it wraps to the other side of the world", () => {
    const before = snake({ path: [{ x: 1905, y: 500 }] });
    const after = snake({ path: [{ x: 0, y: 500 }] });

    drawFrame(ctx, snapshot({ snakes: [before] }), { view, origin: { x: 1905 + 7.5 - 250, y: 500 + 7.5 - 150 } });
    const first = sprites()[0];
    ctx.calls.length = 0;
    drawFrame(ctx, snapshot({ snakes: [after] }), { view, origin: { x: 0 + 7.5 - 250, y: 500 + 7.5 - 150 } });

    expect(sprites()[0]).toMatchObject({ x: first.x, y: first.y });
  });

  it("draws a faint grid fixed to the world, so moving through the arena is visible", () => {
    draw({}, { x: 0, y: 0 });

    const dots = ctx.calls.filter((call) => call.w === 1 && call.h === 1);
    expect(dots).toContainEqual(expect.objectContaining({ x: 24, y: 24 }));
    expect(dots).toContainEqual(expect.objectContaining({ x: 48, y: 24 }));
  });

  it("moves the grid with the camera", () => {
    draw({}, { x: 60, y: 0 });

    const dots = ctx.calls.filter((call) => call.w === 1 && call.h === 1);
    expect(dots).toContainEqual(expect.objectContaining({ x: 12, y: 24 }));
  });

  it("without a view it still clears the whole room with one fill and does not wrap", () => {
    drawFrame(ctx, snapshot({ foods: [food(1, 1900, 1070)] }));

    expect(ctx.calls).toHaveLength(2);
    expect(ctx.calls[0]).toMatchObject({ x: 0, y: 0, w: 384, h: 216 });
  });
});

describe("how a snake looks", () => {
  let ctx: RecordingContext;
  beforeEach(() => {
    ctx = new RecordingContext();
  });

  const eyes = () => ctx.calls.filter((call) => call.w === 1 && call.h === 1);
  const along = (head: { x: number; y: number }, step: { x: number; y: number }) =>
    snake({ path: [{ x: head.x - 2 * step.x, y: head.y - 2 * step.y }, { x: head.x - step.x, y: head.y - step.y }, head] });

  it("alternates the shade of the body segments, counting from the head, so a straight snake does not look like a bar", () => {
    drawFrame(ctx, snapshot({ snakes: [snake({ path: [0, 15, 30, 45, 60].map((x) => ({ x, y: 0 })), segmentHealth: [1, 1, 1, 1, 1] })] }));

    const styles = ctx.calls.slice(1).filter((call) => call.w === 3).map((call) => call.style);
    expect(styles[3]).toBe(shadeColor("#00ff00", 0.82));
    expect(styles[2]).toBe("#00ff00");
    expect(styles[1]).toBe(shadeColor("#00ff00", 0.82));
    expect(styles[0]).toBe("#00ff00");
  });

  it("puts two eyes on the front of the head when it moves right", () => {
    drawFrame(ctx, snapshot({ snakes: [along({ x: 100, y: 100 }, { x: 15, y: 0 })] }));

    expect(eyes().map(({ x, y }) => ({ x, y }))).toEqual([
      { x: 22, y: 20 },
      { x: 22, y: 22 },
    ]);
  });

  it("puts them on the left when it moves left", () => {
    drawFrame(ctx, snapshot({ snakes: [along({ x: 100, y: 100 }, { x: -15, y: 0 })] }));

    expect(eyes().map(({ x, y }) => ({ x, y }))).toEqual([
      { x: 20, y: 20 },
      { x: 20, y: 22 },
    ]);
  });

  it("puts them at the top when it moves up and at the bottom when it moves down", () => {
    drawFrame(ctx, snapshot({ snakes: [along({ x: 100, y: 100 }, { x: 0, y: -15 })] }));
    expect(eyes().map(({ x, y }) => ({ x, y }))).toEqual([
      { x: 20, y: 20 },
      { x: 22, y: 20 },
    ]);

    ctx.calls.length = 0;
    drawFrame(ctx, snapshot({ snakes: [along({ x: 100, y: 100 }, { x: 0, y: 15 })] }));
    expect(eyes().map(({ x, y }) => ({ x, y }))).toEqual([
      { x: 20, y: 22 },
      { x: 22, y: 22 },
    ]);
  });

  it("draws the eyes on top of the head, in a colour that shows against it", () => {
    drawFrame(ctx, snapshot({ snakes: [along({ x: 100, y: 100 }, { x: 15, y: 0 })] }));

    const calls = ctx.calls;
    const head = calls.findIndex((call) => call.w === 3 && call.x === 20);
    expect(calls.slice(head + 1).every((call) => call.w === 1)).toBe(true);
    expect(calls[calls.length - 1].style).toBe(contrastColor(headColor("#00ff00")));
  });

  it("knows which way the snake faces when it has just crossed the edge of the world", () => {
    // The head wrapped from the right edge to the left: it still moves right.
    drawFrame(ctx, snapshot({ snakes: [snake({ path: [{ x: 1890, y: 100 }, { x: 1905, y: 100 }, { x: 0, y: 100 }] })] }));

    expect(eyes()[0].x).toBe(2);
  });

  it("draws no eyes on a snake that has nothing to tell its direction by", () => {
    drawFrame(ctx, snapshot({ snakes: [snake({ path: [{ x: 100, y: 100 }] })] }));

    expect(eyes()).toHaveLength(0);
  });
});

describe("positions that sit exactly on a pixel boundary", () => {
  const view = { width: 100, height: 60 };
  const body = snake({ path: [{ x: 600, y: 300 }, { x: 615, y: 300 }, { x: 630, y: 300 }] });

  function draw(originX: number) {
    const ctx = new RecordingContext();
    drawFrame(ctx, snapshot({ snakes: [body], foods: [food(1, 700, 350)] }), { view, origin: { x: originX, y: 250 } });
    return ctx.calls.map(({ x, y, w, h }) => ({ x, y, w, h }));
  }

  it("are drawn in the same place however tiny the rounding error of the camera is", () => {
    const exact = draw(500);

    expect(draw(500 + 1e-10)).toEqual(exact);
    expect(draw(500 - 1e-10)).toEqual(exact);
    expect(draw(500 + 1e-13)).toEqual(exact);
    expect(draw(500 - 1e-13)).toEqual(exact);
  });

  it("are drawn at the pixel they are on", () => {
    const calls = draw(500);

    // (600 - 500) / 5 = 20, exactly.
    expect(calls.find((call) => call.w === 3)).toMatchObject({ x: 20, y: 10 });
  });
});

