import { spawnSync } from "node:child_process";

const source =
  process.env.OPENAPI_URL ?? "http://localhost:5291/openapi/v1.json";
const result = spawnSync(
  process.execPath,
  [
    "node_modules/openapi-typescript/bin/cli.js",
    source,
    "-o",
    "src/lib/api/schema.ts",
  ],
  {
    stdio: "inherit",
  },
);

if (result.status !== 0) {
  process.exit(result.status ?? 1);
}
