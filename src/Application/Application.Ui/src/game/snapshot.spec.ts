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
      perkTiers: [],
      autoFire: false,
      foodsEaten: 0,
      score: 7,
      color: { fillColor: "#ff0000", border: "#ff0000" },
      direction: "right",
      lives: 0,
      protectedMs: 0,
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

    expect(parseSnapshot(raw)!.strikes).toEqual([{ guid: "100,200", kind: "lightning", position: { x: 100, y: 200 }, radius: 90, msLeft: 1200 }]);
  });

  it("tells a meteor from lightning by the kind of the warning", () => {
    const raw = clone(fixture) as any;
    raw.strikes = [{ x: 1, y: 2, r: 130, t: 2000, k: 1 }, { x: 3, y: 4, r: 90, t: 500, k: 0 }, { x: 5, y: 6, r: 90, t: 500 }];

    expect(parseSnapshot(raw)!.strikes.map((strike) => strike.kind)).toEqual(["meteor", "lightning", "lightning"]);
  });

  it("has no lightning warnings when the server sends none", () => {
    expect(parseSnapshot(fixture)!.strikes).toEqual([]);
  });

  it("drops lightning warnings that are not usable", () => {
    const raw = clone(fixture) as any;
    raw.strikes = ["nope", { x: 1 }, { x: 1, y: 2, r: 0, t: 5 }, { x: 3, y: 4, r: 50, t: -10 }];

    expect(parseSnapshot(raw)!.strikes).toEqual([{ guid: "3,4", kind: "lightning", position: { x: 3, y: 4 }, radius: 50, msLeft: 0 }]);
  });

  it("reads the tier of each perk, one for each perk, the first when it says nothing", () => {
    const raw = clone(fixture) as any;
    raw.snakes[0].perks = ["magnet", "swift", "armor"];
    expect(parseSnapshot(raw)!.snakes[0].perkTiers).toEqual([1, 1, 1]);

    raw.snakes[0].pt = [3, 2];
    expect(parseSnapshot(raw)!.snakes[0].perkTiers).toEqual([3, 2, 1]);

    raw.snakes[0].pt = [2, "x", 0, 9, 4];
    expect(parseSnapshot(raw)!.snakes[0].perkTiers).toEqual([2, 1, 1]);
  });

  it("has no zone when the server sends none", () => {
    expect(parseSnapshot(fixture)!.zone).toBeNull();
  });

  it("reads the safe zone: where, how far it reaches now and at the end, the phase and the time left in it", () => {
    const raw = clone(fixture) as any;
    raw.zone = { x: 300, y: 400, r: 812, f: 160, p: 1, t: 4321 };

    expect(parseSnapshot(raw)!.zone).toEqual({
      position: { x: 300, y: 400 },
      radius: 812,
      finalRadius: 160,
      phase: "closing",
      msLeft: 4321,
    });
  });

  it("names the phases of the zone", () => {
    const raw = clone(fixture) as any;
    const phases = [0, 1, 2, 7, undefined].map((p) => {
      raw.zone = { x: 1, y: 2, r: 100, f: 50, p, t: 10 };
      return parseSnapshot(raw)!.zone!.phase;
    });

    expect(phases).toEqual(["warning", "closing", "holding", "warning", "warning"]);
  });

  it("ignores a zone that is not usable", () => {
    const raw = clone(fixture) as any;
    for (const zone of ["nope", { x: 1 }, { x: 1, y: 2, r: 0, f: 50, p: 1, t: 1 }, { x: "1", y: 2, r: 5, f: 50, p: 1, t: 1 }]) {
      raw.zone = zone;
      expect(parseSnapshot(raw)!.zone).toBeNull();
    }
  });

  it("falls back to the radius it has when it does not say where it ends, and to no time left", () => {
    const raw = clone(fixture) as any;
    raw.zone = { x: 1, y: 2, r: 100, p: 2 };

    expect(parseSnapshot(raw)!.zone).toMatchObject({ finalRadius: 100, msLeft: 0 });
  });

  it("has no boss when the server sends none", () => {
    expect(parseSnapshot(fixture)!.boss).toBeNull();
  });

  it("reads the boss: where it stands, how big, how much is left and where its arms point", () => {
    const raw = clone(fixture) as any;
    raw.boss = { guid: "b1", x: 800, y: 400, s: 60, hp: 38, a: 90, n: 6 };

    expect(parseSnapshot(raw)!.boss).toEqual({
      guid: "b1",
      position: { x: 800, y: 400 },
      size: 60,
      hpRatio: 0.38,
      angle: Math.PI / 2,
      arms: 6,
    });
  });

  it("keeps a boss's hit points between nothing and everything", () => {
    const raw = clone(fixture) as any;
    raw.boss = { guid: "b1", x: 1, y: 2, s: 60, hp: 250, a: 0, n: 1 };
    expect(parseSnapshot(raw)!.boss!.hpRatio).toBe(1);
    raw.boss.hp = -5;
    expect(parseSnapshot(raw)!.boss!.hpRatio).toBe(0);
  });

  it("ignores a boss that is not usable", () => {
    const raw = clone(fixture) as any;
    for (const boss of ["nope", { x: 1 }, { guid: "b", x: 1, y: 2, s: 0, hp: 50, a: 0, n: 1 }, { guid: "b", x: "1", y: 2, s: 60, hp: 50, a: 0, n: 1 }]) {
      raw.boss = boss;
      expect(parseSnapshot(raw)!.boss).toBeNull();
    }
  });

  it("gives a boss at least one arm", () => {
    const raw = clone(fixture) as any;
    raw.boss = { guid: "b1", x: 1, y: 2, s: 60, hp: 50, a: 0 };

    expect(parseSnapshot(raw)!.boss!.arms).toBe(1);
  });

  it("has no mines, no bounty, no shower and no rush when the server sends none", () => {
    const snapshot = parseSnapshot(fixture)!;

    expect(snapshot.mines).toEqual([]);
    expect(snapshot.bounty).toBeNull();
    expect(snapshot.showerMsLeft).toBe(0);
    expect(snapshot.rushMsLeft).toBe(0);
  });

  it("reads mines as a spot, a blast radius and the time until each is armed", () => {
    const raw = clone(fixture) as any;
    raw.mines = [{ x: 100, y: 200, r: 60, t: 1500 }, { x: 300, y: 400, r: 60, t: 0 }];

    expect(parseSnapshot(raw)!.mines).toEqual([
      { guid: "100,200", position: { x: 100, y: 200 }, radius: 60, msToArm: 1500 },
      { guid: "300,400", position: { x: 300, y: 400 }, radius: 60, msToArm: 0 },
    ]);
  });

  it("drops mines that are not usable, and takes one without a time as armed", () => {
    const raw = clone(fixture) as any;
    raw.mines = ["nope", { x: 1 }, { x: 1, y: 2, r: 0, t: 5 }, { x: 3, y: 4, r: 50, t: -10 }, { x: 5, y: 6, r: 40 }];

    expect(parseSnapshot(raw)!.mines!.map((mine) => [mine.guid, mine.msToArm])).toEqual([
      ["3,4", 0],
      ["5,6", 0],
    ]);
  });

  it("reads the bounty: the wanted snake, the time left and the reward", () => {
    const raw = clone(fixture) as any;
    raw.bounty = { guid: playerGuid, t: 45000, m: 150 };

    expect(parseSnapshot(raw)!.bounty).toEqual({ guid: playerGuid, msLeft: 45000, rewardMass: 150 });
  });

  it("ignores a bounty without a wanted snake, and fills in what else it lacks", () => {
    const raw = clone(fixture) as any;
    for (const bounty of ["nope", { t: 100, m: 5 }, { guid: "", t: 100, m: 5 }, { guid: 7, t: 100, m: 5 }]) {
      raw.bounty = bounty;
      expect(parseSnapshot(raw)!.bounty).toBeNull();
    }

    raw.bounty = { guid: "g", t: -5, m: "lots" };
    expect(parseSnapshot(raw)!.bounty).toEqual({ guid: "g", msLeft: 0, rewardMass: 0 });
  });

  it("has no maze when the server sends none", () => {
    expect(parseSnapshot(fixture)!.maze).toBeNull();
  });

  it("reads the maze: the corner, the cells, the corridor, the twistiness, the seed, the block and the time left", () => {
    const raw = clone(fixture) as any;
    raw.maze = { x: 300, y: 200, c: 12, r: 8, k: 4, v: 60, s: 4000000000, u: 15, t: 90500 };

    expect(parseSnapshot(raw)!.maze).toEqual({
      position: { x: 300, y: 200 },
      columns: 12,
      rows: 8,
      corridor: 4,
      twistiness: 60,
      seed: 4000000000,
      unit: 15,
      msLeft: 90500,
    });
  });

  it("drops a maze the generator could not build, and a missing time is none left", () => {
    const raw = clone(fixture) as any;
    const good = { x: 300, y: 200, c: 12, r: 8, k: 4, v: 60, s: 7, u: 15, t: 100 };
    for (const bad of ["nope", { ...good, c: 2 }, { ...good, r: 31 }, { ...good, k: 2 }, { ...good, v: 101 }, { ...good, s: -1 }, { ...good, u: 0 }, { ...good, x: "a" }, { ...good, c: 4.5 }]) {
      raw.maze = bad;
      expect(parseSnapshot(raw)!.maze).toBeNull();
    }

    raw.maze = { ...good, t: undefined };
    expect(parseSnapshot(raw)!.maze!.msLeft).toBe(0);
  });

  it("reads the time left of a fruit shower and of a sugar rush, both at once", () => {
    const raw = clone(fixture) as any;
    raw.shower = 12000;
    raw.rush = 3000;

    const snapshot = parseSnapshot(raw)!;

    expect(snapshot.showerMsLeft).toBe(12000);
    expect(snapshot.rushMsLeft).toBe(3000);
  });

  it("takes a shower or a rush that is not a positive number as none", () => {
    const raw = clone(fixture) as any;
    for (const value of [-5, "soon", null, {}]) {
      raw.shower = value;
      raw.rush = value;
      expect(parseSnapshot(raw)!.showerMsLeft).toBe(0);
      expect(parseSnapshot(raw)!.rushMsLeft).toBe(0);
    }
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

  it("reads the lives, the protection and the cooldown perks of a snake", () => {
    const raw = clone(fixture) as any;
    raw.snakes[0].lives = 2;
    raw.snakes[0].prot = 1500;
    raw.snakes[0].ab = [{ id: "shield", r: 12000, a: 2000 }, { id: "dash" }, { r: 5 }, { id: "bad", r: -3, a: "x" }];

    const snake = parseSnapshot(raw)!.snakes[0];

    expect(snake.lives).toBe(2);
    expect(snake.protectedMs).toBe(1500);
    expect(snake.abilities).toEqual([
      { id: "shield", readyMs: 12000, activeMs: 2000 },
      { id: "dash", readyMs: 0, activeMs: 0 },
      { id: "bad", readyMs: 0, activeMs: 0 },
    ]);
  });

  it("reads a snake without lives, protection or cooldown perks as having none", () => {
    const snake = parseSnapshot(clone(fixture))!.snakes[0];

    expect(snake.lives).toBe(0);
    expect(snake.protectedMs).toBe(0);
    expect(snake.abilities).toBeUndefined();
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
