const fs = require("fs");
const path = require("path");

const root = path.resolve(__dirname, "..");
const dashboardPath = path.join(root, "src", "dashboard", "index.html");
const html = fs.readFileSync(dashboardPath, "utf8");

const required = [
  ["pending render reasons", /\bpendingReasons\b/],
  ["pending render assets", /\bpendingAssets\b/],
  ["last render reasons", /\blastReasons\b/],
  ["active tab filter", /\bfunction\s+activeTabNeedsRender\b/],
  ["reason-aware scheduler", /\bfunction\s+scheduleLiveRender\s*\([^)]*reason\s*=\s*"ui"/],
  ["snapshot reason", /\bscheduleLiveRender\([^)]*"snapshot"/],
  ["book reason", /\brenderLiveAssetChange\([^)]*"book"/],
  ["times reason", /\brenderLiveAssetChange\([^)]*"times"/],
  ["flow reason", /\brenderLiveAssetChange\([^)]*"flow"/],
  ["signal reason", /\brenderLiveAssetChange\([^)]*"signal"/],
  ["diagnostic render reasons", /\bRender motivos\b/],
  ["diagnostic render assets", /\bRender ativos\b/],
  ["adaptive render guard state", /\bguardMode\b/],
  ["adaptive render guard interval", /\bguardIntervalMs\b/],
  ["adaptive render guard helper", /\bfunction\s+updateRenderGuard\b/],
  ["effective render interval helper", /\bfunction\s+effectiveLiveIntervalMs\b/],
  ["scheduler uses effective interval", /\bconst\s+interval\s*=\s*effectiveLiveIntervalMs\(\)/],
  ["render guard diagnostics", /\bRender guard\b/],
  ["render guard top strip", /\bGuard\b/],
];

const failures = required
  .filter(([, pattern]) => !pattern.test(html))
  .map(([label]) => `Missing live render scheduler requirement: ${label}`);

if (failures.length) {
  console.error(failures.join("\n"));
  process.exit(1);
}

console.log("Live render scheduler OK");
