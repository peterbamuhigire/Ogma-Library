import * as THREE from "three";
import { OrbitControls } from "three/examples/jsm/controls/OrbitControls.js";
import type { BookSceneItem, CameraState, InboundMessage, OutboundMessage } from "./messages";
import { assertNever } from "./messages";

type WebKitBridge = Readonly<{
  messageHandlers?: Readonly<{
    ogma?: Readonly<{
      postMessage: (message: string) => void;
    }>;
  }>;
}>;

type ChromiumBridge = Readonly<{
  webview?: Readonly<{
    postMessage: (message: string) => void;
  }>;
}>;

declare global {
  interface Window {
    chrome?: ChromiumBridge;
    webkit?: WebKitBridge;
  }
}

const BOOK_WIDTH = 0.025;
const BOOK_HEIGHT = 0.18;
const BOOK_DEPTH = 0.13;
const SHELF_COLUMNS = 50;

export class Shelf3DScene {
  private readonly scene = new THREE.Scene();
  private readonly camera = new THREE.PerspectiveCamera(45, 1, 0.01, 100);
  private readonly raycaster = new THREE.Raycaster();
  private readonly pointer = new THREE.Vector2();
  private readonly renderer: THREE.WebGLRenderer;
  private readonly controls: OrbitControls;
  private readonly geometry = new THREE.BoxGeometry(BOOK_WIDTH, BOOK_HEIGHT, BOOK_DEPTH);
  private mesh: THREE.InstancedMesh | null = null;
  private books: readonly BookSceneItem[] = [];
  private layout: "shelf" | "grid3d" = "shelf";
  private focusedIndex = 0;
  private hoveredBookId: string | null = null;

  public constructor(private readonly canvas: HTMLCanvasElement) {
    this.renderer = new THREE.WebGLRenderer({ canvas, antialias: true, powerPreference: "high-performance" });
    this.controls = new OrbitControls(this.camera, canvas);
    this.controls.enableDamping = true;
    this.controls.addEventListener("change", () => this.postCameraChanged());

    this.scene.add(new THREE.HemisphereLight(0xffffff, 0x4b3426, 1.2));
    this.camera.position.set(0, 0.45, 1.4);
    this.camera.lookAt(0, 0, 0);
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
        this.applyLayout();
        return;
      default:
        assertNever(message);
    }
  }

  public start(): void {
    const tick = (): void => {
      this.controls.update();
      this.renderer.render(this.scene, this.camera);
      requestAnimationFrame(tick);
    };

    requestAnimationFrame(tick);
  }

  private setScene(books: readonly BookSceneItem[], camera: CameraState): void {
    this.books = books;
    this.focusedIndex = 0;
    this.mesh?.removeFromParent();
    this.mesh?.dispose();

    const material = new THREE.MeshStandardMaterial({ color: 0x8b5a3c, roughness: 0.8, metalness: 0.05 });
    this.mesh = new THREE.InstancedMesh(this.geometry, material, books.length);
    this.mesh.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
    this.scene.add(this.mesh);
    this.applyLayout();
    this.applyCamera(camera);
  }

  private updateBook(bookId: string, book: BookSceneItem): void {
    this.books = this.books.map((existing) => (existing.bookId === bookId ? book : existing));
    this.applyLayout();
  }

  private removeBook(bookId: string): void {
    const remaining = this.books.filter((book) => book.bookId !== bookId);
    this.setScene(remaining, this.readCamera());
  }

  private applyLayout(): void {
    if (this.mesh === null) {
      return;
    }

    const matrix = new THREE.Matrix4();
    for (let index = 0; index < this.books.length; index++) {
      const position = this.layout === "shelf" ? this.shelfPosition(index) : this.gridPosition(index);
      const rotation = this.layout === "shelf" ? ((index % 5) - 2) * 0.015 : 0;
      matrix.compose(position, new THREE.Quaternion().setFromEuler(new THREE.Euler(0, rotation, 0)), new THREE.Vector3(1, 1, 1));
      this.mesh.setMatrixAt(index, matrix);
    }

    this.mesh.count = this.books.length;
    this.mesh.instanceMatrix.needsUpdate = true;
  }

  private shelfPosition(index: number): THREE.Vector3 {
    const column = index % SHELF_COLUMNS;
    const row = Math.floor(index / SHELF_COLUMNS);
    return new THREE.Vector3((column - (SHELF_COLUMNS / 2)) * 0.03, -row * 0.24, 0);
  }

  private gridPosition(index: number): THREE.Vector3 {
    const side = Math.ceil(Math.sqrt(Math.max(this.books.length, 1)));
    const column = index % side;
    const row = Math.floor(index / side);
    return new THREE.Vector3((column - (side / 2)) * 0.05, 0, (row - (side / 2)) * 0.08);
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
    if (index === null) {
      return;
    }

    const book = this.books[index];
    if (book === undefined) {
      return;
    }

    this.focusedIndex = index;
    this.post({ type, bookId: book.bookId });
  }

  private handleHover(event: PointerEvent): void {
    const index = this.pickBookIndex(event);
    const book = index === null ? undefined : this.books[index];
    if (book === undefined || book.bookId === this.hoveredBookId) {
      return;
    }

    this.hoveredBookId = book.bookId;
    this.post({ type: "BookHovered", bookId: book.bookId });
  }

  private pickBookIndex(event: MouseEvent): number | null {
    if (this.mesh === null) {
      return null;
    }

    const rect = this.canvas.getBoundingClientRect();
    this.pointer.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
    this.pointer.y = -(((event.clientY - rect.top) / rect.height) * 2 - 1);
    this.raycaster.setFromCamera(this.pointer, this.camera);
    const hit = this.raycaster.intersectObject(this.mesh, false)[0];
    return hit?.instanceId ?? null;
  }

  private handleKeyDown(event: KeyboardEvent): void {
    if (this.books.length === 0) {
      return;
    }

    if (event.key === "ArrowRight" || event.key === "ArrowDown") {
      this.focusedIndex = (this.focusedIndex + 1) % this.books.length;
      event.preventDefault();
      return;
    }

    if (event.key === "ArrowLeft" || event.key === "ArrowUp") {
      this.focusedIndex = (this.focusedIndex - 1 + this.books.length) % this.books.length;
      event.preventDefault();
      return;
    }

    if (event.key === "Enter") {
      const book = this.books[this.focusedIndex];
      if (book !== undefined) {
        this.post({ type: "BookDoubleClicked", bookId: book.bookId });
      }

      event.preventDefault();
    }
  }

  private postCameraChanged(): void {
    this.post({ type: "CameraChanged", camera: this.readCamera() });
  }

  private post(message: InboundMessage): void {
    const serialized = JSON.stringify(message);
    if (window.chrome?.webview !== undefined) {
      window.chrome.webview.postMessage(serialized);
      return;
    }

    window.webkit?.messageHandlers?.ogma?.postMessage(serialized);
  }

  private isWebGl2Supported(): boolean {
    const context = document.createElement("canvas").getContext("webgl2");
    return context !== null;
  }
}

export function initializeShelf3D(canvas: HTMLCanvasElement): Shelf3DScene {
  const scene = new Shelf3DScene(canvas);
  scene.start();
  return scene;
}
