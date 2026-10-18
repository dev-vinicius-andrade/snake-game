import { beforeEach, describe, expect, it, vi } from "vitest";
import i18n from "@/plugins/i18n";
import { LOCALE_STORAGE_KEY, useLocale } from "./useLocale";

describe("useLocale", () => {
  beforeEach(() => {
    const store = new Map<string, string>();
    vi.stubGlobal("localStorage", {
      getItem: (key: string) => store.get(key) ?? null,
      setItem: (key: string, value: string) => void store.set(key, value),
    });
    vi.stubGlobal("document", { documentElement: { lang: "" } });
    i18n.global.locale.value = "en";
  });

  it("starts with the language the app is showing", () => {
    expect(useLocale().locale.value).toBe("en");
  });

  it("switches the app to another language and remembers it", () => {
    const { locale, setLocale } = useLocale();

    setLocale("pt");

    expect(locale.value).toBe("pt");
    expect(i18n.global.locale.value).toBe("pt");
    expect(localStorage.getItem(LOCALE_STORAGE_KEY)).toBe("pt");
  });

  it("tells the page which language it is in", () => {
    useLocale().setLocale("pt");

    expect(document.documentElement.lang).toBe("pt");
  });

  it("translates after switching", () => {
    useLocale().setLocale("pt");

    expect(i18n.global.t("buttons.joinRandomRoom")).toBe("Jogar agora");
  });

  it("ignores a language it has no text for", () => {
    useLocale().setLocale("fr" as never);

    expect(i18n.global.locale.value).toBe("en");
    expect(localStorage.getItem(LOCALE_STORAGE_KEY)).toBeNull();
  });
});
