const fs = require("fs");
const path = require("path");

const root = path.resolve(__dirname, "..");
const dashboardPath = path.join(root, "src", "dashboard", "index.html");
const html = fs.readFileSync(dashboardPath, "utf8");
const styleMatch = html.match(/<style>([\s\S]*?)<\/style>/i);

if (!styleMatch) {
  throw new Error("Dashboard style block not found.");
}

const css = styleMatch[1];
const failures = [];

const forbiddenCss = [
  ["box-shadow", /\bbox-shadow\s*:/i],
  ["text-shadow", /\btext-shadow\s*:/i],
  ["linear-gradient", /\blinear-gradient\s*\(/i],
  ["radial-gradient", /\bradial-gradient\s*\(/i],
  ["conic-gradient", /\bconic-gradient\s*\(/i],
  ["filter", /\bfilter\s*:/i],
];

for (const [label, pattern] of forbiddenCss) {
  if (pattern.test(css)) failures.push(`Forbidden Industrial token found: ${label}`);
}

if (!/--bg:\s*#0B0C0A\s*;/i.test(css)) {
  failures.push("Industrial warm-black --bg token must remain #0B0C0A.");
}

if (!/--font:[^;]*monospace\s*;/i.test(css) || !/--mono:[^;]*monospace\s*;/i.test(css)) {
  failures.push("Dashboard font tokens must end in monospace.");
}

if (!/html,body\{[^}]*font-family:var\(--font\)/i.test(css)) {
  failures.push("Dashboard body must use var(--font).");
}

if (/font-family:[^;]*(Arial|Helvetica|Inter|system-ui|serif)/i.test(css)) {
  failures.push("Non-industrial font family found in dashboard CSS.");
}

if (!/\.app \*\{border-radius:0!important\}/i.test(css)) {
  failures.push("Dashboard must enforce square Industrial UI with .app * border-radius:0!important.");
}

if (!/\.workspace-status\{[\s\S]*?border:1px solid/i.test(css) || !/\bfunction\s+workspaceGroupStatus\b/.test(html)) {
  failures.push("Workspace menu must expose compact operational status badges.");
}

if (!/\.command-status\{[\s\S]*?border:1px solid/i.test(css) || !/\bfunction\s+buildCommandItems[\s\S]*workspaceGroupStatus\(group\.id\)[\s\S]*assetFeedFreshness\(asset,\s*item\)/.test(html)) {
  failures.push("Command palette must expose grouped operational statuses and asset freshness.");
}

if (failures.length) {
  console.error(failures.join("\n"));
  process.exit(1);
}

console.log("Dashboard design tokens OK");
