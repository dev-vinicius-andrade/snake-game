import { describe, expect, it } from "vitest";
import en from "../locales/messages/en";
import pt from "../locales/messages/pt";
import { SCRIPT_TEMPLATES } from "./scriptTemplates";

const HOOKS = ["modify", "onEat", "onKill", "onTick"];

describe("SCRIPT_TEMPLATES", () => {
  it("has a different id for each", () => {
    const ids = SCRIPT_TEMPLATES.map((template) => template.id);

    expect(new Set(ids).size).toBe(ids.length);
  });

  it.each(SCRIPT_TEMPLATES.map((template) => [template.id, template.code]))("%s is valid JavaScript that defines a hook", (_id, code) => {
    expect(() => new Function(code)).not.toThrow();
    expect(HOOKS.some((hook) => new RegExp(`function\\s+${hook}\\b`).test(code))).toBe(true);
  });

  it.each(SCRIPT_TEMPLATES.map((template) => [template.id]))("%s has a name and a sentence in both languages", (id) => {
    for (const messages of [en, pt]) {
      const text = (messages as any).admin.gameplay.scriptTemplates[id];
      expect(text?.name).toBeTruthy();
      expect(text?.hint).toBeTruthy();
    }
  });

  it("fits what the server accepts: at most 20000 characters each", () => {
    for (const template of SCRIPT_TEMPLATES) expect(template.code.length).toBeLessThan(20_000);
  });
});
