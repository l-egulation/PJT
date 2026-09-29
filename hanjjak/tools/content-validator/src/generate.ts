import { resolve } from "node:path";
import { writeProgressionPackages } from "./buildProgressionRebalance.js";

await writeProgressionPackages(resolve(import.meta.dirname, "../../../packages/game-content"));
console.log("wrote progression-rebalance packages");
