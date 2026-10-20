import { describe, expect, it } from "vitest";
import { adminAccess } from "./adminAccess";

describe("adminAccess", () => {
  it("waits while it is still finding out who is signed in, so the forbidden page does not flash at an admin", () => {
    expect(adminAccess({ checking: true, signedInToAdmin: false })).toBe("checking");
  });

  it("is granted once the page is signed in to the admin api, with an admin account or a key", () => {
    expect(adminAccess({ checking: false, signedInToAdmin: true })).toBe("granted");
  });

  it("is forbidden for everybody else: a guest, a player without the role, a refused key", () => {
    expect(adminAccess({ checking: false, signedInToAdmin: false })).toBe("forbidden");
  });

  it("an admin who is already in stays in while the page is checking again", () => {
    expect(adminAccess({ checking: true, signedInToAdmin: true })).toBe("granted");
  });
});
