const fs = require("fs");
const path = require("path");

const root = path.resolve(__dirname, "..");
const dashboardPath = path.join(root, "src", "dashboard", "index.html");
const html = fs.readFileSync(dashboardPath, "utf8");

const required = [
  ["Garman-Klass", /\bfunction\s+calcGarmanKlass\b/],
  ["Parkinson", /\bfunction\s+calcParkinson\b/],
  ["Rogers-Satchell", /\bfunction\s+calcRogersSatchell\b/],
  ["Yang-Zhang", /\bfunction\s+calcYangZhang\b/],
  ["ATR", /\bfunction\s+calcATR\b/],
  ["Volume profile proxy", /\bfunction\s+volumeProfileProxy\b/],
  ["Backtest proxy", /\bfunction\s+backtestProxy\b/],
  ["Radar opportunities", /\bfunction\s+collectRadarOpportunities\b/],
  ["Flow alignment", /\bfunction\s+flowAlignment\b/],
  ["Quant reversal score", /\bfunction\s+quantReversalAssessment\b/],
  ["Quant indicator summary", /\bfunction\s+quantIndicatorSummary\b/],
  ["Visible Score Quant", /\bScore Quant\b/],
  ["Visible Indicadores Quant", /\bIndicadores Quant\b/],
  ["Visible Base Quant", /\bBase Quant\b/],
  ["Visible Evidencias Quant", /\bEvidencias Quant\b/],
  ["CSV source", /\bCSV\s+\$\{state\.selectedWindow\}d\b/],
  ["RTD price source", /\bRTD preco\b/],
  ["Real T&T source", /\bT&T real\b/],
  ["Derived flow source", /\bfluxo derivado\b/],
  ["Confluence/backtest source", /\bconfluencia\/backtest\b/],
];

const failures = required
  .filter(([, pattern]) => !pattern.test(html))
  .map(([label]) => `Missing quantitative surface requirement: ${label}`);

if (failures.length) {
  console.error(failures.join("\n"));
  process.exit(1);
}

console.log("Quant surface OK");
