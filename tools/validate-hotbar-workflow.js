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
  ["hotbar readiness css", /\.hotbar-readiness\{[\s\S]*?display:flex/],
  ["hotbar source css", /\.hotbar-source\{[\s\S]*?border:1px solid/],
  ["hotbar next css", /\.hotbar-command,\.hotbar-next,\.top-command/],
  ["screen status helper", /\bfunction\s+hotbarScreenStatus\b/],
  ["readiness helper", /\bfunction\s+hotbarReadinessItems\b/],
  ["operational renderer", /\bfunction\s+renderHotbarOperationalState\b/],
  ["throttled renderer", /\bfunction\s+renderHotbarOperationalStateThrottled\b/],
  ["one second throttle", /lastHotbarStateMs\s*<\s*1000/],
  ["hotbar state element", /\bdata-hotbar-status\b/],
  ["hotbar readiness element", /\bdata-hotbar-readiness\b/],
  ["hotbar source buttons", /\bdata-hotbar-source\b/],
  ["hotbar next button", /\bdata-hotbar-next\b/],
  ["uses next step logic", /\bdashboardNextStep\(dashboardCommandContext\(\)\)/],
  ["updates during market strip", /\brenderMarketStrip[\s\S]*renderHotbarOperationalStateThrottled\(\)/],
  ["next click handler", /\bconst\s+next\s*=\s*ev\.target\.closest\("\[data-hotbar-next\]"\)/],
  ["source click handler", /\bconst\s+source\s*=\s*ev\.target\.closest\("\[data-hotbar-source\]"\)/],
  ["uses real book status", /\bliveBookDepthForAsset\(asset\)/],
  ["uses real times status", /\btimesTradesByAsset\[asset\]/],
  ["uses real feed status", /\bfeedFreshness\(snapshot\)/],
  ["uses real process status", /\bterminalHealth\(snapshot,\s*flow\.metrics\s*\|\|\s*\{\}\)/],
  ["price readiness", /\blabel:\s*"P"[\s\S]*Configurar RTD de preco/],
  ["book readiness", /\blabel:\s*"B"[\s\S]*Configurar RTD de Book/],
  ["times readiness", /\blabel:\s*"T"[\s\S]*Configurar RTD de T&T/],
  ["csv readiness", /\blabel:\s*"CSV"[\s\S]*Carregar CSV historico/],
  ["flow readiness", /\blabel:\s*"Flow"[\s\S]*Fluxo calculado/],
  ["edge readiness", /\blabel:\s*"Edge"[\s\S]*Aguardando score quant/],
  ["routine workspace group", /\bid:\s*"routine"[\s\S]*label:\s*"Rotina"[\s\S]*tabs:\s*\["prepare",\s*"live",\s*"review"\]/],
  ["routine panels", /\bid="panel-prepare"[\s\S]*id="panel-live"[\s\S]*id="panel-review"/],
  ["routine renderer prepare", /\bfunction\s+renderRoutinePrepare\b/],
  ["routine renderer live", /\bfunction\s+renderRoutineLive\b/],
  ["routine renderer review", /\bfunction\s+renderRoutineReview\b/],
  ["routine click handler", /\bdata-routine-open\b[\s\S]*showMainTab\(action\.dataset\.routineOpen\)/],
];

const docRequired = [
  ["README hotbar workflow", /\bhotbar\b[\s\S]*\bProximo\b/],
  ["README routine workflow", /\bRotina\b[\s\S]*\bPreparar\b[\s\S]*\bAo vivo\b[\s\S]*\bRevisar\b/],
  ["architecture hotbar workflow", /\bhotbar\b[\s\S]*\bProximo\b/],
  ["architecture routine workflow", /\bRotina\b[\s\S]*\bPreparar\b[\s\S]*\bAo vivo\b[\s\S]*\bRevisar\b/],
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
