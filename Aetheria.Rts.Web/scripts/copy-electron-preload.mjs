import { copyFileSync, rmSync } from "node:fs";
import { resolve } from "node:path";

const root = resolve(import.meta.dirname, "..");
const source = resolve(root, "Electron", "preload.cjs");
const legacyOutput = resolve(root, "electron-dist", "preload.js");
const output = resolve(root, "electron-dist", "preload.cjs");

rmSync(legacyOutput, { force: true });
copyFileSync(source, output);
