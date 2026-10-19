// Writes src/game/gameIcons.json: the bodies of the game-icons.net icons the game uses (src/game/gameIconNames.json), so the pack
// itself is a build-time tool and nothing of it ships but these few icons. Run it with `npm run icons` after changing the list.
import { readFileSync, writeFileSync } from "node:fs";
import { createRequire } from "node:module";

const require = createRequire(import.meta.url);
const pack = require("@iconify-json/game-icons/icons.json");
const names = JSON.parse(readFileSync(new URL("../src/game/gameIconNames.json", import.meta.url), "utf8"));

const missing = names.filter((name) => !pack.icons[name]);
if (missing.length > 0) {
  console.error(`Not in the pack: ${missing.join(", ")}`);
  process.exit(1);
}
if (new Set(names).size !== names.length) {
  console.error("A name is listed twice.");
  process.exit(1);
}

const icons = Object.fromEntries([...names].sort().map((name) => [name, pack.icons[name].body]));
const out = { width: pack.width ?? 512, height: pack.height ?? 512, icons };
writeFileSync(new URL("../src/game/gameIcons.json", import.meta.url), JSON.stringify(out, null, 1) + "\n");
console.log(`${names.length} icons written.`);
