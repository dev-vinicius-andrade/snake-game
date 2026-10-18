/** A perk id such as `twin-shot` as words to show: `Twin shot`. */
export function perkLabel(id: string): string {
  const words = id.trim().replace(/[-_]+/g, " ").replace(/\s+/g, " ");
  return words.charAt(0).toUpperCase() + words.slice(1);
}
