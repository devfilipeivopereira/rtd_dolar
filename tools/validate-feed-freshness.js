const fs = require("fs");
const path = require("path");

const root = path.resolve(__dirname, "..");
const dashboard = fs.readFileSync(path.join(root, "src", "dashboard", "index.html"), "utf8");
const program = fs.readFileSync(path.join(root, "src", "ColetorProfitRTD", "Program.cs"), "utf8");
const rtdClient = fs.readFileSync(path.join(root, "src", "ColetorProfitRTD", "Rtd", "RtdClient.cs"), "utf8");
const marketState = fs.readFileSync(path.join(root, "src", "ColetorProfitRTD", "MarketData", "MarketState.cs"), "utf8");

const requiredDashboard = [
  ["top feed metric", /id="top-feed"/],
  ["top feed tile", /id="top-feed-tile"/],
  ["timestamp age helper", /\bfunction\s+timestampAgeMs\b/],
  ["feed freshness helper", /\bfunction\s+feedFreshness\b/],
  ["live label", /\bAo vivo\b/],
  ["stale labels", /\bAtrasado\b[\s\S]*\bParado\b/],
  ["backend age UI", /\bIdade backend\b/],
  ["selected feed UI", /\bFeed selecionado\b/],
  ["asset feed helper", /\bfunction\s+assetFeedFreshness\b/],
  ["quotes feed column", /<th>Feed<\/th>[\s\S]*renderFeedBadge\(feed\)/],
  ["health triggers status render", /\bscheduleLiveRender\(null,\s*false,\s*"status"/],
];

const requiredProgram = [
  ["health age calculation", /\blastUpdateAgeMs\b/],
  ["health age payload", /\["lastUpdateAgeMs"\]/],
];

const requiredRtdClient = [
  ["asset last update age", /\["lastUpdateAgeMs"\]/],
  ["asset feed status", /\["feedStatus"\]/],
  ["exact snapshot lookup", /\b_state\.Find\(asset\)/],
];

const markStatusMatch = marketState.match(/public\s+MarketSnapshot\s+MarkStatus[\s\S]*?public\s+MarketSnapshot\s+Current/);
if (!markStatusMatch) {
  throw new Error("MarketState.MarkStatus block not found.");
}

const failures = [
  ...requiredDashboard
    .filter(([, pattern]) => !pattern.test(dashboard))
    .map(([label]) => `Missing feed freshness dashboard requirement: ${label}`),
  ...requiredProgram
    .filter(([, pattern]) => !pattern.test(program))
    .map(([label]) => `Missing feed freshness backend requirement: ${label}`),
  ...requiredRtdClient
    .filter(([, pattern]) => !pattern.test(rtdClient))
    .map(([label]) => `Missing feed freshness asset requirement: ${label}`),
];

if (/LocalTimestamp\s*=/.test(markStatusMatch[0])) {
  failures.push("MarketState.MarkStatus must not refresh LocalTimestamp.");
}

if (failures.length) {
  console.error(failures.join("\n"));
  process.exit(1);
}

console.log("Feed freshness OK");
