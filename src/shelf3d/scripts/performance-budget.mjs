import { performance } from "node:perf_hooks";

const BOOK_COUNT = 500;
const ITERATIONS = 250;
const FRAME_BUDGET_MS = 16.67;
const LAYOUT_BUDGET_MS = 5;

function shelfPosition(index) {
  const shelfColumns = 50;
  const column = index % shelfColumns;
  const row = Math.floor(index / shelfColumns);
  return {
    x: (column - shelfColumns / 2) * 0.03,
    y: -row * 0.24,
    z: 0,
    rotationY: ((index % 5) - 2) * 0.015,
  };
}

function gridPosition(index, count) {
  const side = Math.ceil(Math.sqrt(Math.max(count, 1)));
  const column = index % side;
  const row = Math.floor(index / side);
  return {
    x: (column - side / 2) * 0.05,
    y: 0,
    z: (row - side / 2) * 0.08,
    rotationY: 0,
  };
}

function computeLayout(mode, count) {
  let checksum = 0;
  for (let index = 0; index < count; index++) {
    const transform = mode === "shelf" ? shelfPosition(index) : gridPosition(index, count);
    checksum += transform.x + transform.y + transform.z + transform.rotationY;
  }

  return checksum;
}

function measure(mode) {
  const samples = [];
  let checksum = 0;
  for (let i = 0; i < ITERATIONS; i++) {
    const started = performance.now();
    checksum += computeLayout(mode, BOOK_COUNT);
    samples.push(performance.now() - started);
  }

  samples.sort((a, b) => a - b);
  return {
    mode,
    checksum: Number(checksum.toFixed(4)),
    p95Ms: samples[Math.floor(samples.length * 0.95)],
    maxMs: samples.at(-1),
  };
}

const results = [measure("shelf"), measure("grid3d")];
for (const result of results) {
  console.log(`${result.mode}: p95=${result.p95Ms.toFixed(3)}ms max=${result.maxMs.toFixed(3)}ms checksum=${result.checksum}`);
  if (result.p95Ms > LAYOUT_BUDGET_MS) {
    throw new Error(`${result.mode} layout p95 ${result.p95Ms.toFixed(3)}ms exceeds ${LAYOUT_BUDGET_MS}ms budget.`);
  }

  if (result.maxMs > FRAME_BUDGET_MS) {
    throw new Error(`${result.mode} layout max ${result.maxMs.toFixed(3)}ms exceeds ${FRAME_BUDGET_MS}ms frame budget.`);
  }
}
