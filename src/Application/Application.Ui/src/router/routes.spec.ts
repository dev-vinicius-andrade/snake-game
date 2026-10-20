import { createMemoryHistory, createRouter } from "vue-router";
import { describe, expect, it } from "vitest";
import { useImportRoutes } from "@/composables/useImportRoutes";

const routes = useImportRoutes(import.meta.glob("@/router/routes/**/*.*s", { eager: true }));
const router = createRouter({ history: createMemoryHistory(), routes });

const nameOf = (path: string) => router.resolve(path).name;

describe("the routes of the site", () => {
  it("opens the home page at /, whatever the other routes are", () => {
    expect(nameOf("/")).toBe("home");
  });

  it.each([
    ["/about", "about"],
    ["/admin", "admin"],
    ["/account", "account"],
    ["/account/profile", "profile"],
    ["/account/oauth", "account-oauth"],
    ["/credits", "credits"],
    ["/terms", "terms"],
    ["/privacy", "privacy"],
    ["/join/ABC234", "join"],
  ])("opens %s as %s", (path, name) => {
    expect(nameOf(path)).toBe(name);
  });

  it("has no route of its own at / besides the home page: one that is only a layout with children would be matched first and show a blank page", () => {
    const atRoot = routes.filter((route) => route.path === "/");
    expect(atRoot.map((route) => route.name ?? route.children?.[0]?.name)).toEqual(["home"]);
  });
});
