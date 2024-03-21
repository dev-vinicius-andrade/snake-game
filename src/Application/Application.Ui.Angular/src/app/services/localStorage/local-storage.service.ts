import { Injectable } from '@angular/core';
import { LocalStorageKey } from '@app/types/enums';

@Injectable({
  providedIn: 'root',
})
export class LocalStorageService {
  constructor() {}
  set(key: LocalStorageKey, value: string) {
    localStorage.setItem(key.toString(), value);
  }
  get(key: LocalStorageKey) {
    return localStorage.getItem(key.toString());
  }
  remove(key: LocalStorageKey) {
    localStorage.removeItem(key.toString());
  }
  exists(key: LocalStorageKey) {
    return localStorage.getItem(key.toString()) !== null;
  }
}
