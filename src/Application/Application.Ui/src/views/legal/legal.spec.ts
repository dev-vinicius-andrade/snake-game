import { describe, expect, it } from "vitest";
import en from "@/locales/messages/en";
import pt from "@/locales/messages/pt";
import { ISSUES_URL, REPOSITORY_URL } from "@/config/links";

/** The sections the page shows: s1..sN for each of the two pages (kept in step with SECTIONS in index.vue). */
const SECTIONS = { termsPage: 7, privacyPage: 9 } as const;

describe.each([
  ["en", en],
  ["pt", pt],
])("the legal pages (%s)", (_, messages) => {
  it.each(Object.entries(SECTIONS))("%s has a title and text for each of its %i sections", (page, count) => {
    const sections = (messages.legal as unknown as Record<string, Record<string, { title: string; body: string }>>)[page];
    for (let index = 1; index <= count; index++) {
      expect(sections[`s${index}`]?.title, `${page}.s${index}.title`).toBeTruthy();
      expect(sections[`s${index}`]?.body, `${page}.s${index}.body`).toBeTruthy();
    }
    expect(Object.keys(sections)).toHaveLength(count);
  });

  it("says what is kept: a hash of the login identifier and a name, nothing else", () => {
    expect(messages.legal.privacyPage.s2.body).toMatch(/hash/i);
    expect(messages.legal.privacyPage.s3.body).toMatch(/email/i);
  });

  it("says the camera picture does not leave the device", () => {
    expect(messages.legal.privacyPage.s4.body).toMatch(/camera|câmera/i);
  });

  it("tells where to ask for help (the issues address is given in)", () => {
    expect(messages.legal.termsPage.s7.body).toContain("{issues}");
    expect(messages.legal.privacyPage.s8.body).toContain("{issues}");
  });
});

describe("links", () => {
  it("the issues address is under the repository", () => {
    expect(ISSUES_URL).toBe(`${REPOSITORY_URL}/issues`);
  });
});
