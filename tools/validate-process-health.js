const fs = require("fs");
const path = require("path");

const root = path.resolve(__dirname, "..");
const program = fs.readFileSync(path.join(root, "src", "ColetorProfitRTD", "Program.cs"), "utf8");
const html = fs.readFileSync(path.join(root, "src", "dashboard", "index.html"), "utf8");
const source = `${program}\n${html}`;

const required = [
  ["process health builder", /\bBuildProcessHealth\b/],
  ["health process payload", /\["process"\]\s*=\s*BuildProcessHealth\(\)/],
  ["process id", /\["processId"\]\s*=/],
  ["started at", /\["startedAt"\]\s*=/],
  ["uptime", /\["uptimeMs"\]\s*=/],
  ["working set", /\["workingSetMb"\]\s*=/],
  ["private memory", /\["privateMemoryMb"\]\s*=/],
  ["gc memory", /\["gcMemoryMb"\]\s*=/],
  ["thread count", /\["threadCount"\]\s*=/],
  ["connections process label", /\bUptime app\b/],
  ["diagnostics memory label", /\bMemoria local\b/],
  ["diagnostics threads label", /\bThreads local\b/],
];

const failures = required
  .filter(([, pattern]) => !pattern.test(source))
  .map(([label]) => `Missing process health requirement: ${label}`);

if (failures.length) {
  console.error(failures.join("\n"));
  process.exit(1);
}

console.log("Process health OK");
