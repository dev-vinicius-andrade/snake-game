export const SUPPORTED_LOCALES = ["en", "pt"] as const;
export type AppLocale = (typeof SUPPORTED_LOCALES)[number];
export const DEFAULT_LOCALE: AppLocale = "en";

export const isSupportedLocale = (value: string | null | undefined): value is AppLocale =>
  SUPPORTED_LOCALES.some((locale) => locale === value);

const baseOf = (language: string) => language.toLowerCase().split("-")[0];

/** What the player chose before, else the first browser language there is text for, else English. */
export function pickLocale(saved: string | null | undefined, browserLanguages: readonly string[] = []): AppLocale {
  if (isSupportedLocale(saved)) return saved;
  const match = browserLanguages.map(baseOf).find(isSupportedLocale);
  return match ?? DEFAULT_LOCALE;
}
