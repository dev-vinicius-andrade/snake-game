import { beforeEach, describe, expect, it } from "vitest";
import { closeProfile, openProfile, profileModal } from "./useProfileModal";

beforeEach(() => closeProfile());

describe("the profile modal", () => {
  it("starts closed", () => {
    expect(profileModal.open.value).toBe(false);
  });

  it("opens on the stats, or on the tab asked for, and closes", () => {
    openProfile();
    expect(profileModal.open.value).toBe(true);
    expect(profileModal.tab.value).toBe("stats");

    openProfile("account");
    expect(profileModal.tab.value).toBe("account");

    closeProfile();
    expect(profileModal.open.value).toBe(false);
  });

  it("is one modal for the whole page: every caller sees the same state", () => {
    openProfile("account");

    expect(profileModal.open.value).toBe(true);
    expect(profileModal.tab.value).toBe("account");
  });
});
