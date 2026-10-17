import { beforeEach, describe, expect, it, vi } from "vitest";
import { ADMIN_KEY_STORAGE_KEY, useAdminRooms, type AdminHttp } from "./useAdminRooms";

const room = { id: "r1", code: "ABC234", name: "Friends", isPrivate: true, players: 2, maxPlayers: 4, createdAt: "2026-01-01T00:00:00Z", server: "http://a" };

class MemoryStorage {
  values = new Map<string, string>();
  getItem = (key: string) => this.values.get(key) ?? null;
  setItem = (key: string, value: string) => void this.values.set(key, value);
  removeItem = (key: string) => void this.values.delete(key);
}

let http: { get: ReturnType<typeof vi.fn>; delete: ReturnType<typeof vi.fn> };
let storage: MemoryStorage;

const create = () => useAdminRooms({ managerUrl: "http://manager.test/", http: http as unknown as AdminHttp, storage });
const failure = (status: number) => Object.assign(new Error(`HTTP ${status}`), { status });

beforeEach(() => {
  http = { get: vi.fn(async () => [room]), delete: vi.fn(async () => undefined) };
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
