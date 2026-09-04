import { createHash } from "node:crypto";
import { readdir, readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { build } from "esbuild";

const shelfRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const sourceRoot = path.join(shelfRoot, "src");
const entryPoint = path.join(sourceRoot, "main.ts");
const lockfilePath = path.join(shelfRoot, "package-lock.json");
const outputPath = path.resolve(shelfRoot, "../OgmaLibrary.Bookshelf3D/Assets/Web/shelf3d.js");
const manifestPath = path.resolve(shelfRoot, "../OgmaLibrary.Bookshelf3D/Assets/Web/shelf3d.build.json");

async function listTypeScriptFiles(directory) {
  const entries = await readdir(directory, { withFileTypes: true });
  const files = [];
  for (const entry of entries.sort((left, right) => left.name.localeCompare(right.name))) {
    const entryPath = path.join(directory, entry.name);
    if (entry.isDirectory()) {
      files.push(...await listTypeScriptFiles(entryPath));
    } else if (entry.isFile() && entry.name.endsWith(".ts")) {
      files.push(entryPath);
    }
  }
  return files;
}

async function hashFiles(files) {
  const hash = createHash("sha256");
  for (const file of files) {
    const relativePath = path.relative(shelfRoot, file).split(path.sep).join("/");
    hash.update(relativePath);
    hash.update("\0");
    hash.update(await readFile(file));
    hash.update("\0");
  }
  return hash.digest("hex");
}

async function hashFile(file) {
  return createHash("sha256").update(await readFile(file)).digest("hex");
}

const sourceFiles = await listTypeScriptFiles(sourceRoot);
await build({
  entryPoints: [entryPoint],
  bundle: true,
  format: "iife",
  globalName: "OgmaShelf3D",
  target: "es2022",
  outfile: outputPath,
});

const manifest = {
  schema: "ogma-shelf3d-build-v1",
  entryPoint: "src/main.ts",
  sourceFiles: sourceFiles.map((file) => path.relative(shelfRoot, file).split(path.sep).join("/")),
  sourceSha256: await hashFiles(sourceFiles),
  lockfileSha256: await hashFile(lockfilePath),
  bundleSha256: await hashFile(outputPath),
};

await writeFile(manifestPath, `${JSON.stringify(manifest, null, 2)}\n`, "utf8");
console.log(`Built ${path.relative(shelfRoot, outputPath)} and wrote ${path.relative(shelfRoot, manifestPath)}`);
