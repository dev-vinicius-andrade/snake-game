import { describe, expect, it, vi } from "vitest";
import { usePerkCatalog, type PerkCatalogHttp } from "./usePerkCatalog";

const perk = { id: "magnet", name: "Magnet", foodsRequired: 6, effects: [{ effect: "Magnet", amount: 60 }] };

const create = (get: PerkCatalogHttp["get"]) => usePerkCatalog({ managerUrl: "http://manager.test/", http: { get } });

const tiered = { id: "magnet", name: "Magnet", foodsRequired: 6, effects: [{ effect: "Magnet", amount: 60 }], tiers: [{ foodsRequired: 15, multiplier: 1.5 }] };

describe("usePerkCatalog", () => {
  it("is empty until loaded", () => {
    expect(create(vi.fn()).perks.value).toEqual([]);
  });

  it("loads the perks from the manager", async () => {
    const get = vi.fn(async () => ({ revision: "r1", perks: [perk] }));
    const catalog = create(get);

    await catalog.load();

    expect(get).toHaveBeenCalledWith("http://manager.test/gameplay/perks");
    expect(catalog.perks.value).toEqual([{ ...perk, tiers: [] }]);
  });

  it("finds a perk by its id", async () => {
    const catalog = create(async () => ({ revision: "r1", perks: [perk] }));
    await catalog.load();

    expect(catalog.find("magnet")).toEqual({ ...perk, tiers: [] });
    expect(catalog.find("nope")).toBeUndefined();
  });

  it("keeps what it had when the manager cannot be reached", async () => {
    let fail = false;
    const catalog = create(async () => {
      if (fail) throw new Error("down");
      return { revision: "r1", perks: [perk] };
    });
    await catalog.load();

    fail = true;
    await catalog.load();

    expect(catalog.perks.value).toEqual([{ ...perk, tiers: [] }]);
  });

  it("ignores a reply that is not a list of perks", async () => {
    const catalog = create(async () => ({ revision: "r1", perks: "nope" }) as never);

    await catalog.load();

    expect(catalog.perks.value).toEqual([]);
  });

  it("drops entries that are not perks", async () => {
    const catalog = create(async () => ({ revision: "r1", perks: [perk, null, { id: 5 }, { id: "x", effects: "nope" }] }) as never);

    await catalog.load();

    expect(catalog.perks.value).toEqual([{ ...perk, tiers: [] }]);
  });
});

describe("usePerkCatalog tiers", () => {
  it("keeps the tiers of a perk", async () => {
    const catalog = usePerkCatalog({ managerUrl: "http://m", http: { get: async () => ({ revision: "r", perks: [tiered] }) } });

    await catalog.load();

    expect(catalog.find("magnet")?.tiers).toEqual([{ foodsRequired: 15, multiplier: 1.5 }]);
  });

  it("gives a perk with none an empty list, and drops a tier that is not a pair of numbers", async () => {
    const plain = { id: "swift", name: "Swift", foodsRequired: 8, effects: [{ effect: "BoostSpeed", amount: 0.25 }] };
    const broken = { ...tiered, id: "x", tiers: [{ foodsRequired: "a", multiplier: 2 }, { foodsRequired: 20, multiplier: 2 }] };
    const catalog = usePerkCatalog({ managerUrl: "http://m", http: { get: async () => ({ revision: "r", perks: [plain, broken] }) } });

    await catalog.load();

    expect(catalog.find("swift")?.tiers).toEqual([]);
    expect(catalog.find("x")?.tiers).toEqual([{ foodsRequired: 20, multiplier: 2 }]);
  });
});
