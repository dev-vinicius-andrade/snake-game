import { beforeEach, describe, expect, it, vi } from "vitest";
import { ACCOUNT_CREDENTIAL } from "./adminAuth";
import { ADMIN_KEY_STORAGE_KEY, useAdminRooms, type AdminHttp } from "./useAdminRooms";

const room = { id: "r1", code: "ABC234", name: "Friends", isPrivate: true, players: 2, maxPlayers: 4, createdAt: "2026-01-01T00:00:00Z", server: "http://a" };

class MemoryStorage {
  values = new Map<string, string>();
  getItem = (key: string) => this.values.get(key) ?? null;
  setItem = (key: string, value: string) => void this.values.set(key, value);
  removeItem = (key: string) => void this.values.delete(key);
}

let http: { get: ReturnType<typeof vi.fn>; delete: ReturnType<typeof vi.fn>; put: ReturnType<typeof vi.fn>; post: ReturnType<typeof vi.fn> };
let storage: MemoryStorage;

const create = () => useAdminRooms({ managerUrl: "http://manager.test/", http: http as unknown as AdminHttp, storage });
const failure = (status: number) => Object.assign(new Error(`HTTP ${status}`), { status });

beforeEach(() => {
  http = { get: vi.fn(async () => [room]), delete: vi.fn(async () => undefined), put: vi.fn(async () => undefined), post: vi.fn(async () => undefined) };
  storage = new MemoryStorage();
});

describe("useAdminRooms", () => {
  it("starts signed out when no key is remembered", () => {
    expect(create().status.value).toBe("signedOut");
  });

  it("signs in by loading the rooms with the key", async () => {
    const admin = create();

    await admin.signIn("secret");

    expect(http.get).toHaveBeenCalledWith("http://manager.test/admin/rooms", "secret");
    expect(admin.status.value).toBe("ready");
    expect(admin.rooms.value).toEqual([room]);
  });

  it("remembers the key for this tab only", async () => {
    const admin = create();

    await admin.signIn("secret");

    expect(storage.getItem(ADMIN_KEY_STORAGE_KEY)).toBe("secret");
  });

  it("a wrong key keeps the user signed out and forgets nothing it should not", async () => {
    http.get.mockRejectedValue(failure(401));
    const admin = create();

    await admin.signIn("wrong");

    expect(admin.status.value).toBe("signedOut");
    expect(admin.error.value).toBe("invalidKey");
    expect(storage.getItem(ADMIN_KEY_STORAGE_KEY)).toBeNull();
  });

  it("signed in but not an admin (403) is refused like a wrong key", async () => {
    http.get.mockRejectedValue(failure(403));
    const admin = create();

    await admin.signIn("secret");

    expect(admin.status.value).toBe("signedOut");
    expect(admin.error.value).toBe("invalidKey");
  });

  it("signs in as the admin account the player is signed in with, and no key is asked for", async () => {
    const admin = create();

    await admin.signInWithAccount();

    expect(http.get).toHaveBeenCalledWith("http://manager.test/admin/rooms", ACCOUNT_CREDENTIAL);
    expect(admin.status.value).toBe("ready");
    expect(storage.getItem(ADMIN_KEY_STORAGE_KEY)).toBe(ACCOUNT_CREDENTIAL);
    expect(admin.hasKey()).toBe(true);
  });

  it("when the account is no longer an admin, the next load signs the page out", async () => {
    const admin = create();
    await admin.signInWithAccount();
    http.get.mockRejectedValue(failure(403));

    await admin.refresh();

    expect(admin.status.value).toBe("signedOut");
    expect(storage.getItem(ADMIN_KEY_STORAGE_KEY)).toBeNull();
  });

  it("other failures are reported as errors and keep the key", async () => {
    http.get.mockRejectedValue(failure(500));
    const admin = create();

    await admin.signIn("secret");

    expect(admin.status.value).toBe("error");
    expect(admin.error.value).toBe("unavailable");
  });

  it("uses the remembered key to refresh", async () => {
    storage.setItem(ADMIN_KEY_STORAGE_KEY, "secret");
    const admin = create();

    await admin.refresh();

    expect(http.get).toHaveBeenCalledWith("http://manager.test/admin/rooms", "secret");
    expect(admin.status.value).toBe("ready");
  });

  it("refresh does nothing without a key", async () => {
    const admin = create();

    await admin.refresh();

    expect(http.get).not.toHaveBeenCalled();
  });

  it("signs out when the key stops being accepted", async () => {
    storage.setItem(ADMIN_KEY_STORAGE_KEY, "secret");
    const admin = create();
    await admin.refresh();
    http.get.mockRejectedValue(failure(401));

    await admin.refresh();

    expect(admin.status.value).toBe("signedOut");
    expect(storage.getItem(ADMIN_KEY_STORAGE_KEY)).toBeNull();
  });

  it("deletes a room and loads the list again", async () => {
    const admin = create();
    await admin.signIn("secret");
    http.get.mockResolvedValue([]);

    await admin.remove("r1");

    expect(http.delete).toHaveBeenCalledWith("http://manager.test/admin/rooms/r1", "secret");
    expect(admin.rooms.value).toEqual([]);
  });

  it("reports a room that could not be deleted and keeps the list", async () => {
    const admin = create();
    await admin.signIn("secret");
    http.delete.mockRejectedValue(failure(502));

    await admin.remove("r1");

    expect(admin.error.value).toBe("serverUnreachable");
    expect(admin.rooms.value).toEqual([room]);
  });

  it("sign out forgets the key and the rooms", async () => {
    const admin = create();
    await admin.signIn("secret");

    admin.signOut();

    expect(admin.status.value).toBe("signedOut");
    expect(admin.rooms.value).toEqual([]);
    expect(storage.getItem(ADMIN_KEY_STORAGE_KEY)).toBeNull();
  });
});

describe("useAdminRooms gameplay of a room", () => {
  it("sends only what the room changes, with the key, and refreshes the list", async () => {
    const admin = create();
    await admin.signIn("secret");

    const done = await admin.setGameplay("r1", { perks: "inherit", turret: "off", zone: "wild" });

    expect(done).toBe(true);
    expect(http.put).toHaveBeenCalledWith("http://manager.test/admin/rooms/r1/gameplay", "secret", { turret: "off", zone: "wild" });
    expect(http.get).toHaveBeenCalledTimes(2);
  });

  it("sends an empty body to put a room back on the default", async () => {
    const admin = create();
    await admin.signIn("secret");

    await admin.setGameplay("r1", { turret: "inherit" });

    expect(http.put).toHaveBeenCalledWith("http://manager.test/admin/rooms/r1/gameplay", "secret", {});
  });

  it("says why it could not", async () => {
    const admin = create();
    await admin.signIn("secret");

    http.put.mockRejectedValueOnce(failure(502));
    expect(await admin.setGameplay("r1", {})).toBe(false);
    expect(admin.error.value).toBe("serverUnreachable");

    http.put.mockRejectedValueOnce(failure(404));
    await admin.setGameplay("r1", {});
    expect(admin.error.value).toBe("notFound");

    http.put.mockRejectedValueOnce(failure(500));
    await admin.setGameplay("r1", {});
    expect(admin.error.value).toBe("unavailable");
  });

  it("signs out when the key is refused", async () => {
    const admin = create();
    await admin.signIn("secret");
    http.put.mockRejectedValueOnce(failure(401));

    await admin.setGameplay("r1", {});

    expect(admin.status.value).toBe("signedOut");
  });

  it("does nothing without a key", async () => {
    const admin = create();

    expect(await admin.setGameplay("r1", {})).toBe(false);
    expect(http.put).not.toHaveBeenCalled();
  });
});

describe("useAdminRooms trigger", () => {
  it("starts an event in a room with the key", async () => {
    const admin = create();
    await admin.signIn("secret");

    const result = await admin.trigger("r1", "turret");

    expect(result).toBe("started");
    expect(http.post).toHaveBeenCalledWith("http://manager.test/admin/rooms/r1/events/turret", "secret");
  });

  it.each([
    [409, "notNow"],
    [404, "notFound"],
    [502, "serverUnreachable"],
    [500, "unavailable"],
  ])("answers %i as %s", async (status, expected) => {
    const admin = create();
    await admin.signIn("secret");
    http.post.mockRejectedValueOnce(failure(status));

    expect(await admin.trigger("r1", "zone")).toBe(expected);
  });

  it("signs out when the key is refused", async () => {
    const admin = create();
    await admin.signIn("secret");
    http.post.mockRejectedValueOnce(failure(401));

    expect(await admin.trigger("r1", "zone")).toBe("invalidKey");
    expect(admin.status.value).toBe("signedOut");
  });

  it("does nothing without a key", async () => {
    expect(await create().trigger("r1", "zone")).toBe("invalidKey");
    expect(http.post).not.toHaveBeenCalled();
  });
});
