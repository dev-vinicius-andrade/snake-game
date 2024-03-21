import { EventEmitter, Injectable, OnInit, signal } from '@angular/core';
import { LocalStorageService } from '@app/services/localStorage/local-storage.service';
import { LocalStorageKey, Theme } from '@app/types/enums';

@Injectable({
  providedIn: 'root',
})
export class ThemeService {
  private theme = signal<Theme>(Theme.Dark);
  private themeChanged = new EventEmitter<Theme>();
  private localStorageService: LocalStorageService;
  public get current(): Theme {
    return this.theme();
  }
  constructor(localStorageService: LocalStorageService) {
    this.localStorageService = localStorageService;
    const theme = localStorageService.get(LocalStorageKey.theme);
    if (!theme) this.setThemeBasedOnUsersSystemPreference();
    else this.switchTheme(theme as Theme);
    this.themeChanged.subscribe((theme) => {
      this.saveThemePreference();
    });
    this.saveThemePreference();
  }
  private saveThemePreference() {
    this.localStorageService.set(LocalStorageKey.theme, this.current);
  }
  async setThemeBasedOnUsersSystemPreference() {
    const userPrefersDark = window.matchMedia('(prefers-color-scheme: dark)');
    this.switchTheme(userPrefersDark.matches ? Theme.Dark : Theme.Light);
  }

  switchTheme(theme: Theme) {
    if (this.theme() === theme) return;
    this.theme.set(theme);
  }
  isDarkMode() {
    return this.theme() === Theme.Dark;
  }
  isLightMode() {
    return this.theme() === Theme.Light;
  }
  class() {
    return ['bg-app-background', 'text-app-text'];
  }
  toogle() {
    if (this.isDarkMode()) this.setLightMode();
    else this.setDarkMode();
    this.themeChanged.emit(this.theme());
  }
  subscribe(callback: (theme: Theme) => Theme) {
    this.themeChanged.subscribe(callback);
  }
  setDarkMode() {
    this.theme.set(Theme.Dark);
  }
  setLightMode() {
    this.theme.set(Theme.Light);
  }
}
