// Guards the component conventions:
//  - every component file in src/components is listed in ../Components.md (repo root)
//  - no two component files share a name (case-insensitive)
//  - no Tailwind arbitrary values (e.g. w-[13px], text-[#fff]) in any .tsx file
import { readdirSync, readFileSync } from "node:fs";
import { basename, join } from "node:path";

const root = new URL("..", import.meta.url).pathname;
const registry = readFileSync(join(root, "../../Components.md"), "utf8");

function walk(dir: string): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const path = join(dir, entry.name);
    return entry.isDirectory() ? walk(path) : [path];
  });
}

const errors: string[] = [];
const sources = walk(join(root, "src")).filter(
  (f) => f.endsWith(".tsx") && !f.endsWith(".test.tsx"),
);

const components = sources.filter((f) => f.includes("/src/components/"));
const seen = new Map<string, string>();
for (const file of components) {
  const name = basename(file, ".tsx");
  const key = name.toLowerCase();
  const previous = seen.get(key);
  if (previous) errors.push(`Duplicate component name "${name}": ${previous} and ${file}`);
  seen.set(key, file);
  if (!registry.includes(`\`${name}\``))
    errors.push(`Component "${name}" is missing from Components.md`);
}

const arbitrary = /(?:^|[\s"'`:])(?:[a-z-]+:)*-?[a-z]+(?:-[a-z]+)*-\[[^\]]+\]/;
for (const file of sources) {
  readFileSync(file, "utf8")
    .split("\n")
    .forEach((line, index) => {
      if (/class(Name)?=|`|"/.test(line) && arbitrary.test(line)) {
        errors.push(`${file}:${index + 1} arbitrary Tailwind value: ${line.trim()}`);
      }
    });
}

if (errors.length > 0) {
  console.error(errors.join("\n"));
  process.exit(1);
}
console.log(`check-components: ${components.length} components ok`);
