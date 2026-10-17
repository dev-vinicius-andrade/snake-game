import { describe, expect, it } from "vitest";
import { INVITE_CODE_LENGTH, inviteLink, isValidInviteCode, normalizeInviteCode } from "./invite";

describe("normalizeInviteCode", () => {
  it.each([
    ["abc234", "ABC234"],
    ["  abc 234 ", "ABC234"],
    ["ab-c2-34", "ABC234"],
    ["", ""],
  ])("turns %j into %j", (input, expected) => {
    expect(normalizeInviteCode(input)).toBe(expected);
  });

  it("accepts a whole invite link and takes the code from it", () => {
    expect(normalizeInviteCode("https://snake.test/join/abc234")).toBe("ABC234");
    expect(normalizeInviteCode("snake.test/join/abc234/")).toBe("ABC234");
  });
});

describe("isValidInviteCode", () => {
  it("accepts the right length of letters and digits without look alikes", () => {
    expect(isValidInviteCode("ABC234")).toBe(true);
    expect(INVITE_CODE_LENGTH).toBe(6);
  });

  it.each(["", "ABC23", "ABC2345", "ABC23O", "ABC231", "abc234", "ABC 23"])("rejects %j", (code) => {
    expect(isValidInviteCode(code)).toBe(false);
  });
});

describe("inviteLink", () => {
  it("builds the join page address", () => {
    expect(inviteLink("https://snake.test", "ABC234")).toBe("https://snake.test/join/ABC234");
  });

  it("ignores a trailing slash on the origin", () => {
    expect(inviteLink("https://snake.test/", "ABC234")).toBe("https://snake.test/join/ABC234");
  });
});
