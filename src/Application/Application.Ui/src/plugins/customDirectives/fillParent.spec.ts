import { describe, expect, it } from "vitest";
import { fillParent } from "./fillParent";

/** As much of an element as the directive touches: a class attribute and its list of names. */
function element(className = "") {
  const fake = {
    className,
    writes: 0,
    classList: {
      contains: (name: string) => fake.className.split(" ").includes(name),
      add(...names: string[]) {
        fake.writes++;
        const present = fake.className.split(" ").filter(Boolean);
        for (const name of names) if (!present.includes(name)) present.push(name);
        fake.className = present.join(" ");
      },
    },
  };
  return fake;
}

describe("fillParent", () => {
  it("adds the classes that fill the parent, keeping the element's own", () => {
    const el = element("snake-view");

    fillParent(el);

    expect(el.className).toBe("snake-view fill-height fill-width");
  });

  it("adds them once, however often the element is updated", () => {
    const el = element("snake-view");

    for (let update = 0; update < 100; update++) fillParent(el);

    expect(el.className).toBe("snake-view fill-height fill-width");
  });

  it("does not write to the element when nothing is missing, since a write is a change the browser has to look at", () => {
    const el = element("snake-view");
    fillParent(el);

    for (let update = 0; update < 100; update++) fillParent(el);

    expect(el.writes).toBe(1);
  });

  it("puts them back when a render took them away", () => {
    const el = element("snake-view");
    fillParent(el);
    el.className = "snake-view";

    fillParent(el);

    expect(el.className).toBe("snake-view fill-height fill-width");
  });
});
