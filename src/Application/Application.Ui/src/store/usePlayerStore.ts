import { defineStore } from "pinia";
import { IPlayer } from "@/types/player";
import type { Nullable } from "@/types/nullable";
import { useNullableTypedStorage } from "@/composables/useNullableTypedStorage";
const defaultValue: IPlayer = { id: undefined, nickname: undefined };
export const usePlayerStore = defineStore("playerStore", {
  state: () => {
    return {
      data: useNullableTypedStorage<IPlayer>("player", { defaultValue }),
    };
  },
  actions: {
    setNickname(nickname?: Nullable<string>) {
      if (!this.data) this.data = defaultValue;
      this.data.nickname = nickname;
    },
  },
});
