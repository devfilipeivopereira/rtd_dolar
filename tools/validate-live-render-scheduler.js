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
  ["background render pause state", /\bbackgroundPaused\b/],
  ["background render helper", /\bfunction\s+pauseBackgroundRender\b/],
  ["background render resume", /\bfunction\s+resumeBackgroundRender\b/],
  ["visibility helper", /\bfunction\s+documentIsHidden\b/],
  ["scheduler skips hidden tab", /\bif\s*\(documentIsHidden\(\)\)\s*\{[\s\S]*?pauseBackgroundRender\(\);[\s\S]*?return;/],
  ["visibility change listener", /\bdocument\.addEventListener\("visibilitychange"/],
  ["visibility render reason", /\bscheduleLiveRender\([^)]*"visibility"/],
  ["background diagnostics", /\bRender background\b/],
  ["snapshot material render key", /\bfunction\s+snapshotRenderKey\b/],
  ["snapshot render dedupe gate", /\bfunction\s+shouldRenderSnapshot\b/],
  ["snapshot dedupe counter", /\bsnapshotRenderSkipped\b/],
  ["snapshot handler uses dedupe", /\bconst\s+shouldRender\s*=\s*shouldRenderSnapshot\(/],
  ["snapshot dedupe diagnostics", /\bRender dedupe\b/],
  ["prepare render filter", /\btab\s*===\s*"prepare"[\s\S]*has\("snapshot",\s*"status",\s*"asset",\s*"csv",\s*"flow",\s*"times",\s*"book"\)/],
  ["live render filter", /\btab\s*===\s*"live"[\s\S]*has\("snapshot",\s*"book",\s*"times",\s*"flow",\s*"signal",\s*"status"\)/],
  ["review render filter", /\btab\s*===\s*"review"[\s\S]*has\("snapshot",\s*"times",\s*"flow",\s*"signal",\s*"status",\s*"csv"\)/],
  ["derived cache helper", /\bfunction\s+cachedDerivedValue\b/],
  ["derived cache key", /\bfunction\s+derivedCacheKey\b/],
  ["dom levels cached wrapper", /\bfunction\s+collectDomLevelsRaw\b[\s\S]*\bfunction\s+collectDomLevels\b[\s\S]*cachedDerivedValue\("domLevels"/],
  ["radar cached wrapper", /\bfunction\s+collectRadarOpportunitiesRaw\b[\s\S]*\bfunction\s+collectRadarOpportunities\b[\s\S]*cachedDerivedValue\("radar"/],
  ["quant cached wrapper", /\bfunction\s+quantReversalAssessmentRaw\b[\s\S]*\bfunction\s+quantReversalAssessment\b[\s\S]*cachedDerivedValue\("quant"/],
  ["derived cache diagnostics", /\bCache derivados\b/],
];

const failures = required
  .filter(([, pattern]) => !pattern.test(html))
  .map(([label]) => `Missing live render scheduler requirement: ${label}`);

if (failures.length) {
  console.error(failures.join("\n"));
  process.exit(1);
}

console.log("Live render scheduler OK");
