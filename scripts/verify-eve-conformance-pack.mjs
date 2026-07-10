import assert from "node:assert/strict";
import fs from "node:fs";
import path from "node:path";

const root = path.resolve(import.meta.dirname, "..");
const pack = read("conformance/eve/provider-pack.json");
assert.equal(pack.schema, "gamecult.eve.provider_conformance_pack.v1");
assert.equal(pack.providerId, "aetheria");
assert.equal(pack.ownerRepo, "Aetheria");

const advertisement = read(pack.advertisementPath);
assert.equal(advertisement.providerId, pack.providerId);
const starbridgeSurfaces = advertisement.surfaces.filter((surface) =>
  surface.surfaceId === "aetheria.starbridge.commander" ||
  surface.surfaceId === "aetheria.starbridge.pilot");
assert.equal(starbridgeSurfaces.length, 2);
assert.deepEqual(new Set(starbridgeSurfaces.map((surface) => surface.audience)), new Set(["commander", "pilot"]));
for (const surface of starbridgeSurfaces) {
  assert.equal(surface.worldInteraction?.projectionKind, "provider-authored-world-surface");
  assert.equal(surface.worldInteraction?.commandBoundary, "aetheria.daemon.commands");
  assert.ok(surface.worldInteraction?.loweringTargets.includes("electron-shell"));
  assert.ok(surface.worldInteraction?.loweringTargets.includes("unity-scene"));
}
assert.ok(advertisement.surfaces.some((surface) => surface.surfaceId === "aetheria.daemon.editor" && surface.worldInteraction?.projectionKind === "provider-authored-world-editor-surface"));

for (const fixture of pack.fixtures) {
  const surface = read(fixture.surfacePath);
  const metadata = read(fixture.metadataPath);
  assert.equal(surface.providerId, pack.providerId);
  assert.equal(metadata.fixtureId, fixture.fixtureId);
  assert.equal(metadata.surface.path, fixture.surfacePath);
}

for (const scenarioPath of pack.scenarios) {
  const scenario = read(scenarioPath);
  assert.equal(scenario.providerId, pack.providerId);
  assert.equal(scenario.advertisementPath, pack.advertisementPath);
}

for (const assetPath of pack.assets) read(assetPath);
console.log(`Aetheria Eve provider pack passed: ${path.join(root, "conformance/eve/provider-pack.json")}`);

function read(relativePath) {
  const absolutePath = path.join(root, relativePath);
  assert.ok(fs.existsSync(absolutePath), `Missing provider-pack path: ${relativePath}`);
  return JSON.parse(fs.readFileSync(absolutePath, "utf8"));
}
