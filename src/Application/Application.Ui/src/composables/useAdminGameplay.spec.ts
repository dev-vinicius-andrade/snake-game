import { beforeEach, describe, expect, it, vi } from "vitest";
import { ADMIN_KEY_STORAGE_KEY } from "./useAdminRooms";
import { useAdminGameplay, type GameplayConfig, type GameplayHttp, type PrizeDefinition } from "./useAdminGameplay";

const config = (overrides: Partial<PrizeDefinition> = {}): GameplayConfig => ({
  perks: {
    enabled: true,
    shotSpreadDegrees: 12,
    burstIntervalMilliseconds: 80,
    perks: [
      { id: "magnet", name: "Magnet", foodsRequired: 6, enabled: true, effects: [{ effect: "Magnet", amount: 60 }], rooms: [], excludedRooms: [] },
      { id: "twin", name: "Twin shot", foodsRequired: 15, enabled: true, effects: [{ effect: "ExtraShots", amount: 1 }], rooms: [], excludedRooms: [] },
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
        ...overrides,
      },
    ],
  },
  rooms: { emptyPrivateRoomMinutes: 10, maxPrivateRoomMinutes: 0 },
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
    expect(perks[2]).toMatchObject({ foodsRequired: 10, enabled: true, effects: [{ effect: "ExtraShots", amount: 1 }], rooms: [], excludedRooms: [] });
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
