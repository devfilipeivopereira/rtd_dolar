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
  ["Quant edge assessment", /\bfunction\s+quantEdgeAssessment\b/],
  ["Quant edge risk reward", /\bfunction\s+estimateEdgeRiskReward\b/],
  ["Quant volatility agreement", /\bfunction\s+volatilityAgreementScore\b/],
  ["Quant opportunity gate", /\bfunction\s+applyQuantGateToOpportunity\b/],
  ["Quant score cap", /\bMath\.min\(rawScore\s*\*\s*dataPenalty,\s*edge\.cap\)/],
  ["Radar score gate", /\bbaseScore\s*\*\s*edge\.multiplier\b/],
  ["EV proxy calculation", /\bwinRate\s*\*\s*rr\.rr\s*-\s*\(1\s*-\s*winRate\)/],
  ["Quant feed freshness", /\bconst\s+freshness\s*=\s*feedFreshness\(snapshot\)/],
  ["Quant feed penalty", /\bfeedPenalty\b/],
  ["Quant feed stopped label", /\bfeed parado\b/],
  ["Quant feed delayed label", /\bfeed atrasado\b/],
  ["Radar feed penalty", /\bfunction\s+collectRadarOpportunities[\s\S]*feedPenalty[\s\S]*\*\s*feedPenalty/],
  ["Radar feed evidence", /\bfunction\s+collectRadarOpportunities[\s\S]*feedReason/],
  ["Asset radar freshness", /\bassetFeedFreshness\(asset,\s*item\)/],
  ["Visible Score Quant", /\bScore Quant\b/],
  ["Visible Indicadores Quant", /\bIndicadores Quant\b/],
  ["Visible Base Quant", /\bBase Quant\b/],
  ["Visible Evidencias Quant", /\bEvidencias Quant\b/],
  ["Visible Edge Quant", /\bEdge Quant\b/],
  ["Visible Gate Quant", /\bGate Quant\b/],
  ["Visible EV Proxy", /\bEV Proxy\b/],
  ["Visible RR Proxy", /\bR\/R Proxy\b/],
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
