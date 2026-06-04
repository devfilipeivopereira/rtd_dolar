const fs = require("fs");
const path = require("path");

const root = path.resolve(__dirname, "..");
const docsDir = path.join(root, "docs");
const files = [
  path.join(root, "src", "dashboard", "index.html"),
  path.join(root, "README.md"),
  ...fs.readdirSync(docsDir)
    .filter(name => name.endsWith(".md"))
    .map(name => path.join(docsDir, name)),
];

const forbidden = [
  ["Boleta", /\bboleta\b/i],
  ["ordem/ordens", /\bordens?\b/i],
  ["envio de comando operacional", /\benvio\s+de\s+comando\s+operacional\b/i],
  ["executar comandos operacionais", /\bexecut(ar|a|ando|ado|ada)\s+comandos?\s+operacionais?\b/i],
];

const requiredInDashboard = [
  ["Radar", /\bRadar\b/],
  ["Oportunidades", /\bOportunidades\b/],
  ["Analise group", /\bAnalise\b/],
];

const failures = [];

for (const file of files) {
  const text = fs.readFileSync(file, "utf8");
  for (const [label, pattern] of forbidden) {
    if (pattern.test(text)) {
      failures.push(`${path.relative(root, file)} contains forbidden execution language: ${label}`);
    }
  }
}

const dashboard = fs.readFileSync(path.join(root, "src", "dashboard", "index.html"), "utf8");
for (const [label, pattern] of requiredInDashboard) {
  if (!pattern.test(dashboard)) {
    failures.push(`Dashboard must keep analysis/opportunity language: ${label}`);
  }
}

if (failures.length) {
  console.error(failures.join("\n"));
  process.exit(1);
}

console.log("Product language OK");
