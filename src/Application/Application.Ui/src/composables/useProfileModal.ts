import { shallowRef } from "vue";

export type ProfileTab = "stats" | "account";

/** One profile modal for the whole page, so the game canvas and every other page open the same one. */
const open = shallowRef(false);
const tab = shallowRef<ProfileTab>("stats");

export const profileModal = { open, tab };

export function openProfile(which: ProfileTab = "stats") {
  tab.value = which;
  open.value = true;
}

export function closeProfile() {
  open.value = false;
}
