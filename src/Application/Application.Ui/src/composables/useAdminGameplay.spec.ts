import { beforeEach, describe, expect, it, vi } from "vitest";
import { ADMIN_KEY_STORAGE_KEY } from "./useAdminRooms";
import { EMPTY_REWARD, hasAbility, rewardOnlyOutOfPlace, useAdminGameplay, type GameplayConfig, type GameplayHttp, type PrizeDefinition } from "./useAdminGameplay";

const config = (overrides: Partial<PrizeDefinition> = {}): GameplayConfig => ({
  perks: {
    enabled: true,
    shotSpreadDegrees: 12,
    burstIntervalMilliseconds: 80,
    killFoodsCredit: 3,
    extraLifeKeepShare: 0.5,
    extraLifeProtectSeconds: 3,
    perks: [
      { id: "magnet", name: "Magnet", foodsRequired: 6, enabled: true, cooldownSeconds: 0, durationSeconds: 0, effects: [{ effect: "Magnet", amount: 60 }], tiers: [], rooms: [], excludedRooms: [] },
      { id: "twin", name: "Twin shot", foodsRequired: 15, enabled: true, cooldownSeconds: 0, durationSeconds: 0, effects: [{ effect: "ExtraShots", amount: 1 }], tiers: [], rooms: [], excludedRooms: [] },
    ],
  },
  prize: {
    enabled: true,
    prizes: [
      {
        id: "prize",
        name: "Prize",
        enabled: true,
        spawnIntervalSeconds: 60,
        lifetimeSeconds: 45,
        value: 20,
        foodsCredit: 8,
        minPlayers: 2,
        color: "#ffd23f",
        rooms: [],
        excludedRooms: [],
        reward: { ...EMPTY_REWARD },
        ...overrides,
      },
    ],
  },
  rooms: { emptyPrivateRoomMinutes: 10, maxPrivateRoomMinutes: 0 },
  worldEvents: {
    enabled: true,
    lightning: {
      enabled: true,
      minIntervalSeconds: 45,
      maxIntervalSeconds: 120,
      radius: 90,
      telegraphMilliseconds: 1500,
      minStrikes: 5,
      maxStrikes: 12,
      stormSeconds: 6,
      aimedShare: 0.25,
      rooms: [],
      excludedRooms: [],
    },
    meteors: {
      enabled: true,
      minIntervalSeconds: 120,
      maxIntervalSeconds: 300,
      radius: 130,
      telegraphMilliseconds: 2000,
      minMeteors: 6,
      maxMeteors: 14,
      rainSeconds: 8,
      aimedShare: 0.2,
      rooms: [],
      excludedRooms: [],
    },
    turret: {
      enabled: true,
      minIntervalSeconds: 120,
      maxIntervalSeconds: 300,
      lifetimeSeconds: 90,
      hitPoints: 40,
      size: 60,
      fireIntervalMilliseconds: 900,
      arms: 6,
      spinDegrees: 11,
      shotSpeed: 0.5,
      minPlayers: 1,
      rewardMass: 60,
      foodsCredit: 15,
      reward: { ...EMPTY_REWARD },
      rooms: [],
      excludedRooms: [],
    },
    zone: {
      enabled: true,
      minIntervalSeconds: 300,
      maxIntervalSeconds: 600,
      warningSeconds: 15,
      shrinkSeconds: 60,
      holdSeconds: 30,
      finalRadius: 160,
      damageIntervalMilliseconds: 1000,
      damageMass: 4,
      minPlayers: 2,
      rooms: [],
      excludedRooms: [],
    },
    fruitShower: {
      enabled: true,
      minIntervalSeconds: 180,
      maxIntervalSeconds: 420,
      durationSeconds: 20,
      fruitsPerSecond: 8,
      minPlayers: 1,
      rooms: [],
      excludedRooms: [],
    },
    bounty: {
      enabled: true,
      minIntervalSeconds: 240,
      maxIntervalSeconds: 600,
      durationSeconds: 90,
      rewardMass: 150,
      foodsCredit: 10,
      reward: { ...EMPTY_REWARD },
      minPlayers: 3,
      rooms: [],
      excludedRooms: [],
    },
    mines: {
      enabled: true,
      minIntervalSeconds: 150,
      maxIntervalSeconds: 360,
      minMines: 6,
      maxMines: 14,
      radius: 60,
      triggerRadius: 20,
      armMilliseconds: 2000,
      minPlayers: 1,
      rooms: [],
      excludedRooms: [],
    },
    sugarRush: {
      enabled: true,
      minIntervalSeconds: 240,
      maxIntervalSeconds: 600,
      durationSeconds: 30,
      minPlayers: 1,
      rooms: [],
      excludedRooms: [],
    },
    maze: {
      enabled: true,
      minIntervalSeconds: 420,
      maxIntervalSeconds: 900,
      durationSeconds: 240,
      columns: 12,
      rows: 8,
      corridorHeads: 4,
      twistiness: 60,
      wallMassLoss: 2,
      foodPercent: 25,
      minPlayers: 2,
      prizeMass: 200,
      prizeFoodsCredit: 20,
      prizeReward: { ...EMPTY_REWARD },
      prizeColor: "#ffd23f",
      rooms: [],
      excludedRooms: [],
    },
  },
});

const defaults = config({ value: 25 });
const payload = { current: { revision: "r1", config: config() }, defaults, effects: ["ExtraShots", "Magnet"] };

class MemoryStorage {
  values = new Map<string, string>([[ADMIN_KEY_STORAGE_KEY, "secret"]]);
  getItem = (key: string) => this.values.get(key) ?? null;
  setItem = (key: string, value: string) => void this.values.set(key, value);
  removeItem = (key: string) => void this.values.delete(key);
}

let http: { get: ReturnType<typeof vi.fn>; put: ReturnType<typeof vi.fn>; checkScript: ReturnType<typeof vi.fn> };
let storage: MemoryStorage;

const create = () => useAdminGameplay({ managerUrl: "http://manager.test/", http: http as unknown as GameplayHttp, storage });
const failure = (status: number, error?: string) => Object.assign(new Error(`HTTP ${status}`), { status, serverMessage: error });

beforeEach(() => {
  http = {
    get: vi.fn(async () => structuredClone(payload)),
    put: vi.fn(async (_url: string, _key: string, body: GameplayConfig) => ({ revision: "r2", config: structuredClone(body) })),
    checkScript: vi.fn(async () => ({ ok: true, error: null })),
  };
  storage = new MemoryStorage();
});

describe("useAdminGameplay", () => {
  it("loads the current config with the admin key", async () => {
    const gameplay = create();

    await gameplay.load();

    expect(http.get).toHaveBeenCalledWith("http://manager.test/admin/gameplay", "secret");
    expect(gameplay.draft.value).toEqual(config());
    expect(gameplay.effects.value).toEqual(["ExtraShots", "Magnet"]);
    expect(gameplay.status.value).toBe("ready");
  });

  it("does nothing without a key", async () => {
    storage.values.clear();
    const gameplay = create();

    await gameplay.load();

    expect(http.get).not.toHaveBeenCalled();
    expect(gameplay.status.value).toBe("idle");
  });

  it("is not changed until something is edited", async () => {
    const gameplay = create();
    await gameplay.load();

    expect(gameplay.dirty.value).toBe(false);

    gameplay.draft.value!.prize.prizes[0].value = 99;

    expect(gameplay.dirty.value).toBe(true);
  });

  it("saves what was edited and then counts as unchanged", async () => {
    const gameplay = create();
    await gameplay.load();
    gameplay.draft.value!.prize.prizes[0].value = 99;

    await gameplay.save();

    expect(http.put).toHaveBeenCalledWith("http://manager.test/admin/gameplay", "secret", expect.objectContaining({ prize: expect.objectContaining({ prizes: [expect.objectContaining({ value: 99 })] }) }));
    expect(gameplay.dirty.value).toBe(false);
    expect(gameplay.revision.value).toBe("r2");
    expect(gameplay.status.value).toBe("saved");
  });

  it("shows the server's reason when it refuses the config and keeps the edits", async () => {
    http.put.mockRejectedValue(failure(400, "Perk 'x' needs at least 1 food."));
    const gameplay = create();
    await gameplay.load();
    gameplay.draft.value!.prize.prizes[0].value = 99;

    await gameplay.save();

    expect(gameplay.error.value).toBe("Perk 'x' needs at least 1 food.");
    expect(gameplay.draft.value!.prize.prizes[0].value).toBe(99);
    expect(gameplay.dirty.value).toBe(true);
  });

  it("a wrong key is reported", async () => {
    http.get.mockRejectedValue(failure(401));
    const gameplay = create();

    await gameplay.load();

    expect(gameplay.status.value).toBe("signedOut");
  });

  it("other failures are reported as unavailable", async () => {
    http.get.mockRejectedValue(failure(500));
    const gameplay = create();

    await gameplay.load();

    expect(gameplay.status.value).toBe("error");
    expect(gameplay.error.value).toBe("unavailable");
  });

  it("goes back to the defaults as an edit, not a save", async () => {
    const gameplay = create();
    await gameplay.load();

    gameplay.useDefaults();

    expect(gameplay.draft.value!.prize.prizes[0].value).toBe(25);
    expect(gameplay.dirty.value).toBe(true);
    expect(http.put).not.toHaveBeenCalled();
  });

  it("drops the edits back to what is saved", async () => {
    const gameplay = create();
    await gameplay.load();
    gameplay.draft.value!.prize.prizes[0].value = 99;

    gameplay.revert();

    expect(gameplay.draft.value!.prize.prizes[0].value).toBe(20);
    expect(gameplay.dirty.value).toBe(false);
  });

  it("adds a perk with sensible starting values and a free id", async () => {
    const gameplay = create();
    await gameplay.load();

    gameplay.addPerk();
    gameplay.addPerk();

    const perks = gameplay.draft.value!.perks.perks;
    expect(perks).toHaveLength(4);
    expect(new Set(perks.map((perk) => perk.id)).size).toBe(4);
    expect(perks[2]).toMatchObject({ foodsRequired: 10, enabled: true, effects: [{ effect: "ExtraShots", amount: 1 }], tiers: [], rooms: [], excludedRooms: [] });
  });

  it("removes a perk", async () => {
    const gameplay = create();
    await gameplay.load();

    gameplay.removePerk(0);

    expect(gameplay.draft.value!.perks.perks.map((perk) => perk.id)).toEqual(["twin"]);
  });

  it("keeps the perks sorted by foods when it saves, as the server reads them", async () => {
    const gameplay = create();
    await gameplay.load();
    gameplay.draft.value!.perks.perks[0].foodsRequired = 40;

    await gameplay.save();

    const sent = http.put.mock.calls[0][2] as GameplayConfig;
    expect(sent.perks.perks.map((perk) => perk.id)).toEqual(["twin", "magnet"]);
  });

  it("edits never touch what is saved", async () => {
    const gameplay = create();
    await gameplay.load();

    gameplay.draft.value!.perks.perks[0].effects[0].amount = 1;

    expect(gameplay.saved.value!.perks.perks[0].effects[0].amount).toBe(60);
  });

  it("adds an effect to a perk, so a perk can do several things", async () => {
    const gameplay = create();
    await gameplay.load();

    gameplay.addEffect(0);

    const effects = gameplay.draft.value!.perks.perks[0].effects;
    expect(effects).toHaveLength(2);
    expect(effects[1]).toEqual({ effect: "ExtraShots", amount: 1 });
  });

  it("removes an effect of a perk, but never the last one", async () => {
    const gameplay = create();
    await gameplay.load();
    gameplay.addEffect(0);

    gameplay.removeEffect(0, 0);
    gameplay.removeEffect(0, 0);

    expect(gameplay.draft.value!.perks.perks[0].effects).toHaveLength(1);
  });

  it("a perk with a script may have no effect of its own", async () => {
    const gameplay = create();
    await gameplay.load();
    gameplay.draft.value!.perks.perks[0].script = "function modify() { return {}; }";

    gameplay.removeEffect(0, 0);

    expect(gameplay.draft.value!.perks.perks[0].effects).toHaveLength(0);
  });

  it("keeps the rooms of a perk through a save", async () => {
    const gameplay = create();
    await gameplay.load();
    gameplay.draft.value!.perks.perks[1].rooms = ["ABC234"];

    await gameplay.save();

    const sent = http.put.mock.calls[0][2] as GameplayConfig;
    expect(sent.perks.perks.find((perk) => perk.id === "twin")!.rooms).toEqual(["ABC234"]);
  });

  it("does not send blank rooms", async () => {
    const gameplay = create();
    await gameplay.load();
    gameplay.draft.value!.perks.perks[1].rooms = ["ABC234", "  ", ""];

    await gameplay.save();

    const sent = http.put.mock.calls[0][2] as GameplayConfig;
    expect(sent.perks.perks.find((perk) => perk.id === "twin")!.rooms).toEqual(["ABC234"]);
  });

  it("does not send blank left-out rooms either", async () => {
    const gameplay = create();
    await gameplay.load();
    gameplay.draft.value!.perks.perks[1].excludedRooms = ["ABC234", " "];

    await gameplay.save();

    const sent = http.put.mock.calls[0][2] as GameplayConfig;
    expect(sent.perks.perks.find((perk) => perk.id === "twin")!.excludedRooms).toEqual(["ABC234"]);
  });

  it("fills in the rooms of a perk an older server sent without them", async () => {
    http.get.mockResolvedValue({
      ...payload,
      current: { revision: "r1", config: { ...config(), perks: { ...config().perks, perks: [{ id: "old", name: "Old", foodsRequired: 3, effects: [{ effect: "Magnet", amount: 1 }] }] } } },
    });
    const gameplay = create();

    await gameplay.load();

    expect(gameplay.draft.value!.perks.perks[0].rooms).toEqual([]);
    expect(gameplay.draft.value!.perks.perks[0].excludedRooms).toEqual([]);
  });

  describe("world events", () => {
    it("fills in lightning for a server that does not know world events yet", async () => {
      const { worldEvents: _unknown, ...older } = config();
      http.get.mockResolvedValue({ ...payload, current: { revision: "r1", config: older } });
      const gameplay = create();

      await gameplay.load();

      expect(gameplay.draft.value!.worldEvents).toEqual(config().worldEvents);
    });

    it("fills in the zone for a server that does not know it yet", async () => {
      const { zone: _unknown, ...older } = config().worldEvents;
      http.get.mockResolvedValue({ ...payload, current: { revision: "r1", config: { ...config(), worldEvents: older } } });
      const gameplay = create();

      await gameplay.load();

      expect(gameplay.draft.value!.worldEvents.zone).toEqual(config().worldEvents.zone);
    });

    it("keeps the zone settings the server sent and fills in the rest", async () => {
      http.get.mockResolvedValue({
        ...payload,
        current: { revision: "r1", config: { ...config(), worldEvents: { ...config().worldEvents, zone: { finalRadius: 250, rooms: ["arena"] } } } },
      });
      const gameplay = create();

      await gameplay.load();

      expect(gameplay.draft.value!.worldEvents.zone).toEqual({ ...config().worldEvents.zone, finalRadius: 250, rooms: ["arena"] });
    });

    it("sends the zone settings without blank rooms", async () => {
      const gameplay = create();
      await gameplay.load();
      const zone = gameplay.draft.value!.worldEvents.zone;
      zone.shrinkSeconds = 90;
      zone.rooms = ["arena", " "];
      zone.excludedRooms = ["", "calm "];

      await gameplay.save();

      const sent = (http.put.mock.calls[0][2] as GameplayConfig).worldEvents.zone;
      expect(sent).toMatchObject({ shrinkSeconds: 90, rooms: ["arena"], excludedRooms: ["calm"] });
    });

    it("fills in the turret for a server that does not know it yet", async () => {
      const { turret: _unknown, ...older } = config().worldEvents;
      http.get.mockResolvedValue({ ...payload, current: { revision: "r1", config: { ...config(), worldEvents: older } } });
      const gameplay = create();

      await gameplay.load();

      expect(gameplay.draft.value!.worldEvents.turret).toEqual(config().worldEvents.turret);
    });

    it("keeps the turret settings the server sent, with a perk it gives", async () => {
      http.get.mockResolvedValue({
        ...payload,
        current: { revision: "r1", config: { ...config(), worldEvents: { ...config().worldEvents, turret: { hitPoints: 99, grantsPerk: "magnet" } } } },
      });
      const gameplay = create();

      await gameplay.load();

      expect(gameplay.draft.value!.worldEvents.turret).toEqual({ ...config().worldEvents.turret, hitPoints: 99, grantsPerk: "magnet" });
    });

    it("sends the turret settings without blank rooms, and without a blank perk", async () => {
      const gameplay = create();
      await gameplay.load();
      const turret = gameplay.draft.value!.worldEvents.turret;
      turret.hitPoints = 25;
      turret.grantsPerk = "  ";
      turret.rooms = ["arena", " "];
      turret.excludedRooms = ["", "calm "];

      await gameplay.save();

      const sent = (http.put.mock.calls[0][2] as GameplayConfig).worldEvents.turret;
      expect(sent).toMatchObject({ hitPoints: 25, rooms: ["arena"], excludedRooms: ["calm"] });
      expect(sent.grantsPerk).toBeUndefined();
    });

    it("sends the perk the turret gives, trimmed", async () => {
      const gameplay = create();
      await gameplay.load();
      gameplay.draft.value!.worldEvents.turret.grantsPerk = " magnet ";

      await gameplay.save();

      expect((http.put.mock.calls[0][2] as GameplayConfig).worldEvents.turret.grantsPerk).toBe("magnet");
    });

    it("makes lightning from before storms a storm to edit, with the storm defaults", async () => {
      const { minStrikes: _a, maxStrikes: _b, stormSeconds: _c, aimedShare: _d, ...single } = config().worldEvents.lightning;
      http.get.mockResolvedValue({
        ...payload,
        current: { revision: "r1", config: { ...config(), worldEvents: { enabled: true, lightning: single } } },
      });
      const gameplay = create();

      await gameplay.load();

      expect(gameplay.draft.value!.worldEvents.lightning).toMatchObject({ minStrikes: 5, maxStrikes: 12, stormSeconds: 6, aimedShare: 0.25 });
      expect(gameplay.draft.value!.worldEvents.meteors).toEqual(config().worldEvents.meteors);
    });

    it("sends the meteor settings without blank rooms", async () => {
      const gameplay = create();
      await gameplay.load();
      gameplay.draft.value!.worldEvents.meteors.maxMeteors = 20;
      gameplay.draft.value!.worldEvents.meteors.rooms = ["arena", " "];
      gameplay.draft.value!.worldEvents.meteors.excludedRooms = ["", "calm "];

      await gameplay.save();

      const sent = http.put.mock.calls[0][2] as GameplayConfig;
      expect(sent.worldEvents.meteors).toMatchObject({ maxMeteors: 20, rooms: ["arena"], excludedRooms: ["calm"] });
    });

    it("keeps what the server sent and fills in the rest of lightning", async () => {
      http.get.mockResolvedValue({
        ...payload,
        current: { revision: "r1", config: { ...config(), worldEvents: { enabled: false, lightning: { radius: 150, rooms: ["ABC234"] } } } },
      });
      const gameplay = create();

      await gameplay.load();

      expect(gameplay.draft.value!.worldEvents).toEqual({
        enabled: false,
        lightning: { ...config().worldEvents.lightning, radius: 150, rooms: ["ABC234"] },
        meteors: config().worldEvents.meteors,
        turret: config().worldEvents.turret,
        zone: config().worldEvents.zone,
        fruitShower: config().worldEvents.fruitShower,
        bounty: config().worldEvents.bounty,
        mines: config().worldEvents.mines,
        sugarRush: config().worldEvents.sugarRush,
        maze: config().worldEvents.maze,
      });
    });

    it("fills in the maze for a config saved before it, and sends it on save", async () => {
      const { maze: _maze, ...older } = config().worldEvents;
      http.get.mockResolvedValue({ ...payload, current: { revision: "r1", config: { ...config(), worldEvents: older } } });
      const gameplay = create();

      await gameplay.load();
      await gameplay.save();

      expect(gameplay.draft.value!.worldEvents.maze).toEqual(config().worldEvents.maze);
      expect((http.put.mock.calls[0][2] as GameplayConfig).worldEvents.maze).toEqual(config().worldEvents.maze);
    });

    it("keeps what the server sent of the maze and fills in the rest", async () => {
      http.get.mockResolvedValue({
        ...payload,
        current: {
          revision: "r1",
          config: { ...config(), worldEvents: { ...config().worldEvents, maze: { corridorHeads: 6, twistiness: 10, enabled: false, prizeGrantsPerk: "magnet" } } },
        },
      });
      const gameplay = create();

      await gameplay.load();

      expect(gameplay.draft.value!.worldEvents.maze).toEqual({
        ...config().worldEvents.maze,
        corridorHeads: 6,
        twistiness: 10,
        enabled: false,
        prizeGrantsPerk: "magnet",
      });
    });

    it("sends the maze without blank rooms, and the prize perk trimmed", async () => {
      const gameplay = create();
      await gameplay.load();
      const maze = gameplay.draft.value!.worldEvents.maze;
      maze.rooms = ["arena", " "];
      maze.excludedRooms = ["", "calm "];
      maze.prizeGrantsPerk = " magnet ";

      await gameplay.save();

      expect((http.put.mock.calls[0][2] as GameplayConfig).worldEvents.maze).toMatchObject({
        rooms: ["arena"],
        excludedRooms: ["calm"],
        prizeGrantsPerk: "magnet",
      });
    });

    it("leaves the prize perk out of the maze when it is blank", async () => {
      const gameplay = create();
      await gameplay.load();
      gameplay.draft.value!.worldEvents.maze.prizeGrantsPerk = "  ";

      await gameplay.save();

      expect((http.put.mock.calls[0][2] as GameplayConfig).worldEvents.maze.prizeGrantsPerk).toBeUndefined();
    });

    it("fills in the fruit shower, bounty, mines and sugar rush for a config saved before them, and sends them on save", async () => {
      const { fruitShower: _a, bounty: _b, mines: _c, sugarRush: _d, ...older } = config().worldEvents;
      http.get.mockResolvedValue({ ...payload, current: { revision: "r1", config: { ...config(), worldEvents: older } } });
      const gameplay = create();

      await gameplay.load();
      await gameplay.save();

      const events = config().worldEvents;
      const sent = (http.put.mock.calls[0][2] as GameplayConfig).worldEvents;
      for (const draft of [gameplay.draft.value!.worldEvents, sent]) {
        expect(draft.fruitShower).toEqual(events.fruitShower);
        expect(draft.bounty).toEqual(events.bounty);
        expect(draft.mines).toEqual(events.mines);
        expect(draft.sugarRush).toEqual(events.sugarRush);
      }
    });

    it("keeps what the server sent of the new events and fills in the rest", async () => {
      http.get.mockResolvedValue({
        ...payload,
        current: {
          revision: "r1",
          config: {
            ...config(),
            worldEvents: {
              ...config().worldEvents,
              fruitShower: { fruitsPerSecond: 20 },
              bounty: { rewardMass: 500, rooms: ["arena"] },
              mines: { maxMines: 30, enabled: false },
              sugarRush: { durationSeconds: 60, excludedRooms: ["calm"] },
            },
          },
        },
      });
      const gameplay = create();

      await gameplay.load();

      const events = gameplay.draft.value!.worldEvents;
      expect(events.fruitShower).toEqual({ ...config().worldEvents.fruitShower, fruitsPerSecond: 20 });
      expect(events.bounty).toEqual({ ...config().worldEvents.bounty, rewardMass: 500, rooms: ["arena"] });
      expect(events.mines).toEqual({ ...config().worldEvents.mines, maxMines: 30, enabled: false });
      expect(events.sugarRush).toEqual({ ...config().worldEvents.sugarRush, durationSeconds: 60, excludedRooms: ["calm"] });
    });

    it("sends the new events without blank rooms", async () => {
      const gameplay = create();
      await gameplay.load();
      const events = gameplay.draft.value!.worldEvents;
      for (const event of [events.fruitShower, events.bounty, events.mines, events.sugarRush]) {
        event.rooms = ["arena", " "];
        event.excludedRooms = ["", "calm "];
      }
      events.mines.triggerRadius = 35;

      await gameplay.save();

      const sent = (http.put.mock.calls[0][2] as GameplayConfig).worldEvents;
      for (const event of [sent.fruitShower, sent.bounty, sent.mines, sent.sugarRush]) expect(event).toMatchObject({ rooms: ["arena"], excludedRooms: ["calm"] });
      expect(sent.mines.triggerRadius).toBe(35);
    });

    it("sends the lightning settings without blank rooms", async () => {
      const gameplay = create();
      await gameplay.load();
      gameplay.draft.value!.worldEvents.lightning.radius = 120;
      gameplay.draft.value!.worldEvents.lightning.rooms = ["ABC234", " "];
      gameplay.draft.value!.worldEvents.lightning.excludedRooms = ["", "calm "];

      await gameplay.save();

      const sent = http.put.mock.calls[0][2] as GameplayConfig;
      expect(sent.worldEvents.lightning).toMatchObject({ radius: 120, rooms: ["ABC234"], excludedRooms: ["calm"] });
    });

    it("counts a change to lightning as unsaved, and takes it back on revert", async () => {
      const gameplay = create();
      await gameplay.load();

      gameplay.draft.value!.worldEvents.lightning.enabled = false;
      expect(gameplay.dirty.value).toBe(true);
      gameplay.revert();

      expect(gameplay.dirty.value).toBe(false);
    });
  });

  it("does not send an empty script", async () => {
    const gameplay = create();
    await gameplay.load();
    gameplay.draft.value!.perks.perks[0].script = "   ";

    await gameplay.save();

    const sent = http.put.mock.calls[0][2] as GameplayConfig;
    expect(sent.perks.perks[0].script).toBeUndefined();
  });

  describe("scripts", () => {
    it("adds a script to a perk, starting from an example that works", async () => {
      const gameplay = create();
      await gameplay.load();

      gameplay.addScript(0);

      const script = gameplay.draft.value!.perks.perks[0].script!;
      expect(script).toContain("function modify");
      expect(script).toContain("function onEat");
    });

    it("keeps the script a perk already has when asked to add one", async () => {
      const gameplay = create();
      await gameplay.load();
      gameplay.draft.value!.perks.perks[0].script = "function modify() { return {}; }";

      gameplay.addScript(0);

      expect(gameplay.draft.value!.perks.perks[0].script).toBe("function modify() { return {}; }");
    });

    it("removes a script", async () => {
      const gameplay = create();
      await gameplay.load();
      gameplay.addScript(0);

      gameplay.removeScript(0);

      expect(gameplay.draft.value!.perks.perks[0].script).toBeUndefined();
    });

    it("will not remove a script that a perk needs because it has no effect", async () => {
      const gameplay = create();
      await gameplay.load();
      gameplay.draft.value!.perks.perks[0].effects = [];
      gameplay.addScript(0);

      gameplay.removeScript(0);

      expect(gameplay.draft.value!.perks.perks[0].script).toBeDefined();
    });

    it("asks the server whether a script would be accepted", async () => {
      http.checkScript.mockResolvedValue({ ok: false, error: "Unexpected token" });
      const gameplay = create();
      await gameplay.load();

      const result = await gameplay.checkScript("function modify( {");

      expect(http.checkScript).toHaveBeenCalledWith("http://manager.test/admin/gameplay/check-script", "secret", "function modify( {");
      expect(result).toEqual({ ok: false, error: "Unexpected token" });
    });

    it("says so when the server cannot be asked", async () => {
      http.checkScript.mockRejectedValue(failure(500));
      const gameplay = create();
      await gameplay.load();

      const result = await gameplay.checkScript("function modify() {}");

      expect(result).toEqual({ ok: false, error: "unavailable" });
    });

    it("does not ask about nothing", async () => {
      const gameplay = create();
      await gameplay.load();

      const result = await gameplay.checkScript("   ");

      expect(result.ok).toBe(false);
      expect(http.checkScript).not.toHaveBeenCalled();
    });
  });

  describe("perks that can be switched off", () => {
    it("a new perk is on", async () => {
      const gameplay = create();
      await gameplay.load();

      gameplay.addPerk();

      expect(gameplay.draft.value!.perks.perks[2].enabled).toBe(true);
    });

    it("a perk an older server sent without the switch is on", async () => {
      http.get.mockResolvedValue({
        ...payload,
        current: { revision: "r1", config: { ...config(), perks: { ...config().perks, perks: [{ id: "old", name: "Old", foodsRequired: 3, effects: [{ effect: "Magnet", amount: 1 }] }] } } },
      });
      const gameplay = create();

      await gameplay.load();

      expect(gameplay.draft.value!.perks.perks[0].enabled).toBe(true);
    });

    it("keeps a perk that was switched off through a save", async () => {
      const gameplay = create();
      await gameplay.load();
      gameplay.draft.value!.perks.perks[1].enabled = false;

      await gameplay.save();

      const sent = http.put.mock.calls[0][2] as GameplayConfig;
      expect(sent.perks.perks.find((perk) => perk.id === "twin")!.enabled).toBe(false);
    });
  });

  describe("icon and description", () => {
    it("keeps them through a save, trimmed", async () => {
      const gameplay = create();
      await gameplay.load();
      gameplay.draft.value!.perks.perks[0].icon = " mdi-skull ";
      gameplay.draft.value!.perks.perks[0].description = "  Gets angry.  ";

      await gameplay.save();

      const sent = http.put.mock.calls[0][2] as GameplayConfig;
      expect(sent.perks.perks[0]).toMatchObject({ icon: "mdi-skull", description: "Gets angry." });
    });

    it("sends neither when they are blank", async () => {
      const gameplay = create();
      await gameplay.load();
      gameplay.draft.value!.perks.perks[0].icon = "  ";
      gameplay.draft.value!.perks.perks[0].description = "";

      await gameplay.save();

      const sent = http.put.mock.calls[0][2] as GameplayConfig;
      expect(sent.perks.perks[0].icon).toBeUndefined();
      expect(sent.perks.perks[0].description).toBeUndefined();
    });
  });

  describe("prizes", () => {
    it("adds a kind of prize with sensible starting values and a free id", async () => {
      const gameplay = create();
      await gameplay.load();

      gameplay.addPrize();
      gameplay.addPrize();

      const prizes = gameplay.draft.value!.prize.prizes;
      expect(prizes).toHaveLength(3);
      expect(new Set(prizes.map((prize) => prize.id)).size).toBe(3);
      expect(prizes[1]).toMatchObject({ enabled: true, spawnIntervalSeconds: 60, lifetimeSeconds: 45, value: 20, minPlayers: 2, color: "#ffd23f", rooms: [], excludedRooms: [] });
    });

    it("removes a kind of prize, but never the last one: a list with none would bring back the default (use the prize switch)", async () => {
      const gameplay = create();
      await gameplay.load();
      gameplay.addPrize();

      gameplay.removePrize(0);
      gameplay.removePrize(0);

      expect(gameplay.draft.value!.prize.prizes).toHaveLength(1);
    });

    it("a kind can give a perk, and the perk is kept through a save", async () => {
      const gameplay = create();
      await gameplay.load();
      gameplay.draft.value!.prize.prizes[0].grantsPerk = "twin";

      await gameplay.save();

      const sent = http.put.mock.calls[0][2] as GameplayConfig;
      expect(sent.prize.prizes[0].grantsPerk).toBe("twin");
    });

    it("sends no perk for a kind that gives none", async () => {
      const gameplay = create();
      await gameplay.load();
      gameplay.draft.value!.prize.prizes[0].grantsPerk = "";

      await gameplay.save();

      const sent = http.put.mock.calls[0][2] as GameplayConfig;
      expect(sent.prize.prizes[0].grantsPerk).toBeUndefined();
    });

    it("does not send blank rooms of a prize", async () => {
      const gameplay = create();
      await gameplay.load();
      gameplay.draft.value!.prize.prizes[0].rooms = ["ABC234", " "];
      gameplay.draft.value!.prize.prizes[0].excludedRooms = [""];

      await gameplay.save();

      const sent = http.put.mock.calls[0][2] as GameplayConfig;
      expect(sent.prize.prizes[0].rooms).toEqual(["ABC234"]);
      expect(sent.prize.prizes[0].excludedRooms).toEqual([]);
    });

    it("turns the single prize an older server sent into a kind", async () => {
      http.get.mockResolvedValue({
        ...payload,
        current: {
          revision: "r1",
          config: {
            ...config(),
            prize: { enabled: true, spawnIntervalSeconds: 33, lifetimeSeconds: 22, value: 55, foodsCredit: 4, minPlayers: 1 },
          },
        },
      });
      const gameplay = create();

      await gameplay.load();

      const [kind] = gameplay.draft.value!.prize.prizes;
      expect(kind).toMatchObject({ id: "prize", spawnIntervalSeconds: 33, lifetimeSeconds: 22, value: 55, foodsCredit: 4, minPlayers: 1, color: "#ffd23f" });
    });

    it("has the choice of perks a prize can give", async () => {
      const gameplay = create();
      await gameplay.load();

      expect(gameplay.perkChoices.value).toEqual([
        { id: "magnet", name: "Magnet" },
        { id: "twin", name: "Twin shot" },
      ]);
    });
  });

  describe("room settings", () => {
    it("loads how long private rooms live", async () => {
      const gameplay = create();
      await gameplay.load();

      expect(gameplay.draft.value!.rooms).toEqual({ emptyPrivateRoomMinutes: 10, maxPrivateRoomMinutes: 0 });
    });

    it("fills in the defaults when an older server sent none", async () => {
      const older = config() as Partial<GameplayConfig>;
      delete older.rooms;
      http.get.mockResolvedValue({ ...payload, current: { revision: "r1", config: older } });
      const gameplay = create();

      await gameplay.load();

      expect(gameplay.draft.value!.rooms).toEqual({ emptyPrivateRoomMinutes: 10, maxPrivateRoomMinutes: 0 });
    });

    it("saves a change", async () => {
      const gameplay = create();
      await gameplay.load();
      gameplay.draft.value!.rooms.maxPrivateRoomMinutes = 120;

      await gameplay.save();

      const sent = http.put.mock.calls[0][2] as GameplayConfig;
      expect(sent.rooms.maxPrivateRoomMinutes).toBe(120);
      expect(gameplay.dirty.value).toBe(false);
    });
  });
});

describe("perk tiers", () => {
  it("fills in no tiers and the credit for a kill for a server from before tiers", async () => {
    const { killFoodsCredit: _credit, ...perks } = config().perks;
    const older = { ...config(), perks: { ...perks, perks: perks.perks.map(({ tiers: _tiers, ...perk }) => perk) } };
    http.get.mockResolvedValue({ ...payload, current: { revision: "r1", config: older } });
    const gameplay = create();

    await gameplay.load();

    expect(gameplay.draft.value!.perks.killFoodsCredit).toBe(3);
    expect(gameplay.draft.value!.perks.perks.map((perk) => perk.tiers)).toEqual([[], []]);
  });

  it("adds a tier that asks for more foods and does more than the one before", async () => {
    const gameplay = create();
    await gameplay.load();

    gameplay.addTier(0);
    gameplay.addTier(0);

    expect(gameplay.draft.value!.perks.perks[0].tiers).toEqual([
      { foodsRequired: 12, multiplier: 1.5, rewardOnly: false },
      { foodsRequired: 18, multiplier: 2, rewardOnly: false },
    ]);
  });

  it("allows nine tiers after the first, no more", async () => {
    const gameplay = create();
    await gameplay.load();

    for (let count = 0; count < 12; count++) gameplay.addTier(0);

    expect(gameplay.draft.value!.perks.perks[0].tiers).toHaveLength(9);
  });

  it("takes the last tier away", async () => {
    const gameplay = create();
    await gameplay.load();
    gameplay.addTier(0);
    gameplay.addTier(0);

    gameplay.removeTier(0, 0);

    expect(gameplay.draft.value!.perks.perks[0].tiers).toEqual([{ foodsRequired: 18, multiplier: 2, rewardOnly: false }]);
  });

  it("does nothing for a perk that is not there", async () => {
    const gameplay = create();
    await gameplay.load();

    gameplay.addTier(9);
    gameplay.removeTier(9, 0);

    expect(gameplay.dirty.value).toBe(false);
  });

  it("sends the tiers in order of the foods they ask for", async () => {
    const gameplay = create();
    await gameplay.load();
    gameplay.addTier(0);
    gameplay.addTier(0);
    const tiers = gameplay.draft.value!.perks.perks[0].tiers!;
    [tiers[0], tiers[1]] = [tiers[1], tiers[0]];

    await gameplay.save();

    const sent = http.put.mock.calls[0][2] as GameplayConfig;
    expect(sent.perks.perks[0].tiers!.map((tier) => tier.foodsRequired)).toEqual([12, 18]);
  });
});

describe("useAdminGameplay rewards and cooldown perks", () => {
  /** A config from a server that does not know rewards, cooldown perks or extra lives yet: none of those fields are in it. */
  function oldServer() {
    const old = structuredClone(config()) as any;
    delete old.perks.extraLifeKeepShare;
    delete old.perks.extraLifeProtectSeconds;
    for (const perk of old.perks.perks) {
      delete perk.cooldownSeconds;
      delete perk.durationSeconds;
    }
    delete old.prize.prizes[0].reward;
    delete old.worldEvents.turret.reward;
    delete old.worldEvents.bounty.reward;
    delete old.worldEvents.maze.prizeReward;
    return old as GameplayConfig;
  }

  it("fills in what a server from before rewards does not send", async () => {
    http.get.mockResolvedValue({ ...payload, current: { revision: "r1", config: oldServer() } });
    const gameplay = create();

    await gameplay.load();

    const draft = gameplay.draft.value!;
    expect(draft).toEqual(config());
    expect(draft.perks.extraLifeKeepShare).toBe(0.5);
    expect(draft.perks.extraLifeProtectSeconds).toBe(3);
    expect(draft.prize.prizes[0].reward).toEqual(EMPTY_REWARD);
    expect(gameplay.dirty.value).toBe(false);
  });

  it("keeps the reward the server sent and fills in the fields it left out", async () => {
    const withReward = structuredClone(config()) as any;
    withReward.prize.prizes[0].reward = { extraLives: 2, tierSurgeMin: 1, tierSurgeMax: 3, tierSurgePerk: "magnet" };
    withReward.worldEvents.turret.reward = { mend: true };
    http.get.mockResolvedValue({ ...payload, current: { revision: "r1", config: withReward } });
    const gameplay = create();

    await gameplay.load();

    const draft = gameplay.draft.value!;
    expect(draft.prize.prizes[0].reward).toEqual({ ...EMPTY_REWARD, extraLives: 2, tierSurgeMin: 1, tierSurgeMax: 3, tierSurgePerk: "magnet" });
    expect(draft.worldEvents.turret.reward).toEqual({ ...EMPTY_REWARD, mend: true });
  });

  it("sends a reward without a blank perk to lift, and trims the one it has", async () => {
    const gameplay = create();
    await gameplay.load();
    const draft = gameplay.draft.value!;
    draft.prize.prizes[0].reward.tierSurgePerk = "  ";
    draft.worldEvents.turret.reward.tierSurgePerk = " magnet ";
    draft.worldEvents.bounty.reward.tierSurgePerk = "";
    draft.worldEvents.maze.prizeReward.tierSurgePerk = "twin";

    await gameplay.save();

    const sent = http.put.mock.calls[0][2] as GameplayConfig;
    expect("tierSurgePerk" in sent.prize.prizes[0].reward).toBe(false);
    expect(sent.worldEvents.turret.reward.tierSurgePerk).toBe("magnet");
    expect("tierSurgePerk" in sent.worldEvents.bounty.reward).toBe(false);
    expect(sent.worldEvents.maze.prizeReward.tierSurgePerk).toBe("twin");
  });

  it("sends the target of an event resistance, and none for any other effect or a blank one", async () => {
    const gameplay = create();
    await gameplay.load();
    const perks = gameplay.draft.value!.perks.perks;
    perks[0].effects = [{ effect: "EventResistance", amount: 0.4, target: "zone" }, { effect: "EventResistance", amount: 0.2, target: "" }];
    perks[1].effects = [{ effect: "Armor", amount: 0.2, target: "zone" }];

    await gameplay.save();

    const sent = http.put.mock.calls[0][2] as GameplayConfig;
    const [first, second] = sent.perks.perks.find((perk) => perk.id === "magnet")!.effects;
    expect(first.target).toBe("zone");
    expect("target" in second).toBe(false);
    expect("target" in sent.perks.perks.find((perk) => perk.id === "twin")!.effects[0]).toBe(false);
  });

  it("knows which perks have a cooldown ability", () => {
    expect(hasAbility({ effects: [{ effect: "Armor", amount: 1 }, { effect: "Dash", amount: 1 }] })).toBe(true);
    expect(hasAbility({ effects: [{ effect: "Armor", amount: 1 }] })).toBe(false);
  });

  it("sends the cooldown and duration of a perk with an ability, and zeros for a perk without one", async () => {
    const gameplay = create();
    await gameplay.load();
    const perks = gameplay.draft.value!.perks.perks;
    perks[0].effects = [{ effect: "ShieldBurst", amount: 1 }];
    perks[0].cooldownSeconds = 30;
    perks[0].durationSeconds = 4;
    perks[1].cooldownSeconds = 99;
    perks[1].durationSeconds = 9;

    await gameplay.save();

    const sent = http.put.mock.calls[0][2] as GameplayConfig;
    const shield = sent.perks.perks.find((perk) => perk.id === "magnet")!;
    const plain = sent.perks.perks.find((perk) => perk.id === "twin")!;
    expect([shield.cooldownSeconds, shield.durationSeconds]).toEqual([30, 4]);
    expect([plain.cooldownSeconds, plain.durationSeconds]).toEqual([0, 0]);
  });

  it("gives a new prize an empty reward of its own", async () => {
    const gameplay = create();
    await gameplay.load();

    gameplay.addPrize();
    gameplay.addPrize();
    const [, first, second] = [null, ...gameplay.draft.value!.prize.prizes];
    first!.reward.extraLives = 3;

    expect(second!.reward.extraLives).toBe(0);
    expect(EMPTY_REWARD.extraLives).toBe(0);
  });
});

describe("useAdminGameplay reward-only tiers", () => {
  it("lets a perk have nine tiers after its first, and no more", async () => {
    const gameplay = create();
    await gameplay.load();

    for (let count = 0; count < 12; count++) gameplay.addTier(0);

    expect(gameplay.draft.value!.perks.perks[0].tiers).toHaveLength(9);
  });

  it("fills in a tier from a server that does not send the flag as eatable", async () => {
    const old = structuredClone(config());
    old.perks.perks[0].tiers = [{ foodsRequired: 20, multiplier: 2 }];
    http.get.mockResolvedValue({ ...payload, current: { revision: "r1", config: old } });
    const gameplay = create();

    await gameplay.load();

    expect(gameplay.draft.value!.perks.perks[0].tiers![0].rewardOnly).toBe(false);
    expect(gameplay.dirty.value).toBe(false);
  });

  it("says a reward-only tier is out of place when an eatable one comes after it, whatever the order in the form", () => {
    const ok = [{ foodsRequired: 10, multiplier: 2 }, { foodsRequired: 20, multiplier: 3, rewardOnly: true }];
    const bad = [{ foodsRequired: 10, multiplier: 2, rewardOnly: true }, { foodsRequired: 20, multiplier: 3 }];

    expect(rewardOnlyOutOfPlace(ok)).toBe(false);
    expect(rewardOnlyOutOfPlace(bad)).toBe(true);
    expect(rewardOnlyOutOfPlace([...bad].reverse())).toBe(true);
    expect(rewardOnlyOutOfPlace([])).toBe(false);
  });

  it("sends the flag of each tier", async () => {
    const gameplay = create();
    await gameplay.load();
    gameplay.draft.value!.perks.perks[0].tiers = [{ foodsRequired: 20, multiplier: 2 }, { foodsRequired: 30, multiplier: 5, rewardOnly: true }];

    await gameplay.save();

    const sent = http.put.mock.calls[0][2] as GameplayConfig;
    expect(sent.perks.perks.find((perk) => perk.id === "magnet")!.tiers!.map((tier) => tier.rewardOnly)).toEqual([false, true]);
  });
});
