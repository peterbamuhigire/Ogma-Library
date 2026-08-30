import * as THREE from "three";
import { OrbitControls } from "three/examples/jsm/controls/OrbitControls.js";
import type { BookSceneItem, CameraState, InboundMessage, OutboundMessage } from "./messages";
import { assertNever, BRIDGE_PROTOCOL_VERSION } from "./messages";

type WebKitBridge = Readonly<{
  messageHandlers?: Readonly<{ ogma?: Readonly<{ postMessage: (message: string) => void }> }>;
}>;

type ChromiumBridge = Readonly<{
  webview?: Readonly<{ postMessage: (message: string) => void }>;
}>;

type BookMesh = THREE.Mesh<THREE.BoxGeometry, THREE.MeshStandardMaterial>;

declare global {
  interface Window {
    chrome?: ChromiumBridge;
    webkit?: WebKitBridge;
  }
}

const BOOK_WIDTH = 0.025;
const BOOK_HEIGHT = 0.18;
const BOOK_DEPTH = 0.13;
const SHELF_COLUMNS = 18;
const SHELF_ROW_HEIGHT = 0.27;
const MAX_RESIDENT_BOOKS = 500;
const BRIDGE_MESSAGE_BOOK_LIMIT = 100_000;

export class Shelf3DScene {
  private readonly scene = new THREE.Scene();
  private readonly camera = new THREE.PerspectiveCamera(45, 1, 0.01, 100);
  private readonly raycaster = new THREE.Raycaster();
  private readonly pointer = new THREE.Vector2();
  private readonly renderer: THREE.WebGLRenderer;
  private readonly controls: OrbitControls;
  private readonly geometry = new THREE.BoxGeometry(BOOK_WIDTH, BOOK_HEIGHT, BOOK_DEPTH);
  private readonly shelfGeometry = new THREE.BoxGeometry(SHELF_COLUMNS * 0.03 + 0.04, 0.025, 0.18);
  private readonly sceneRoot = new THREE.Group();
  private readonly reducedMotion: boolean;
  private readonly bookMeshes: BookMesh[] = [];
  private readonly shelfMeshes: BookMesh[] = [];
  private books: readonly BookSceneItem[] = [];
  private layout: "shelf" | "grid3d" = "shelf";
  private focusedIndex = 0;
  private residentStart = 0;
  private residentEnd = 0;
  private hoveredBookId: string | null = null;
  private frameSamples: number[] = [];
  private performanceWindowStartedAt = 0;
  private lastFrameTimestamp = 0;
  private lastPerformanceWarningAt = 0;

  public constructor(private readonly canvas: HTMLCanvasElement) {
    this.reducedMotion = window.matchMedia?.("(prefers-reduced-motion: reduce)").matches ?? false;
    this.renderer = new THREE.WebGLRenderer({ canvas, antialias: true, powerPreference: "high-performance" });
    this.controls = new OrbitControls(this.camera, canvas);
    this.controls.enableDamping = !this.reducedMotion;
    this.controls.addEventListener("change", () => this.postCameraChanged());

    this.scene.add(new THREE.HemisphereLight(0xffffff, 0x4b3426, 1.2));
    this.scene.add(this.sceneRoot);
    this.camera.position.set(0, 0.45, 1.4);
    this.camera.lookAt(0, 0, 0);
    this.canvas.setAttribute("aria-describedby", "shelf3d-status");
    this.resize();
    this.bindEvents();
    this.post({ type: "WebGl2Status", supported: this.isWebGl2Supported() });
  }

  public handleMessage(message: OutboundMessage): void {
    switch (message.type) {
      case "SetScene":
        this.setScene(message.books, message.camera);
        return;
      case "UpdateBook":
        this.updateBook(message.bookId, message.book);
        return;
      case "RemoveBook":
        this.removeBook(message.bookId);
        return;
      case "SetCamera":
        this.applyCamera(message.camera);
        return;
      case "SetTheme":
        this.setTheme(message.themeKey);
        return;
      case "SetLayout":
        this.layout = message.layout;
        this.rebuildResidentWindow();
        return;
      default:
        assertNever(message);
    }
  }

  public start(): void {
    const tick = (timestamp: number): void => {
      this.recordFrame(timestamp);
      this.controls.update();
      this.renderer.render(this.scene, this.camera);
      requestAnimationFrame(tick);
    };

    requestAnimationFrame(tick);
  }

  private setScene(books: readonly BookSceneItem[], camera: CameraState): void {
    this.clearScene();
    this.books = Array.isArray(books)
      ? books.slice(0, BRIDGE_MESSAGE_BOOK_LIMIT).filter((book) => this.isSafeBook(book))
      : [];
    this.focusedIndex = 0;
    this.rebuildResidentWindow();
    this.applyCamera(camera);
    this.setFocusedIndex(0, false, false);
    this.setStatus(this.books.length === 0
      ? "No books are available in this shelf."
      : `${this.books.length} books loaded; use arrow keys to browse and Enter to open.`);
  }

  private updateBook(bookId: string, book: BookSceneItem): void {
    const index = this.books.findIndex((existing) => existing.bookId === bookId);
    if (index < 0 || !this.isSafeBook(book)) return;
    this.books = this.books.map((existing, existingIndex) => existingIndex === index ? book : existing);
    const mesh = this.bookMeshes.find((candidate) => candidate.userData.bookIndex === index);
    if (mesh !== undefined) {
      mesh.userData.bookId = book.bookId;
      this.applySpineTexture(mesh, book);
    }
    this.applyLayout();
  }

  private removeBook(bookId: string): void {
    const remaining = this.books.filter((book) => book.bookId !== bookId);
    this.setScene(remaining, this.readCamera());
  }

  private rebuildResidentWindow(): void {
    this.clearBookMeshes();
    if (this.books.length === 0) {
      this.residentStart = 0;
      this.residentEnd = 0;
      this.buildShelves();
      return;
    }

    const maximumStart = Math.max(0, this.books.length - MAX_RESIDENT_BOOKS);
    this.residentStart = Math.min(
      maximumStart,
      Math.max(0, this.focusedIndex - Math.floor(MAX_RESIDENT_BOOKS / 2)));
    this.residentEnd = Math.min(this.books.length, this.residentStart + MAX_RESIDENT_BOOKS);

    for (let index = this.residentStart; index < this.residentEnd; index++) {
      const book = this.books[index];
      if (book === undefined) continue;
      const material = new THREE.MeshStandardMaterial({
        color: this.fallbackColor(book.bookId),
        roughness: 0.7,
        metalness: 0.05,
      });
      const mesh: BookMesh = new THREE.Mesh(this.geometry, material);
      mesh.userData.bookIndex = index;
      mesh.userData.bookId = book.bookId;
      this.bookMeshes.push(mesh);
      this.sceneRoot.add(mesh);
      this.applySpineTexture(mesh, book);
    }

    this.buildShelves();
    this.applyLayout();
    this.applyFocusScale();
  }

  private applyLayout(): void {
    for (const mesh of this.bookMeshes) {
      const index = mesh.userData.bookIndex as number;
      const position = this.layout === "shelf" ? this.shelfPosition(index) : this.gridPosition(index);
      const rotation = this.layout === "shelf" ? ((index % 5) - 2) * 0.015 : 0;
      mesh.position.copy(position);
      mesh.rotation.set(0, rotation, 0);
    }

    const firstRow = Math.floor(this.residentStart / SHELF_COLUMNS);
    for (let index = 0; index < this.shelfMeshes.length; index++) {
      const shelf = this.shelfMeshes[index];
      if (shelf !== undefined) {
        shelf.position.set(0, -(firstRow + index) * SHELF_ROW_HEIGHT - 0.115, 0.02);
      }
    }
  }

  private shelfPosition(index: number): THREE.Vector3 {
    const column = index % SHELF_COLUMNS;
    const row = Math.floor(index / SHELF_COLUMNS);
    return new THREE.Vector3((column - (SHELF_COLUMNS - 1) / 2) * 0.03, -row * SHELF_ROW_HEIGHT, 0);
  }

  private gridPosition(index: number): THREE.Vector3 {
    const side = Math.ceil(Math.sqrt(Math.max(this.books.length, 1)));
    const column = index % side;
    const row = Math.floor(index / side);
    return new THREE.Vector3((column - (side - 1) / 2) * 0.05, (side / 2 - row) * 0.08, 0);
  }

  private setTheme(themeKey: "light" | "dark"): void {
    this.scene.background = new THREE.Color(themeKey === "dark" ? 0x211a16 : 0xf5efe4);
  }

  private applyCamera(camera: CameraState): void {
    this.camera.position.set(camera.x, camera.y, camera.z);
    this.camera.fov = camera.fov;
    this.camera.updateProjectionMatrix();
    this.controls.target.set(camera.targetX, camera.targetY, camera.targetZ);
    this.controls.update();
  }

  private readCamera(): CameraState {
    return {
      x: this.camera.position.x,
      y: this.camera.position.y,
      z: this.camera.position.z,
      targetX: this.controls.target.x,
      targetY: this.controls.target.y,
      targetZ: this.controls.target.z,
      fov: this.camera.fov,
    };
  }

  private bindEvents(): void {
    window.addEventListener("resize", () => this.resize());
    this.canvas.addEventListener("pointerdown", (event) => this.handlePointer(event, "BookClicked"));
    this.canvas.addEventListener("dblclick", (event) => this.handlePointer(event, "BookDoubleClicked"));
    this.canvas.addEventListener("pointermove", (event) => this.handleHover(event));
    this.canvas.addEventListener("keydown", (event) => this.handleKeyDown(event));
    this.canvas.addEventListener("webglcontextlost", (event) => {
      event.preventDefault();
      this.post({ type: "WebGl2Status", supported: false });
      this.setStatus("3D rendering is temporarily unavailable; the accessible list remains available.");
    });
    this.canvas.addEventListener("webglcontextrestored", () => {
      this.post({ type: "WebGl2Status", supported: this.isWebGl2Supported() });
      this.rebuildResidentWindow();
    });
    this.canvas.tabIndex = 0;
  }

  private resize(): void {
    const width = Math.max(this.canvas.clientWidth, 1);
    const height = Math.max(this.canvas.clientHeight, 1);
    this.renderer.setSize(width, height, false);
    this.camera.aspect = width / height;
    this.camera.updateProjectionMatrix();
  }

  private handlePointer(event: MouseEvent, type: "BookClicked" | "BookDoubleClicked"): void {
    const index = this.pickBookIndex(event);
    if (index === null) return;
    const book = this.books[index];
    if (book === undefined) return;
    this.setFocusedIndex(index);
    this.post({ type, bookId: book.bookId });
  }

  private handleHover(event: PointerEvent): void {
    const index = this.pickBookIndex(event);
    const book = index === null ? undefined : this.books[index];
    if (index === null || book === undefined) {
      this.hoveredBookId = null;
      return;
    }
    if (book.bookId === this.hoveredBookId) return;
    this.hoveredBookId = book.bookId;
    this.setFocusedIndex(index, false);
    this.post({ type: "BookHovered", bookId: book.bookId });
  }

  private pickBookIndex(event: MouseEvent): number | null {
    if (this.bookMeshes.length === 0) return null;
    const rect = this.canvas.getBoundingClientRect();
    this.pointer.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
    this.pointer.y = -(((event.clientY - rect.top) / rect.height) * 2 - 1);
    this.raycaster.setFromCamera(this.pointer, this.camera);
    const hit = this.raycaster.intersectObjects(this.bookMeshes, false)[0];
    return hit?.object.userData.bookIndex ?? null;
  }

  private handleKeyDown(event: KeyboardEvent): void {
    if (this.books.length === 0) return;
    if (event.key === "ArrowRight" || event.key === "ArrowDown") {
      this.setFocusedIndex((this.focusedIndex + 1) % this.books.length);
      event.preventDefault();
      return;
    }
    if (event.key === "ArrowLeft" || event.key === "ArrowUp") {
      this.setFocusedIndex((this.focusedIndex - 1 + this.books.length) % this.books.length);
      event.preventDefault();
      return;
    }
    if (event.key === "Enter") {
      const book = this.books[this.focusedIndex];
      if (book !== undefined) this.post({ type: "BookDoubleClicked", bookId: book.bookId });
      event.preventDefault();
    }
  }

  private postCameraChanged(): void {
    this.post({ type: "CameraChanged", camera: this.readCamera() });
  }

  private recordFrame(timestamp: number): void {
    if (this.performanceWindowStartedAt === 0) {
      this.performanceWindowStartedAt = timestamp;
      this.lastFrameTimestamp = timestamp;
      return;
    }

    this.frameSamples.push(Math.max(0, timestamp - this.lastFrameTimestamp));
    this.lastFrameTimestamp = timestamp;
    const elapsedMs = timestamp - this.performanceWindowStartedAt;
    if (elapsedMs < 2_000) return;

    const samples = [...this.frameSamples].sort((a, b) => a - b);
    const p95FrameTimeMs = samples[Math.min(samples.length - 1, Math.floor(samples.length * 0.95))] ?? 0;
    const averageFps = samples.length / elapsedMs * 1_000;
    this.post({
      type: "PerformanceMetrics",
      averageFps,
      frameTimeMs: p95FrameTimeMs,
      drawCalls: this.renderer.info.render.calls,
      sceneBookCount: this.books.length,
      residentBookCount: this.bookMeshes.length,
      reducedMotion: this.reducedMotion,
    });
    if (averageFps < 55 && timestamp - this.lastPerformanceWarningAt > 5_000) {
      this.lastPerformanceWarningAt = timestamp;
      this.post({ type: "PerformanceWarning", averageFps });
    }
    this.frameSamples = [];
    this.performanceWindowStartedAt = timestamp;
  }

  private post(message: InboundMessage): void {
    const serialized = JSON.stringify({ ...message, version: BRIDGE_PROTOCOL_VERSION });
    if (window.chrome?.webview !== undefined) {
      window.chrome.webview.postMessage(serialized);
      return;
    }
    window.webkit?.messageHandlers?.ogma?.postMessage(serialized);
  }

  private isWebGl2Supported(): boolean {
    return document.createElement("canvas").getContext("webgl2") !== null;
  }

  private clearScene(): void {
    this.clearBookMeshes();
    for (const shelf of this.shelfMeshes.splice(0)) {
      shelf.material.dispose();
      this.sceneRoot.remove(shelf);
    }
  }

  private clearBookMeshes(): void {
    for (const mesh of this.bookMeshes.splice(0)) {
      mesh.material.map?.dispose();
      mesh.material.dispose();
      this.sceneRoot.remove(mesh);
    }
  }

  private buildShelves(): void {
    const firstRow = Math.floor(this.residentStart / SHELF_COLUMNS);
    const rowCount = Math.max(1, Math.ceil(Math.max(this.residentEnd - this.residentStart, 1) / SHELF_COLUMNS));
    const material = new THREE.MeshStandardMaterial({ color: 0x492d22, roughness: 0.82, metalness: 0.02 });
    for (let row = 0; row < rowCount; row++) {
      const shelf: BookMesh = new THREE.Mesh(this.shelfGeometry, material.clone());
      shelf.position.set(0, -(firstRow + row) * SHELF_ROW_HEIGHT - 0.115, 0.02);
      this.shelfMeshes.push(shelf);
      this.sceneRoot.add(shelf);
    }
    material.dispose();
  }

  private isSafeBook(book: BookSceneItem | null | undefined): book is BookSceneItem {
    return book !== null && book !== undefined &&
      typeof book.bookId === "string" && book.bookId.length > 0 && book.bookId.length <= 128 &&
      typeof book.title === "string" && book.title.length <= 160 &&
      typeof book.author === "string" && book.author.length <= 120 &&
      typeof book.spineUri === "string" && book.spineUri.length <= 512;
  }

  private fallbackColor(bookId: string): THREE.Color {
    let hash = 0;
    for (let index = 0; index < bookId.length; index++) hash = (hash * 31 + bookId.charCodeAt(index)) >>> 0;
    return new THREE.Color(`hsl(${hash % 360}, 42%, 38%)`);
  }

  private applySpineTexture(mesh: BookMesh, book: BookSceneItem): void {
    const fallback = document.createElement("canvas");
    fallback.width = 256;
    fallback.height = 512;
    const context = fallback.getContext("2d");
    if (context === null) return;
    context.fillStyle = `#${this.fallbackColor(book.bookId).getHexString()}`;
    context.fillRect(0, 0, fallback.width, fallback.height);
    context.fillStyle = "#f5efe4";
    context.font = "bold 22px Georgia";
    context.textAlign = "center";
    context.save();
    context.translate(fallback.width / 2, fallback.height / 2);
    context.rotate(-Math.PI / 2);
    context.fillText(book.title.slice(0, 34), 0, -4);
    context.font = "16px Georgia";
    context.fillText(book.author.slice(0, 34), 0, 22);
    context.restore();
    const fallbackTexture = new THREE.Texture(fallback);
    fallbackTexture.colorSpace = THREE.SRGBColorSpace;
    fallbackTexture.needsUpdate = true;
    mesh.material.map = fallbackTexture;
    mesh.material.needsUpdate = true;
    if (typeof Image === "undefined" || !book.spineUri.startsWith("ogma://assets/")) return;
    const image = new Image();
    image.onload = () => {
      const texture = new THREE.Texture(image);
      texture.colorSpace = THREE.SRGBColorSpace;
      texture.needsUpdate = true;
      mesh.material.map?.dispose();
      mesh.material.map = texture;
      mesh.material.needsUpdate = true;
    };
    image.onerror = () => undefined;
    image.src = book.spineUri;
  }

  private setFocusedIndex(index: number, announce = true, moveCamera = true): void {
    if (this.books.length === 0) return;
    this.focusedIndex = Math.max(0, Math.min(index, this.books.length - 1));
    if (this.focusedIndex < this.residentStart || this.focusedIndex >= this.residentEnd) this.rebuildResidentWindow();
    this.applyFocusScale();
    const focusedBook = this.books[this.focusedIndex];
    if (moveCamera) this.focusCamera(this.focusedIndex);
    if (announce && focusedBook !== undefined) this.post({ type: "BookHovered", bookId: focusedBook.bookId });
    if (focusedBook !== undefined) this.setStatus(`${focusedBook.title} — ${focusedBook.author}`);
  }

  private applyFocusScale(): void {
    for (const mesh of this.bookMeshes) {
      const focused = mesh.userData.bookIndex === this.focusedIndex;
      mesh.scale.setScalar(focused ? 1.08 : 1);
    }
  }

  private focusCamera(index: number): void {
    const target = this.layout === "shelf" ? this.shelfPosition(index) : this.gridPosition(index);
    const deltaX = target.x - this.controls.target.x;
    const deltaY = target.y - this.controls.target.y;
    this.controls.target.copy(target);
    this.camera.position.x += deltaX;
    this.camera.position.y += deltaY;
    this.controls.update();
  }

  private setStatus(text: string): void {
    const status = document.getElementById("shelf3d-status");
    if (status !== null) status.textContent = text.slice(0, 240);
  }
}

export function initializeShelf3D(canvas: HTMLCanvasElement): Shelf3DScene {
  const scene = new Shelf3DScene(canvas);
  scene.start();
  return scene;
}
