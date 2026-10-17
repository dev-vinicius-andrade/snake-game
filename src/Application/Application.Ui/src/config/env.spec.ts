import { describe, expect, it } from "vitest";
import { DEFAULT_MANAGER_URL, resolveManagerUrl } from "./env";

describe("resolveManagerUrl", () => {
  it("uses VITE_MANAGER_URL when set", () => {
    expect(resolveManagerUrl({ VITE_MANAGER_URL: "https://manager.example.com" })).toBe(
      "https://manager.example.com"
    );
  });

  it("drops trailing slashes", () => {
    expect(resolveManagerUrl({ VITE_MANAGER_URL: "https://manager.example.com//" })).toBe(
      "https://manager.example.com"
    );
  });

  it("trims whitespace", () => {
    expect(resolveManagerUrl({ VITE_MANAGER_URL: "  http://localhost:1  " })).toBe("http://localhost:1");
  });

  it.each([undefined, "", "   "])("falls back to the default for %j", (value) => {
    expect(resolveManagerUrl({ VITE_MANAGER_URL: value })).toBe(DEFAULT_MANAGER_URL);
  });
});
