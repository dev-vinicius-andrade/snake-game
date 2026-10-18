import { computed } from "vue";
import { isSupportedLocale, pickLocale, SUPPORTED_LOCALES, type AppLocale } from "@/locales/locale";
import i18n from "@/plugins/i18n";

export const LOCALE_STORAGE_KEY = "snake.locale";

function readSaved(): string | null {
  try {
    return localStorage.getItem(LOCALE_STORAGE_KEY);
  } catch {
    return null;
  }
}

function save(locale: AppLocale) {
  try {
    localStorage.setItem(LOCALE_STORAGE_KEY, locale);
  } catch {
    // Private mode or blocked storage: the language just is not remembered.
  }
}

/** Puts the saved language, else the browser's, on the app. Called once at startup. */
export function applyInitialLocale() {
  const browser = typeof navigator === "undefined" ? [] : navigator.languages ?? [navigator.language];
  applyLocale(pickLocale(readSaved(), browser));
}

function applyLocale(locale: AppLocale) {
  i18n.global.locale.value = locale;
  if (typeof document !== "undefined") document.documentElement.lang = locale;
}

/** The language the app shows, and a way to change it. The choice is remembered. */
export function useLocale() {
  const locale = computed(() => i18n.global.locale.value as AppLocale);

  function setLocale(next: AppLocale) {
    if (!isSupportedLocale(next)) return;
    applyLocale(next);
    save(next);
  }

  return { locale, locales: SUPPORTED_LOCALES, setLocale };
}
