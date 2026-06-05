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
  ["terminal pill element", /id="terminal-pill"/],
  ["terminal pill ref", /\bterminalPill:\s*document\.getElementById\("terminal-pill"\)/],
  ["terminal health helper", /\bfunction\s+terminalHealth\b/],
  ["feed freshness input", /\bfeedFreshness\(snapshot\)/],
  ["websocket state input", /\breadyState\s*===\s*WebSocket\.OPEN\b/],
  ["latency input", /\blastLagMs\b/],
  ["render duration input", /\blastDurationMs\b/],
  ["flow queue input", /\bqueueSize\b/],
  ["process memory input", /\bworkingSetMb\b/],
  ["terminal pill render", /\bsetPill\(refs\.terminalPill,\s*terminal\.tone,\s*terminal\.label\)/],
  ["terminal OK label", /\bTerminal OK\b/],
  ["terminal attention label", /\bTerminal Atencao\b/],
  ["terminal alert label", /\bTerminal Alerta\b/],
  ["session top tile", /id="top-session"/],
  ["session settings start", /id="settings-session-start"/],
  ["session settings end", /id="settings-session-end"/],
  ["session status helper", /\bfunction\s+sessionStatus\b/],
  ["session settings persisted", /\bsessionStart\b[\s\S]*\bsessionEnd\b/],
  ["session rendered in market strip", /\brefs\.topSession\.textContent\b[\s\S]*\bsetMetricTone\(refs\.topSessionTile,\s*session\.tone\)/],
  ["session diagnostics", /\bSessao local\b/],
];

const docRequired = [
  ["README terminal summary", /\bTerminal OK\b[\s\S]*\bTerminal Atencao\b[\s\S]*\bTerminal Alerta\b/],
  ["README session summary", /\bSessao\b[\s\S]*\bInicio da sessao\b[\s\S]*\bFim da sessao\b/],
  ["architecture terminal summary", /\bTerminal OK\b[\s\S]*\bTerminal Atencao\b[\s\S]*\bTerminal Alerta\b/],
  ["architecture session summary", /\bSessao\b[\s\S]*\bwdo-ui-settings\b/],
  ["validation terminal QA", /\bvalidate-terminal-health\.js\b/],
];

const failures = required
  .filter(([, pattern]) => !pattern.test(dashboard))
  .map(([label]) => `Missing terminal health requirement: ${label}`)
  .concat(docRequired
    .filter(([, pattern]) => !pattern.test(docs))
    .map(([label]) => `Missing terminal health doc requirement: ${label}`));

if (failures.length) {
  console.error(failures.join("\n"));
  process.exit(1);
}

console.log("Terminal health OK");
