import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root',
})
export class UniqueIdService {
  constructor() {}
  generateUserId(): string {
    const navigatorInfo = window.navigator;
    const screenInfo = window.screen;
    let uid = navigatorInfo.mimeTypes.length.toString();
    uid += navigatorInfo.userAgent.replace(/\D+/g, '');
    uid += navigatorInfo.plugins.length.toString();
    uid += screenInfo.height ?? '';
    uid += screenInfo.width ?? '';
    uid += screenInfo.pixelDepth ?? '';

    return this.hashString(uid);
  }

  private hashString(str: string): string {
    let hash = 0,
      i,
      chr;
    if (str.length === 0) return hash.toString();
    for (i = 0; i < str.length; i++) {
      chr = str.charCodeAt(i);
      hash = (hash << 5) - hash + chr;
      hash |= 0; // Convert to 32bit integer
    }
    return hash.toString();
  }
}
