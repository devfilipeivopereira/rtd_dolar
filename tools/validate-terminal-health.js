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
];

const docRequired = [
  ["README terminal summary", /\bTerminal OK\b[\s\S]*\bTerminal Atencao\b[\s\S]*\bTerminal Alerta\b/],
  ["architecture terminal summary", /\bTerminal OK\b[\s\S]*\bTerminal Atencao\b[\s\S]*\bTerminal Alerta\b/],
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
