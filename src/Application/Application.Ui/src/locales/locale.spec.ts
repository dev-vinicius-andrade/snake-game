import { describe, expect, it } from "vitest";
import en from "./messages/en";
import pt from "./messages/pt";
import { DEFAULT_LOCALE, SUPPORTED_LOCALES, isSupportedLocale, pickLocale } from "./locale";

type Tree = { [key: string]: string | Tree };

function leaves(tree: Tree, prefix = ""): Map<string, string> {
  const found = new Map<string, string>();
  for (const [key, value] of Object.entries(tree)) {
    if (typeof value === "string") found.set(`${prefix}${key}`, value);
    else leaves(value, `${prefix}${key}.`).forEach((text, path) => found.set(path, text));
  }
  return found;
}

const placeholders = (text: string) => [...text.matchAll(/\{(\w+)\}/g)].map((match) => match[1]).sort();

describe("supported locales", () => {
  it("are English and Portuguese, English first", () => {
    expect(SUPPORTED_LOCALES).toEqual(["en", "pt"]);
    expect(DEFAULT_LOCALE).toBe("en");
  });

  it("recognises only those", () => {
    expect(isSupportedLocale("pt")).toBe(true);
    expect(isSupportedLocale("fr")).toBe(false);
    expect(isSupportedLocale(null)).toBe(false);
    expect(isSupportedLocale(undefined)).toBe(false);
  });
});

describe("pickLocale", () => {
  it("prefers what the player chose before", () => {
    expect(pickLocale("pt", ["en-US"])).toBe("pt");
    expect(pickLocale("en", ["pt-BR"])).toBe("en");
  });

  it("falls back to the browser language, by its first part", () => {
    expect(pickLocale(null, ["pt-BR", "en"])).toBe("pt");
    expect(pickLocale(null, ["PT"])).toBe("pt");
  });

  it("skips browser languages it has no text for", () => {
    expect(pickLocale(null, ["fr-FR", "pt-PT"])).toBe("pt");
  });

  it("ignores a saved language it does not know", () => {
    expect(pickLocale("klingon", ["pt-BR"])).toBe("pt");
  });

  it("uses English when nothing matches", () => {
    expect(pickLocale(null, ["fr", "de"])).toBe("en");
    expect(pickLocale(null, [])).toBe("en");
    expect(pickLocale(undefined, undefined)).toBe("en");
  });
});

describe("Portuguese messages", () => {
  const english = leaves(en as unknown as Tree);
  const portuguese = leaves(pt as unknown as Tree);

  it("have every key English has, and no others", () => {
    expect([...portuguese.keys()].sort()).toEqual([...english.keys()].sort());
  });

  it("keep the placeholders of the English text", () => {
    for (const [path, text] of english) expect(placeholders(portuguese.get(path) ?? ""), path).toEqual(placeholders(text));
  });

  it("are not just a copy of the English text", () => {
    const untranslated = [...english].filter(([path, text]) => portuguese.get(path) === text && /[a-z]{4,}/i.test(text));
    expect(untranslated.length / english.size).toBeLessThan(0.2);
  });
});
