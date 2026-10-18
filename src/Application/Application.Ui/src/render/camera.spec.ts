import { describe, expect, it } from "vitest";
import { cameraOrigin, followCamera, toViewPoint, viewLayout } from "./camera";

describe("viewLayout", () => {
  it("scales pixel art so the short side shows about 120 logical pixels, in whole numbers", () => {
    const layout = viewLayout(1920, 1080)!;

    expect(layout.scale).toBe(9);
    expect(layout.logicalWidth).toBe(214);
    expect(layout.logicalHeight).toBe(120);
  });

  it("shows how much of the world is visible, in world units", () => {
    const layout = viewLayout(1920, 1080)!;

    expect(layout.worldWidth).toBe(214 * 5);
    expect(layout.worldHeight).toBe(120 * 5);
  });

  it("works for a tall phone screen", () => {
    const layout = viewLayout(375, 812)!;

    expect(layout.scale).toBe(3);
    expect(layout.logicalWidth).toBe(125);
    expect(layout.logicalHeight).toBe(271);
  });

  it("never goes below the minimum scale, so a tiny window still shows chunky pixels", () => {
    expect(viewLayout(100, 100)!.scale).toBe(2);
  });

  it("covers the whole screen: the logical size rounds up", () => {
    const layout = viewLayout(1001, 703)!;

    expect(layout.logicalWidth * layout.scale).toBeGreaterThanOrEqual(1001);
    expect(layout.logicalHeight * layout.scale).toBeGreaterThanOrEqual(703);
  });

  describe("with a world to stay inside", () => {
    const world = { width: 1920, height: 1080 };

    it("zooms in on a tall screen so the view is never taller than the world", () => {
      const layout = viewLayout(375, 812, { world })!;

      expect(layout.scale).toBe(4);
      expect(layout.worldHeight).toBeLessThanOrEqual(world.height);
    });

    it("zooms in on a wide screen so the view is never wider than the world", () => {
      const layout = viewLayout(3000, 700, { world })!;

      expect(layout.worldWidth).toBeLessThanOrEqual(world.width);
    });

    it("leaves a screen that already fits alone", () => {
      expect(viewLayout(1920, 1080, { world })!.scale).toBe(9);
    });

    it("works with a small world", () => {
      const layout = viewLayout(800, 600, { world: { width: 400, height: 300 } })!;

      expect(layout.worldWidth).toBeLessThanOrEqual(400);
      expect(layout.worldHeight).toBeLessThanOrEqual(300);
    });
  });

  it("honours another pixel size", () => {
    expect(viewLayout(1920, 1080, { pixelSize: 10 })!.worldHeight).toBe(1200);
  });

  it("honours another target for the short side", () => {
    expect(viewLayout(1920, 1080, { targetShortSide: 240 })!.scale).toBe(5);
  });

  it.each([
    [0, 600],
    [800, 0],
    [-5, 600],
    [Number.NaN, 600],
  ])("returns null for an unusable size %f x %f", (width, height) => {
    expect(viewLayout(width, height)).toBeNull();
  });
});

describe("cameraOrigin", () => {
  const world = { width: 1920, height: 1080 };
  const view = { width: 1070, height: 600 };

  it("puts the target in the middle of the view", () => {
    expect(cameraOrigin({ x: 960, y: 540 }, view, world)).toEqual({ x: 425, y: 240 });
  });

  it("stops at the left and top edge of the world", () => {
    expect(cameraOrigin({ x: 10, y: 5 }, view, world)).toEqual({ x: 0, y: 0 });
  });

  it("stops at the right and bottom edge of the world", () => {
    expect(cameraOrigin({ x: 1915, y: 1075 }, view, world)).toEqual({ x: 850, y: 480 });
  });

  it("handles each axis on its own", () => {
    expect(cameraOrigin({ x: 10, y: 540 }, view, world)).toEqual({ x: 0, y: 240 });
  });

  it("centres the world on an axis where the view is larger than the world", () => {
    const tall = { width: 625, height: 1355 };

    expect(cameraOrigin({ x: 960, y: 100 }, tall, world)).toEqual({ x: 647.5, y: -137.5 });
  });

  it("centres the world when the view is exactly as large", () => {
    expect(cameraOrigin({ x: 5, y: 5 }, { width: 1920, height: 1080 }, world)).toEqual({ x: 0, y: 0 });
  });

  it("follows a target that moves", () => {
    const first = cameraOrigin({ x: 900, y: 500 }, view, world);
    const second = cameraOrigin({ x: 960, y: 500 }, view, world);

    expect(second.x - first.x).toBe(60);
  });
});

describe("toViewPoint", () => {
  it("measures a world point from the top-left of the view", () => {
    expect(toViewPoint({ x: 500, y: 300 }, { x: 425, y: 240 })).toEqual({ x: 75, y: 60 });
  });

  it("gives negative values for points left of or above the view", () => {
    expect(toViewPoint({ x: 100, y: 100 }, { x: 425, y: 240 })).toEqual({ x: -325, y: -140 });
  });
});

describe("followCamera", () => {
  const world = { width: 1920, height: 1080 };
  const layout = viewLayout(1920, 1080)!;

  it("gives the canvas size in logical pixels and the world size it shows", () => {
    const camera = followCamera(layout, world, { x: 960, y: 540 });

    expect(camera.view).toEqual({ width: 214, height: 120 });
    expect(camera.size).toEqual({ width: 1070, height: 600 });
  });

  it("centres on the target", () => {
    expect(followCamera(layout, world, { x: 960, y: 540 }).origin).toEqual({ x: 425, y: 240 });
  });

  it("measures points from the top-left of what is visible", () => {
    const camera = followCamera(layout, world, { x: 960, y: 540 });

    expect(toViewPoint({ x: 960, y: 540 }, camera.origin)).toEqual({ x: 535, y: 300 });
  });
});
