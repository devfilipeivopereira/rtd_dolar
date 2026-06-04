const fs = require("fs");
const path = require("path");

const root = path.resolve(__dirname, "..");
const hub = fs.readFileSync(path.join(root, "src", "ColetorProfitRTD", "Web", "WebSocketHub.cs"), "utf8");
const program = fs.readFileSync(path.join(root, "src", "ColetorProfitRTD", "Program.cs"), "utf8");
const html = fs.readFileSync(path.join(root, "src", "dashboard", "index.html"), "utf8");

const source = `${hub}\n${program}\n${html}`;

const required = [
  ["hub health method", /\bDictionary<string,\s*object>\s+Health\(\)/],
  ["clients metric", /\["clients"\]\s*=/],
  ["broadcasts metric", /\["broadcasts"\]\s*=/],
  ["target messages metric", /\["targetMessages"\]\s*=/],
  ["failed sends metric", /\["failedSends"\]\s*=/],
  ["last broadcast metric", /\["lastBroadcastAt"\]\s*=/],
  ["health payload websocket", /\["webSocket"\]\s*=\s*hub\.Health\(\)/],
  ["connections websocket clients label", /\bWS clientes\b/],
  ["connections websocket broadcasts label", /\bWS broadcasts\b/],
  ["diagnostics websocket backend label", /\bWS clientes backend\b/],
];

const failures = required
  .filter(([, pattern]) => !pattern.test(source))
  .map(([label]) => `Missing WebSocket health requirement: ${label}`);

if (failures.length) {
  console.error(failures.join("\n"));
  process.exit(1);
}

console.log("WebSocket health OK");
