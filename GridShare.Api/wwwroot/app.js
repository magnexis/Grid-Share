import * as THREE from "https://cdn.jsdelivr.net/npm/three@0.164.1/build/three.module.js";

const canvas = document.querySelector("#grid-canvas");
const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: false });
renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
renderer.setClearColor(0x07090c, 1);

const scene = new THREE.Scene();
scene.fog = new THREE.Fog(0x07090c, 20, 42);

const camera = new THREE.PerspectiveCamera(45, 1, 0.1, 100);
camera.position.set(12, 16, 18);
camera.lookAt(0, 0, 0);

const ambient = new THREE.AmbientLight(0xb7d8ff, 0.65);
scene.add(ambient);

const sun = new THREE.DirectionalLight(0xffcf8f, 1.5);
sun.position.set(5, 14, 7);
scene.add(sun);

const grid = new THREE.GridHelper(24, 24, 0x355064, 0x162430);
grid.position.y = -0.02;
scene.add(grid);

const nodeMeshes = new Map();
const flowMeshes = [];
const state = {
  frame: null,
  selectedNodeId: null,
  lastTickAt: 0,
  currencies: [
    { code: "USD", displayName: "US Dollar", region: "United States solar markets", unitsPerUsd: 1, locale: "en-US" },
  ],
  currency: { code: "USD", displayName: "US Dollar", region: "United States solar markets", unitsPerUsd: 1, locale: "en-US" },
};

const producerMaterial = new THREE.MeshStandardMaterial({
  color: 0xffb84d,
  emissive: 0x70410b,
  roughness: 0.42,
  metalness: 0.08,
});
const consumerMaterial = new THREE.MeshStandardMaterial({
  color: 0x4aa9ff,
  emissive: 0x103d72,
  roughness: 0.48,
  metalness: 0.04,
});
const neutralMaterial = new THREE.MeshStandardMaterial({
  color: 0x8ca4b5,
  emissive: 0x14202a,
  roughness: 0.55,
  metalness: 0.02,
});

function resize() {
  const width = canvas.clientWidth || window.innerWidth;
  const height = canvas.clientHeight || window.innerHeight;
  camera.aspect = width / height;
  camera.updateProjectionMatrix();
  renderer.setSize(width, height, false);
}

window.addEventListener("resize", resize);
resize();

function normalize(value, fallback = {}) {
  return value ?? fallback;
}

function prop(object, camel, pascal) {
  return object?.[camel] ?? object?.[pascal];
}

function setText(id, value) {
  const element = document.getElementById(id);
  if (element) {
    element.textContent = value;
  }
}

function money(usdAmount, options = {}) {
  const converted = Number(usdAmount ?? 0) * Number(state.currency.unitsPerUsd ?? 1);
  return new Intl.NumberFormat(state.currency.locale || "en-US", {
    style: "currency",
    currency: state.currency.code,
    maximumFractionDigits: options.maximumFractionDigits ?? 3,
    minimumFractionDigits: options.minimumFractionDigits ?? 2,
  }).format(converted);
}

function updateConnection(text, className = "") {
  const element = document.getElementById("connection-state");
  element.textContent = text;
  element.className = `connection ${className}`.trim();
}

function nodePosition(node) {
  const location = prop(node, "location", "Location") ?? {};
  const x = (prop(location, "x", "X") ?? 0) - 5;
  const z = (prop(location, "y", "Y") ?? 0) - 5;
  return new THREE.Vector3(x * 1.5, 0, z * 1.5);
}

function nodeNet(node) {
  return prop(node, "netKw", "NetKw") ?? prop(node, "gridExchangeKw", "GridExchangeKw") ?? 0;
}

function ensureNode(node) {
  const id = prop(node, "nodeId", "NodeId");
  if (!id) return null;

  let mesh = nodeMeshes.get(id);
  if (!mesh) {
    const height = 0.4 + Math.random() * 1.2;
    const geometry = new THREE.BoxGeometry(0.8, height, 0.8);
    mesh = new THREE.Mesh(geometry, neutralMaterial);
    mesh.userData.height = height;
    mesh.userData.nodeId = id;
    scene.add(mesh);
    nodeMeshes.set(id, mesh);
  }

  const net = nodeNet(node);
  mesh.material = net > 0.05 ? producerMaterial : net < -0.05 ? consumerMaterial : neutralMaterial;
  const target = nodePosition(node);
  mesh.position.lerp(new THREE.Vector3(target.x, mesh.userData.height / 2, target.z), 0.35);
  const scale = 1 + Math.min(Math.abs(net) / 12, 0.7);
  mesh.scale.setScalar(scale);

  return mesh;
}

function clearFlows() {
  while (flowMeshes.length > 0) {
    const mesh = flowMeshes.pop();
    scene.remove(mesh);
    mesh.geometry.dispose();
    mesh.material.dispose();
  }
}

function createFlow(fromMesh, toMesh, flow) {
  const start = fromMesh.position.clone();
  const end = toMesh.position.clone();
  start.y += fromMesh.userData.height + 0.3;
  end.y += toMesh.userData.height + 0.3;
  const mid = start.clone().lerp(end, 0.5);
  mid.y += 1.8;

  const curve = new THREE.QuadraticBezierCurve3(start, mid, end);
  const points = curve.getPoints(32);
  const geometry = new THREE.BufferGeometry().setFromPoints(points);
  const material = new THREE.LineBasicMaterial({
    color: prop(flow, "to", "To") === "COMMUNITY_BATTERY" ? 0xffbf63 : 0x7ed2ff,
    transparent: true,
    opacity: 0.78,
  });
  const line = new THREE.Line(geometry, material);
  line.userData.phase = Math.random() * Math.PI * 2;
  scene.add(line);
  flowMeshes.push(line);
}

function updateScene(frame) {
  const nodes = normalize(prop(frame, "nodes", "Nodes"), []);
  const flows = normalize(prop(frame, "flows", "Flows"), []);
  nodes.forEach(ensureNode);
  clearFlows();

  flows.slice(0, 36).forEach((flow) => {
    const fromMesh = nodeMeshes.get(prop(flow, "from", "From"));
    const toMesh = nodeMeshes.get(prop(flow, "to", "To"));
    if (fromMesh && toMesh) {
      createFlow(fromMesh, toMesh, flow);
    }
  });

  if (!state.selectedNodeId && nodes.length > 0) {
    state.selectedNodeId = prop(nodes[0], "nodeId", "NodeId");
  }
}

function updateHud(frame) {
  const market = normalize(prop(frame, "market", "Market"));
  const nodes = normalize(prop(frame, "nodes", "Nodes"), []);
  const status = prop(market, "status", "Status");
  const statusText = status === 1 || status === "BlackoutRisk" ? "Blackout Risk" : "Nominal";
  const stale = nodes.filter((node) => prop(node, "isStale", "IsStale")).length;
  const lastBlocks = normalize(prop(market, "lastLedgerBlocks", "LastLedgerBlocks"), []);
  const carbon = normalize(prop(market, "carbon", "Carbon"));

  setText("grid-status", statusText);
  setText("produced", `${(prop(market, "totalProductionKw", "TotalProductionKw") ?? 0).toFixed(2)} kW`);
  setText("consumed", `${(prop(market, "totalConsumptionKw", "TotalConsumptionKw") ?? 0).toFixed(2)} kW`);
  setText("supply", `${(prop(market, "marketSupplyKw", "MarketSupplyKw") ?? 0).toFixed(2)} kW`);
  setText("demand", `${(prop(market, "marketDemandKw", "MarketDemandKw") ?? 0).toFixed(2)} kW`);
  const price = prop(market, "marketPricePerKwh", "MarketPricePerKwh") ?? 0;
  setText("price", `${money(price, { maximumFractionDigits: 3 })}`);
  setText("currency-label", state.currency.code);
  setText("orders", `${normalize(prop(market, "openOrders", "OpenOrders"), []).length}`);
  setText("carbon", `${(prop(carbon, "carbonOffsetKg", "CarbonOffsetKg") ?? 0).toFixed(2)} kg`);
  setText("trade-count", `${lastBlocks.length}`);
  setText("node-count", `${nodes.length} online`);
  setText("sim-time", new Date(prop(market, "simulationTime", "SimulationTime") ?? Date.now()).toLocaleString());
  setText("stale", `${stale}`);

  const priceFill = document.getElementById("price-fill");
  priceFill.style.width = `${Math.min(100, Math.max(0, (Number(price) / 0.65) * 100))}%`;

  renderTrades(lastBlocks);
  renderNodeFocus(nodes);
  refreshLedgerHealth();
}

function renderTrades(blocks) {
  const list = document.getElementById("trade-list");
  list.innerHTML = "";
  if (blocks.length === 0) {
    const item = document.createElement("li");
    item.innerHTML = "<span>No trades yet</span><strong>--</strong>";
    list.appendChild(item);
    return;
  }

  blocks.slice(0, 5).forEach((block) => {
    const from = prop(block, "prosumerId", "ProsumerId");
    const to = prop(block, "consumerId", "ConsumerId");
    const fromMarket = prop(block, "prosumerMarketCode", "ProsumerMarketCode");
    const toMarket = prop(block, "consumerMarketCode", "ConsumerMarketCode");
    const delivered = prop(block, "deliveredKwh", "DeliveredKwh") ?? prop(block, "kwhAmount", "KwhAmount") ?? 0;
    const amount = prop(block, "settlementAmount", "SettlementAmount") ?? 0;
    const item = document.createElement("li");
    const marketText = fromMarket && toMarket ? ` (${fromMarket} -> ${toMarket})` : "";
    item.innerHTML = `<span>${from} -> ${to}${marketText}<br>${Number(delivered).toFixed(2)} kWh delivered</span><strong>${money(amount, { maximumFractionDigits: 2 })}</strong>`;
    list.appendChild(item);
  });
}

function renderNodeFocus(nodes) {
  const focus = document.getElementById("node-focus");
  if (nodes.length === 0) {
    focus.textContent = "Waiting for node telemetry.";
    return;
  }

  const sorted = [...nodes].sort((a, b) => Math.abs(nodeNet(b)) - Math.abs(nodeNet(a)));
  const node = sorted.find((candidate) => prop(candidate, "nodeId", "NodeId") === state.selectedNodeId) ?? sorted[0];
  state.selectedNodeId = prop(node, "nodeId", "NodeId");
  const net = nodeNet(node);
  const battery = prop(node, "batteryChargeKwh", "BatteryChargeKwh") ?? 0;
  const capacity = prop(node, "batteryCapacityKwh", "BatteryCapacityKwh") ?? 0;
  const marketCode = prop(node, "marketCode", "MarketCode") ?? "US-AVG";
  focus.innerHTML = `<strong>${state.selectedNodeId}</strong><br>${marketCode} location price basis<br>${net >= 0 ? "Exporting" : "Importing"} ${Math.abs(net).toFixed(2)} kW<br>Battery ${battery.toFixed(2)} / ${capacity.toFixed(2)} kWh`;
}

async function refreshLedgerHealth() {
  try {
    const response = await fetch("/api/grid/health", { cache: "no-store" });
    const health = await response.json();
    setText("ledger", health.ledgerVerified ? "verified" : "failed");
  } catch {
    setText("ledger", "unknown");
  }
}

async function loadInitialFrame() {
  const response = await fetch("/api/grid/frame", { cache: "no-store" });
  if (!response.ok) return;
  const frame = await response.json();
  if (frame.market) {
    state.frame = frame;
    updateScene(frame);
    updateHud(frame);
  }
}

async function loadCurrencies() {
  try {
    const response = await fetch("/api/currencies", { cache: "no-store" });
    if (!response.ok) return;
    state.currencies = await response.json();
  } catch {
    return;
  }

  const select = document.getElementById("currency-select");
  select.innerHTML = "";
  state.currencies.forEach((profile) => {
    const option = document.createElement("option");
    option.value = profile.code;
    option.textContent = `${profile.code} - ${profile.displayName}`;
    option.title = profile.region;
    select.appendChild(option);
  });

  const saved = localStorage.getItem("gridshare.currency");
  const selected = state.currencies.find((profile) => profile.code === saved) ?? state.currencies[0];
  state.currency = selected;
  select.value = selected.code;
  select.addEventListener("change", () => {
    state.currency = state.currencies.find((profile) => profile.code === select.value) ?? state.currencies[0];
    localStorage.setItem("gridshare.currency", state.currency.code);
    if (state.frame) {
      updateHud(state.frame);
    }
  });
}

async function connect() {
  updateConnection("Connecting");
  const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/grid")
    .withAutomaticReconnect()
    .build();

  connection.on("grid.tick", (frame) => {
    state.frame = frame;
    state.lastTickAt = performance.now();
    updateConnection("Live", "live");
    updateScene(frame);
    updateHud(frame);
  });

  connection.onreconnecting(() => updateConnection("Reconnecting", "warn"));
  connection.onreconnected(() => updateConnection("Live", "live"));
  connection.onclose(() => updateConnection("Offline", "warn"));

  await connection.start();
  updateConnection("Live", "live");
}

function animate(time) {
  requestAnimationFrame(animate);
  scene.rotation.y = Math.sin(time / 16000) * 0.04;
  flowMeshes.forEach((flow) => {
    flow.material.opacity = 0.42 + Math.sin(time / 280 + flow.userData.phase) * 0.28;
  });
  nodeMeshes.forEach((mesh) => {
    mesh.rotation.y += 0.002;
  });
  renderer.render(scene, camera);
}

await loadCurrencies();
await loadInitialFrame();
connect().catch(() => updateConnection("Offline", "warn"));
requestAnimationFrame(animate);
