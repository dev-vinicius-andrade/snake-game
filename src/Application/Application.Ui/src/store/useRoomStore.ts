import { defineStore } from "pinia";
import { IRoom } from "@/types/room";
import type { Nullable } from "@/types/nullable";
import { useNullableTypedStorage } from "@/composables/useNullableTypedStorage";
const defaultValue: IRoom = { id: undefined };
export const useRoomStore = defineStore("roomStore", {
  state: () => {
    return {
      data: useNullableTypedStorage<IRoom>("room", { defaultValue }),
    };
  },
  actions: {
    setRoomId(id?: Nullable<string>) {},
  },
});
