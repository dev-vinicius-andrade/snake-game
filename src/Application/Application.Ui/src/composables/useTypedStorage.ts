import {
  RemovableRef,
  StorageSerializers,
  useStorage,
  UseStorageOptions,
} from "@vueuse/core";
import { Nullable } from "@/types/nullable";

export declare type TypedStorageOptions<T> = {
  storage?: Storage;
  options?: Nullable<UseStorageOptions<T | any>>;
};
export function useTypedStorage<T>(
  key: string,
  defaultValue: T,
  payload: TypedStorageOptions<T> = {
    storage: localStorage,
    options: null,
  }
): RemovableRef<Nullable<T>> {
  const defaultOptions = {
    serializer: StorageSerializers.object,
  } as UseStorageOptions<T | any>;
  const mergedOptions = payload?.options
    ? { ...defaultOptions, ...payload.options }
    : defaultOptions;
  return useStorage(key, defaultValue, localStorage, mergedOptions);
}
