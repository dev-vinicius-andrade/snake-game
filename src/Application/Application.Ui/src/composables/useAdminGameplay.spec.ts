import { beforeEach, describe, expect, it, vi } from "vitest";
import { ADMIN_KEY_STORAGE_KEY } from "./useAdminRooms";
import { useAdminGameplay, type GameplayConfig, type GameplayHttp } from "./useAdminGameplay";

const config = (overrides: Partial<GameplayConfig["prize"]> = {}): GameplayConfig => ({
  perks: {
    enabled: true,
    shotSpreadDegrees: 12,
    burstIntervalMilliseconds: 80,
    perks: [
      { id: "magnet", name: "Magnet", foodsRequired: 6, effects: [{ effect: "Magnet", amount: 60 }], rooms: [], excludedRooms: [] },
      { id: "twin", name: "Twin shot", foodsRequired: 15, effects: [{ effect: "ExtraShots", amount: 1 }], rooms: [], excludedRooms: [] },
    ],
  },
  prize: { enabled: true, spawnIntervalSeconds: 60, lifetimeSeconds: 45, value: 20, foodsCredit: 8, minPlayers: 2, ...overrides },
});

const defaults = config({ value: 25 });
const payload = { current: { revision: "r1", config: config() }, defaults, effects: ["ExtraShots", "Magnet"] };

class MemoryStorage {
  values = new Map<string, string>([[ADMIN_KEY_STORAGE_KEY, "secret"]]);
  getItem = (key: string) => this.values.get(key) ?? null;
  setItem = (key: string, value: string) => void this.values.set(key, value);
  removeItem = (key: string) => void this.values.delete(key);
}

let http: { get: ReturnType<typeof vi.fn>; put: ReturnType<typeof vi.fn> };
let storage: MemoryStorage;

const create = () => useAdminGameplay({ managerUrl: "http://manager.test/", http: http as unknown as GameplayHttp, storage });
const failure = (status: number, error?: string) => Object.assign(new Error(`HTTP ${status}`), { status, serverMessage: error });

beforeEach(() => {
  http = {
    get: vi.fn(async () => structuredClone(payload)),
    put: vi.fn(async (_url: string, _key: string, body: GameplayConfig) => ({ revision: "r2", config: structuredClone(body) })),
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

    gameplay.draft.value!.prize.value = 99;

    expect(gameplay.dirty.value).toBe(true);
  });

  it("saves what was edited and then counts as unchanged", async () => {
    const gameplay = create();
    await gameplay.load();
    gameplay.draft.value!.prize.value = 99;

    await gameplay.save();

    expect(http.put).toHaveBeenCalledWith("http://manager.test/admin/gameplay", "secret", expect.objectContaining({ prize: expect.objectContaining({ value: 99 }) }));
    expect(gameplay.dirty.value).toBe(false);
    expect(gameplay.revision.value).toBe("r2");
    expect(gameplay.status.value).toBe("saved");
  });

  it("shows the server's reason when it refuses the config and keeps the edits", async () => {
    http.put.mockRejectedValue(failure(400, "Perk 'x' needs at least 1 food."));
    const gameplay = create();
    await gameplay.load();
    gameplay.draft.value!.prize.value = 99;

    await gameplay.save();

    expect(gameplay.error.value).toBe("Perk 'x' needs at least 1 food.");
    expect(gameplay.draft.value!.prize.value).toBe(99);
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

    expect(gameplay.draft.value!.prize.value).toBe(25);
    expect(gameplay.dirty.value).toBe(true);
    expect(http.put).not.toHaveBeenCalled();
  });

  it("drops the edits back to what is saved", async () => {
    const gameplay = create();
    await gameplay.load();
    gameplay.draft.value!.prize.value = 99;

    gameplay.revert();

    expect(gameplay.draft.value!.prize.value).toBe(20);
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
    expect(perks[2]).toMatchObject({ foodsRequired: 10, effects: [{ effect: "ExtraShots", amount: 1 }], rooms: [], excludedRooms: [] });
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
});
