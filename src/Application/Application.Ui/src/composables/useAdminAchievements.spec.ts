import { beforeEach, describe, expect, it, vi } from "vitest";
import { ACCOUNT_CREDENTIAL } from "./adminAuth";
import { useAdminAchievements, type AchievementsHttp, type AchievementsPayload } from "./useAdminAchievements";
import { ADMIN_KEY_STORAGE_KEY } from "./useAdminRooms";

class MemoryStorage {
  values = new Map<string, string>();
  getItem = (key: string) => this.values.get(key) ?? null;
  setItem = (key: string, value: string) => void this.values.set(key, value);
  removeItem = (key: string) => void this.values.delete(key);
}

const payload: AchievementsPayload = {
  achievements: [
    { id: "hunter", icon: "game-icons:crosshair", stat: "kills", threshold: 25, enabled: true, texts: {} },
    { id: "snacker", icon: "game-icons:sparkles", stat: "foods", threshold: 10, enabled: false, texts: { en: { name: "Snacker", description: "Eat 10." } } },
  ],
  stats: ["games", "kills", "foods", "bestScore"],
  writable: true,
};

const failure = (status: number, serverMessage?: string) => Object.assign(new Error(`HTTP ${status}`), { status, serverMessage });

let http: { get: ReturnType<typeof vi.fn>; put: ReturnType<typeof vi.fn>; delete: ReturnType<typeof vi.fn> };
let storage: MemoryStorage;

const create = (key: string | null = "secret") => {
  if (key) storage.setItem(ADMIN_KEY_STORAGE_KEY, key);
  return useAdminAchievements({ managerUrl: "http://manager.test/", http: http as unknown as AchievementsHttp, storage });
};

beforeEach(() => {
  http = { get: vi.fn(async () => payload), put: vi.fn(async () => undefined), delete: vi.fn(async () => undefined) };
  storage = new MemoryStorage();
});

describe("useAdminAchievements", () => {
  it("loads every achievement, the stats that can unlock one, and whether they can be changed", async () => {
    const admin = create();

    await admin.load();

    expect(http.get).toHaveBeenCalledWith("http://manager.test/admin/achievements", "secret");
    expect(admin.status.value).toBe("ready");
    expect(admin.entries.value.map((entry) => entry.id)).toEqual(["hunter", "snacker"]);
    expect(admin.stats.value).toContain("bestScore");
    expect(admin.writable.value).toBe(true);
  });

  it("does nothing without a way into the admin api", async () => {
    const admin = create(null);

    await admin.load();

    expect(http.get).not.toHaveBeenCalled();
  });

  it("asks with the signed in account as well as with a key", async () => {
    const admin = create(ACCOUNT_CREDENTIAL);

    await admin.load();

    expect(http.get).toHaveBeenCalledWith("http://manager.test/admin/achievements", ACCOUNT_CREDENTIAL);
  });

  it("refused (401 or 403) signs the editor out", async () => {
    http.get.mockRejectedValue(failure(403));
    const admin = create();

    await admin.load();

    expect(admin.status.value).toBe("signedOut");
    expect(admin.error.value).toBe("invalidKey");
  });

  it("saves one with only the languages that have a name, then loads the list again", async () => {
    const admin = create();
    await admin.load();

    const saved = await admin.save({
      id: "big-eater",
      icon: "game-icons:sparkles",
      stat: "foods",
      threshold: 100,
      enabled: true,
      texts: { en: { name: " Big eater ", description: "Eat 100." }, pt: { name: "  ", description: "x" } },
    });

    expect(saved).toBe(true);
    expect(http.put).toHaveBeenCalledWith("http://manager.test/admin/achievements/big-eater", "secret", {
      icon: "game-icons:sparkles",
      stat: "foods",
      threshold: 100,
      enabled: true,
      texts: { en: { name: "Big eater", description: "Eat 100." } },
    });
    expect(http.get).toHaveBeenCalledTimes(2);
  });

  it("keeps what the server said when it refuses an achievement, and does not reload", async () => {
    http.put.mockRejectedValue(failure(400, "invalidThreshold"));
    const admin = create();
    await admin.load();

    const saved = await admin.save({ id: "x-y", icon: "mdi-star", stat: "kills", threshold: 0, enabled: true, texts: {} });

    expect(saved).toBe(false);
    expect(admin.error.value).toBe("invalidThreshold");
    expect(http.get).toHaveBeenCalledTimes(1);
  });

  it("says readOnly when the manager has no database to change them in", async () => {
    http.put.mockRejectedValue(failure(409, "readOnly"));
    const admin = create();
    await admin.load();

    expect(await admin.save({ id: "x-y", icon: "mdi-star", stat: "kills", threshold: 1, enabled: true, texts: {} })).toBe(false);
    expect(admin.error.value).toBe("readOnly");
  });

  it("deletes one and reloads; one somebody unlocked says inUse", async () => {
    const admin = create();
    await admin.load();

    expect(await admin.remove("snacker")).toBe(true);
    expect(http.delete).toHaveBeenCalledWith("http://manager.test/admin/achievements/snacker", "secret");

    http.delete.mockRejectedValue(failure(409, "inUse"));
    expect(await admin.remove("hunter")).toBe(false);
    expect(admin.error.value).toBe("inUse");
  });

  it("an answer that is not what it expects is reported as unavailable, not as an empty list", async () => {
    http.get.mockResolvedValue({ nothing: true });
    const admin = create();

    await admin.load();

    expect(admin.status.value).toBe("error");
    expect(admin.error.value).toBe("unavailable");
  });
});
