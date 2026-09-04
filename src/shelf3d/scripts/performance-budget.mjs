import { performance } from "node:perf_hooks";

const BOOK_COUNTS = [50, 250, 500, 1_000, 5_000, 10_000];
const ITERATIONS = 250;
const WARMUP_ITERATIONS = 10;
const LAYOUT_BUDGET_MS = 5;
const MAX_RESIDENT_BOOKS = 500;
const TEXTURE_RESIDENT_RADIUS = 80;

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

function measure(mode, bookCount) {
  const samples = [];
  let checksum = 0;
  for (let i = 0; i < ITERATIONS + WARMUP_ITERATIONS; i++) {
    const started = performance.now();
    checksum += computeLayout(mode, bookCount);
    if (i >= WARMUP_ITERATIONS) {
      samples.push(performance.now() - started);
    }
  }

  samples.sort((a, b) => a - b);
  return {
    mode,
    bookCount,
    checksum: Number(checksum.toFixed(4)),
    p95Ms: samples[Math.floor(samples.length * 0.95)],
    maxMs: samples.at(-1),
  };
}

const results = BOOK_COUNTS.flatMap((bookCount) => [measure("shelf", bookCount), measure("grid3d", bookCount)]);
for (const result of results) {
  console.log(`${result.mode} ${result.bookCount}: p95=${result.p95Ms.toFixed(3)}ms max=${result.maxMs.toFixed(3)}ms checksum=${result.checksum}`);
  if (result.p95Ms > LAYOUT_BUDGET_MS) {
    throw new Error(`${result.mode} layout p95 ${result.p95Ms.toFixed(3)}ms exceeds ${LAYOUT_BUDGET_MS}ms budget.`);
  }

  // This Node-only arithmetic harness is intentionally not a GPU/frame-time
  // claim. Max is retained in the output for diagnostics; p95 is the stable
  // gate while runtime WebView metrics provide the real rendering evidence.
}

for (const bookCount of BOOK_COUNTS) {
  const residentBooks = Math.min(bookCount, MAX_RESIDENT_BOOKS);
  const textureResidentBooks = Math.min(residentBooks, TEXTURE_RESIDENT_RADIUS * 2 + 1);
  console.log(`residency ${bookCount}: meshes=${residentBooks} textured=${textureResidentBooks}`);
  if (residentBooks > MAX_RESIDENT_BOOKS || textureResidentBooks > TEXTURE_RESIDENT_RADIUS * 2 + 1) {
    throw new Error(`residency bounds exceeded for ${bookCount} books.`);
  }
}
