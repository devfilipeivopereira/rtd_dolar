const fs = require("fs");
const path = require("path");

const root = path.resolve(__dirname, "..");
const dashboard = fs.readFileSync(path.join(root, "src", "dashboard", "index.html"), "utf8");
const docs = [
  fs.readFileSync(path.join(root, "README.md"), "utf8"),
  fs.readFileSync(path.join(root, "docs", "arquitetura.md"), "utf8"),
  fs.readFileSync(path.join(root, "docs", "validacao.md"), "utf8"),
].join("\n");

const required = [
  ["hotbar state css", /\.hotbar-state\{[\s\S]*?border:1px solid/],
  ["hotbar next css", /\.hotbar-command,\.hotbar-next,\.top-command/],
  ["screen status helper", /\bfunction\s+hotbarScreenStatus\b/],
  ["operational renderer", /\bfunction\s+renderHotbarOperationalState\b/],
  ["throttled renderer", /\bfunction\s+renderHotbarOperationalStateThrottled\b/],
  ["one second throttle", /lastHotbarStateMs\s*<\s*1000/],
  ["hotbar state element", /\bdata-hotbar-status\b/],
  ["hotbar next button", /\bdata-hotbar-next\b/],
  ["uses next step logic", /\bdashboardNextStep\(dashboardCommandContext\(\)\)/],
  ["updates during market strip", /\brenderMarketStrip[\s\S]*renderHotbarOperationalStateThrottled\(\)/],
  ["next click handler", /\bconst\s+next\s*=\s*ev\.target\.closest\("\[data-hotbar-next\]"\)/],
  ["uses real book status", /\bliveBookDepthForAsset\(asset\)/],
  ["uses real times status", /\btimesTradesByAsset\[asset\]/],
  ["uses real feed status", /\bfeedFreshness\(snapshot\)/],
  ["uses real process status", /\bterminalHealth\(snapshot,\s*flow\.metrics\s*\|\|\s*\{\}\)/],
];

const docRequired = [
  ["README hotbar workflow", /\bhotbar\b[\s\S]*\bProximo\b/],
  ["architecture hotbar workflow", /\bhotbar\b[\s\S]*\bProximo\b/],
  ["validation hotbar QA", /\bvalidate-hotbar-workflow\.js\b/],
];

const failures = required
  .filter(([, pattern]) => !pattern.test(dashboard))
  .map(([label]) => `Missing hotbar workflow requirement: ${label}`)
  .concat(docRequired
    .filter(([, pattern]) => !pattern.test(docs))
    .map(([label]) => `Missing hotbar workflow doc requirement: ${label}`));

if (failures.length) {
  console.error(failures.join("\n"));
  process.exit(1);
}

console.log("Hotbar workflow OK");
