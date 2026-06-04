const fs = require("fs");
const path = require("path");

const root = path.resolve(__dirname, "..");
const html = fs.readFileSync(path.join(root, "src", "dashboard", "index.html"), "utf8");
const program = fs.readFileSync(path.join(root, "src", "ColetorProfitRTD", "Program.cs"), "utf8");
const server = fs.readFileSync(path.join(root, "src", "ColetorProfitRTD", "Web", "LocalWebServer.cs"), "utf8");

const required = [
  ["bootstrap endpoint route", /IsPath\(path,\s*"\/bootstrap"\)/],
  ["bootstrap factory", /\bBuildBootstrap\b/],
  ["bootstrap health", /\["health"\]\s*=\s*BuildHealth/],
  ["bootstrap assets", /\["assets"\]\s*=\s*BuildAssets/],
  ["bootstrap snapshot", /\["snapshot"\]\s*=/],
  ["bootstrap flow", /\["flow"\]\s*=/],
  ["bootstrap signals", /\["signals"\]\s*=/],
  ["frontend loadBootstrap", /\basync\s+function\s+loadBootstrap\b/],
  ["frontend applyBootstrapMessage", /\bfunction\s+applyBootstrapMessage\b/],
  ["frontend bootstrap fetch", /fetch\("\/bootstrap"\)/],
  ["frontend fallback endpoints", /fetch\("\/flow"\)[\s\S]*fetch\("\/signals"\)[\s\S]*loadAssets\(\)/],
];

const source = `${server}\n${program}\n${html}`;
const failures = required
  .filter(([, pattern]) => !pattern.test(source))
  .map(([label]) => `Missing bootstrap loading requirement: ${label}`);

if (failures.length) {
  console.error(failures.join("\n"));
  process.exit(1);
}

console.log("Bootstrap loading OK");
