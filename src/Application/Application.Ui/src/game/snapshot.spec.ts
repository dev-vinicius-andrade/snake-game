import { describe, expect, it } from "vitest";
import fixture from "./__fixtures__/gameState.json";
import { parseLeaderboard, parseRoomSize, parseSnapshot } from "./snapshot";

const clone = <T>(value: T): T => JSON.parse(JSON.stringify(value));
const playerKey = "22222222-2222-2222-2222-222222222222";

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
      guid: playerKey,
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
      score: 7,
      color: { fillColor: "#ff0000", border: "#aa0000" },
      direction: "right",
    });
  });

  it("reads foods", () => {
    expect(parseSnapshot(fixture)!.foods).toEqual([
      {
        guid: "33333333-3333-3333-3333-333333333333",
        position: { x: 10, y: 20 },
        color: { fillColor: "#00ff00", border: "#00aa00" },
        value: 3,
      },
    ]);
  });

  it("reads shots with their owner", () => {
    expect(parseSnapshot(fixture)!.shots).toEqual([
      {
        guid: "44444444-4444-4444-4444-444444444444",
        position: { x: 30, y: 40 },
        size: 15,
        ownerGuid: playerKey,
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

  it("skips players that are not objects or have no position", () => {
    const raw = clone(fixture) as any;
    raw.players.broken = "nope";
    raw.players.empty = { name: "ghost" };

    expect(parseSnapshot(raw)!.snakes.map((snake) => snake.name)).toEqual(["ana"]);
  });

  it("derives the head from currentPosition when the path is empty", () => {
    const raw = clone(fixture) as any;
    raw.players[playerKey].path = [];

    const [snake] = parseSnapshot(raw)!.snakes;

    expect(snake.path).toEqual([{ x: 500, y: 500 }]);
    expect(snake.head).toEqual({ x: 500, y: 500 });
  });

  it("drops path points that are not finite numbers", () => {
    const raw = clone(fixture) as any;
    raw.players[playerKey].path = [{ x: 1, y: 2 }, { x: "a", y: 2 }, null, { x: 3, y: 4 }];
    raw.players[playerKey].segmentHealth = [1, 1];

    expect(parseSnapshot(raw)!.snakes[0].path).toEqual([
      { x: 1, y: 2 },
      { x: 3, y: 4 },
    ]);
  });

  it("defaults missing segment health to full", () => {
    const raw = clone(fixture) as any;
    delete raw.players[playerKey].segmentHealth;

    expect(parseSnapshot(raw)!.snakes[0].segmentHealth).toEqual([1, 1]);
  });

  it("pads short segment health at the tail side so it stays aligned with the head", () => {
    const raw = clone(fixture) as any;
    raw.players[playerKey].segmentHealth = [0.25];

    expect(parseSnapshot(raw)!.snakes[0].segmentHealth).toEqual([1, 0.25]);
  });

  it("trims long segment health from the tail side", () => {
    const raw = clone(fixture) as any;
    raw.players[playerKey].segmentHealth = [0.1, 0.5, 0.75];

    expect(parseSnapshot(raw)!.snakes[0].segmentHealth).toEqual([0.5, 0.75]);
  });

  it("clamps segment health to 0..1 and treats junk as full", () => {
    const raw = clone(fixture) as any;
    raw.players[playerKey].segmentHealth = [-3, "x"];

    expect(parseSnapshot(raw)!.snakes[0].segmentHealth).toEqual([0, 1]);
  });

  it("defaults score, flags, colour and direction", () => {
    const raw = clone(fixture) as any;
    const player = raw.players[playerKey];
    player.score = "many";
    delete player.isAlive;
    delete player.isBoosting;
    delete player.backgroundColor;
    player.currentDirection = "sideways";

    const [snake] = parseSnapshot(raw)!.snakes;

    expect(snake.score).toBe(0);
    expect(snake.isAlive).toBe(true);
    expect(snake.isBoosting).toBe(false);
    expect(snake.color).toEqual({ fillColor: "#ffffff", border: "#000000" });
    expect(snake.direction).toBe("right");
  });

  it("accepts the angular direction", () => {
    const raw = clone(fixture) as any;
    raw.players[playerKey].currentDirection = "angular";

    expect(parseSnapshot(raw)!.snakes[0].direction).toBe("angular");
  });

  it("returns empty lists when foods and obstacles are missing or malformed", () => {
    const raw = clone(fixture) as any;
    delete raw.foods;
    raw.obstacles = "nope";

    const snapshot = parseSnapshot(raw)!;

    expect(snapshot.foods).toEqual([]);
    expect(snapshot.shots).toEqual([]);
  });

  it("skips foods without a position and defaults their value to 1", () => {
    const raw = clone(fixture) as any;
    raw.foods.bad = { value: 9 };
    delete raw.foods["33333333-3333-3333-3333-333333333333"].value;

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
