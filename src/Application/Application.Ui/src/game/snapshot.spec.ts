import { describe, expect, it } from "vitest";
import fixture from "./__fixtures__/gameState.json";
import { parseLeaderboard, parseRoomSize, parseSnapshot } from "./snapshot";

const clone = <T>(value: T): T => JSON.parse(JSON.stringify(value));
const playerGuid = "22222222-2222-2222-2222-222222222222";

describe("parseSnapshot", () => {
  it("reads the room", () => {
    expect(parseSnapshot(fixture)?.room).toEqual({
      guid: "11111111-1111-1111-1111-111111111111",
      width: 1920,
      height: 1080,
    });
  });

  it("reads a snake the way the client draws it", () => {
    const [snake] = parseSnapshot(fixture)!.snakes;

    expect(snake).toEqual({
      guid: playerGuid,
      connectionId: "connection-1",
      name: "ana",
      head: { x: 500, y: 500 },
      path: [
        { x: 485, y: 500 },
        { x: 500, y: 500 },
      ],
      segmentHealth: [0.5, 1],
      isAlive: true,
      isBoosting: true,
      perks: [],
      autoFire: false,
      foodsEaten: 0,
      score: 7,
      color: { fillColor: "#ff0000", border: "#ff0000" },
      direction: "right",
    });
  });

  it("reads foods", () => {
    expect(parseSnapshot(fixture)!.foods).toEqual([
      {
        guid: "10,20",
        position: { x: 10, y: 20 },
        color: { fillColor: "#00ff00", border: "#00ff00" },
        value: 3,
        kind: "normal",
      },
    ]);
  });

  it("reads lightning warnings as a spot, a radius and the time left", () => {
    const raw = clone(fixture) as any;
    raw.strikes = [{ x: 100, y: 200, r: 90, t: 1200 }];

    expect(parseSnapshot(raw)!.strikes).toEqual([{ guid: "100,200", position: { x: 100, y: 200 }, radius: 90, msLeft: 1200 }]);
  });

  it("has no lightning warnings when the server sends none", () => {
    expect(parseSnapshot(fixture)!.strikes).toEqual([]);
  });

  it("drops lightning warnings that are not usable", () => {
    const raw = clone(fixture) as any;
    raw.strikes = ["nope", { x: 1 }, { x: 1, y: 2, r: 0, t: 5 }, { x: 3, y: 4, r: 50, t: -10 }];

    expect(parseSnapshot(raw)!.strikes).toEqual([{ guid: "3,4", position: { x: 3, y: 4 }, radius: 50, msLeft: 0 }]);
  });

  it("reads the prize kind of a food", () => {
    const raw = clone(fixture) as any;
    raw.foods[0].k = 1;

    expect(parseSnapshot(raw)!.foods[0].kind).toBe("prize");
  });

  it("gives foods that are alike the same identity only when they are at the same place", () => {
    const raw = clone(fixture) as any;
    raw.foods.push({ x: 11, y: 20, c: "#00ff00" });

    expect(new Set(parseSnapshot(raw)!.foods.map((food) => food.guid)).size).toBe(2);
  });

  it("reads the perks, foods eaten and auto fire of a snake", () => {
    const raw = clone(fixture) as any;
    raw.snakes[0].perks = ["swift", 5, "twin-shot"];
    raw.snakes[0].foodsEaten = 12;
    raw.snakes[0].autoFire = true;

    const snake = parseSnapshot(raw)!.snakes[0];

    expect(snake.perks).toEqual(["swift", "twin-shot"]);
    expect(snake.foodsEaten).toBe(12);
    expect(snake.autoFire).toBe(true);
  });

  it("reads shots with their owner", () => {
    expect(parseSnapshot(fixture)!.shots).toEqual([
      {
        guid: "44444444-4444-4444-4444-444444444444",
        position: { x: 30, y: 40 },
        size: 15,
        ownerGuid: playerGuid,
      },
    ]);
  });

  it.each([null, undefined, 5, "text", []])("returns null for %j", (raw) => {
    expect(parseSnapshot(raw)).toBeNull();
  });

  it("returns null without a usable room", () => {
    const raw = clone(fixture) as any;
    delete raw.room;

    expect(parseSnapshot(raw)).toBeNull();
  });

  it("returns null for a room without a positive size", () => {
    const raw = clone(fixture) as any;
    raw.room.width = 0;

    expect(parseSnapshot(raw)).toBeNull();
  });

  it("skips snakes that are not objects or have no body", () => {
    const raw = clone(fixture) as any;
    raw.snakes.push("nope", { name: "ghost" }, { name: "empty", path: [] });

    expect(parseSnapshot(raw)!.snakes.map((snake) => snake.name)).toEqual(["ana"]);
  });

  it("reads the path as pairs and ignores a stray last number", () => {
    const raw = clone(fixture) as any;
    raw.snakes[0].path = [1, 2, 3, 4, 5];
    raw.snakes[0].health = undefined;

    expect(parseSnapshot(raw)!.snakes[0].path).toEqual([
      { x: 1, y: 2 },
      { x: 3, y: 4 },
    ]);
  });

  it("drops path points that are not finite numbers", () => {
    const raw = clone(fixture) as any;
    raw.snakes[0].path = [1, 2, "a", 2, 3, 4];
    raw.snakes[0].health = undefined;

    expect(parseSnapshot(raw)!.snakes[0].path).toEqual([
      { x: 1, y: 2 },
      { x: 3, y: 4 },
    ]);
  });

  it("takes a snake without health as unhurt", () => {
    const raw = clone(fixture) as any;
    delete raw.snakes[0].health;

    expect(parseSnapshot(raw)!.snakes[0].segmentHealth).toEqual([1, 1]);
  });

  it("pads short health at the tail side so it stays aligned with the head", () => {
    const raw = clone(fixture) as any;
    raw.snakes[0].health = [25];

    expect(parseSnapshot(raw)!.snakes[0].segmentHealth).toEqual([1, 0.25]);
  });

  it("trims long health from the tail side", () => {
    const raw = clone(fixture) as any;
    raw.snakes[0].health = [10, 50, 75];

    expect(parseSnapshot(raw)!.snakes[0].segmentHealth).toEqual([0.5, 0.75]);
  });

  it("clamps health to 0..100 percent and treats junk as full", () => {
    const raw = clone(fixture) as any;
    raw.snakes[0].health = [-30, "x"];

    expect(parseSnapshot(raw)!.snakes[0].segmentHealth).toEqual([0, 1]);

    raw.snakes[0].health = [250, 100];
    expect(parseSnapshot(raw)!.snakes[0].segmentHealth).toEqual([1, 1]);
  });

  it("takes what is left out as plain: alive, not boosting, no perks", () => {
    const raw = clone(fixture) as any;
    delete raw.snakes[0].boost;

    const [snake] = parseSnapshot(raw)!.snakes;

    expect(snake.isAlive).toBe(true);
    expect(snake.isBoosting).toBe(false);
    expect(snake.perks).toEqual([]);
    expect(snake.autoFire).toBe(false);
  });

  it("knows a snake that died", () => {
    const raw = clone(fixture) as any;
    raw.snakes[0].dead = true;

    expect(parseSnapshot(raw)!.snakes[0].isAlive).toBe(false);
  });

  it("defaults score, colour and direction", () => {
    const raw = clone(fixture) as any;
    raw.snakes[0].score = "many";
    delete raw.snakes[0].color;
    raw.snakes[0].direction = "sideways";

    const [snake] = parseSnapshot(raw)!.snakes;

    expect(snake.score).toBe(0);
    expect(snake.color).toEqual({ fillColor: "#ffffff", border: "#000000" });
    expect(snake.direction).toBe("right");
  });

  it("accepts the angular direction", () => {
    const raw = clone(fixture) as any;
    raw.snakes[0].direction = "angular";

    expect(parseSnapshot(raw)!.snakes[0].direction).toBe("angular");
  });

  it("returns empty lists when foods and shots are missing or malformed", () => {
    const raw = clone(fixture) as any;
    delete raw.foods;
    raw.shots = "nope";

    const snapshot = parseSnapshot(raw)!;

    expect(snapshot.foods).toEqual([]);
    expect(snapshot.shots).toEqual([]);
  });

  it("skips foods without a position and defaults their value to 1", () => {
    const raw = clone(fixture) as any;
    raw.foods.push({ v: 9 }, "x");
    delete raw.foods[0].v;

    const foods = parseSnapshot(raw)!.foods;

    expect(foods).toHaveLength(1);
    expect(foods[0].value).toBe(1);
  });

  it("does not mutate its input", () => {
    const raw = clone(fixture);
    const before = JSON.stringify(raw);

    parseSnapshot(raw);

    expect(JSON.stringify(raw)).toBe(before);
  });
});

describe("parseRoomSize", () => {
  it("reads width and height from RoomDetailsUpdated", () => {
    expect(parseRoomSize({ width: 1920, height: 1080 })).toEqual({ width: 1920, height: 1080 });
  });

  it.each([null, {}, { width: 0, height: 10 }, { width: "a", height: 1 }])("returns null for %j", (raw) => {
    expect(parseRoomSize(raw)).toBeNull();
  });
});

describe("parseLeaderboard", () => {
  it("keeps the server order", () => {
    const raw = [
      { nickname: "ana", currentScore: 9 },
      { nickname: "bob", currentScore: 4 },
    ];

    expect(parseLeaderboard(raw)).toEqual(raw);
  });

  it("skips malformed entries", () => {
    expect(parseLeaderboard([{ nickname: "ana", currentScore: 9 }, null, { nickname: 3 }, "x"])).toEqual([
      { nickname: "ana", currentScore: 9 },
    ]);
  });

  it("returns an empty list for non arrays", () => {
    expect(parseLeaderboard({ nickname: "ana" })).toEqual([]);
  });
});
