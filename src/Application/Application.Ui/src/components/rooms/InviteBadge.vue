<script setup lang="ts">
import { ref } from "vue";
import { getText } from "@/helpers";
import { inviteLink } from "@/game/invite";

/** Shows the room code of a private room and copies the invite link. */
const props = defineProps<{ code: string }>();

const copied = ref(false);

async function copy() {
  try {
    await navigator.clipboard.writeText(inviteLink(window.location.origin, props.code));
    copied.value = true;
    setTimeout(() => (copied.value = false), 2000);
  } catch {
    // Without clipboard access the player can still read the code out.
  }
}
</script>

<template>
  <section class="invite">
    <div>{{ getText({ key: "rooms.inviteCode" }) }}: <strong>{{ code }}</strong></div>
    <button type="button" class="invite-copy" @click="copy">
      {{ getText({ key: copied ? "rooms.copied" : "rooms.copyLink" }) }}
    </button>
  </section>
</template>

<style scoped>
.invite {
  position: fixed;
  z-index: 10;
  top: 76px;
  left: 8px;
  padding: 8px 12px;
  color: #ffffff;
  font-family: monospace;
  font-size: 14px;
  background: rgba(0, 0, 0, 0.45);
}
.invite-copy {
  margin-top: 6px;
  padding: 2px 8px;
  color: inherit;
  font: inherit;
  font-size: 12px;
  background: rgba(255, 255, 255, 0.15);
  border: 1px solid rgba(255, 255, 255, 0.4);
  border-radius: 2px;
  cursor: pointer;
}
</style>
