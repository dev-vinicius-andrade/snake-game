import { describe, expect, it } from "vitest";
import { ACCOUNT_CREDENTIAL, adminHeaders, isRejected } from "./adminAuth";

describe("adminHeaders", () => {
  it("sends an admin key as the admin key header", () => {
    expect(adminHeaders("the-key", "device-token")).toEqual({ "X-Admin-Key": "the-key" });
  });

  it("signed in as an admin, sends the device token as a bearer token and no key", () => {
    expect(adminHeaders(ACCOUNT_CREDENTIAL, "device-token")).toEqual({ Authorization: "Bearer device-token" });
  });

  it("signed in but with no token left, sends nothing, so the server says no", () => {
    expect(adminHeaders(ACCOUNT_CREDENTIAL, null)).toEqual({});
  });
});

describe("isRejected", () => {
  it("is true for not signed in (401) and for signed in but not an admin (403), and for nothing else", () => {
    expect(isRejected(401)).toBe(true);
    expect(isRejected(403)).toBe(true);
    for (const status of [undefined, 200, 404, 409, 500, 502]) expect(isRejected(status)).toBe(false);
  });
});
