const fs = require("fs");
const path = require("path");

const root = path.resolve(__dirname, "..");
const dashboard = fs.readFileSync(path.join(root, "src", "dashboard", "index.html"), "utf8");
const program = fs.readFileSync(path.join(root, "src", "ColetorProfitRTD", "Program.cs"), "utf8");

const requiredDashboard = [
  ["top feed metric", /id="top-feed"/],
  ["top feed tile", /id="top-feed-tile"/],
  ["timestamp age helper", /\bfunction\s+timestampAgeMs\b/],
  ["feed freshness helper", /\bfunction\s+feedFreshness\b/],
  ["live label", /\bAo vivo\b/],
  ["stale labels", /\bAtrasado\b[\s\S]*\bParado\b/],
  ["backend age UI", /\bIdade backend\b/],
  ["selected feed UI", /\bFeed selecionado\b/],
  ["health triggers status render", /\bscheduleLiveRender\(null,\s*false,\s*"status"/],
];

const requiredProgram = [
  ["health age calculation", /\blastUpdateAgeMs\b/],
  ["health age payload", /\["lastUpdateAgeMs"\]/],
];

const failures = [
  ...requiredDashboard
    .filter(([, pattern]) => !pattern.test(dashboard))
    .map(([label]) => `Missing feed freshness dashboard requirement: ${label}`),
  ...requiredProgram
    .filter(([, pattern]) => !pattern.test(program))
    .map(([label]) => `Missing feed freshness backend requirement: ${label}`),
];

if (failures.length) {
  console.error(failures.join("\n"));
  process.exit(1);
}

console.log("Feed freshness OK");
