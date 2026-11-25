const NAMES = ["fill-height", "fill-width"];

/**
 * Makes an element as large as its parent. The directive runs on every update of the element, and the game view updates with every
 * snapshot (30 times a second): the names are added only when they are missing, so the class attribute does not grow. (Appending to
 * `className` made it thousands of names long within minutes, which Safari reads again on every update: the page froze as the snake grew.)
 */
export function fillParent(el: { classList: Pick<DOMTokenList, "add" | "contains"> }) {
  // `classList.add` writes the attribute again even when the names are there.
  if (NAMES.every((name) => el.classList.contains(name))) return;
  el.classList.add(...NAMES);
}
