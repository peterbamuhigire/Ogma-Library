import type { OutboundMessage } from "./messages";
import { initializeShelf3D, type Shelf3DScene } from "./scene";

type Shelf3DHostApi = Readonly<{
  postMessage: (json: string) => void;
  focus: () => void;
}>;

declare global {
  interface Window {
    ogmaShelf3D?: Shelf3DHostApi;
  }
}

function parseOutboundMessage(json: string): OutboundMessage | null {
  try {
    const parsed = JSON.parse(json) as Partial<OutboundMessage>;
    return typeof parsed.type === "string" ? (parsed as OutboundMessage) : null;
  } catch {
    return null;
  }
}

function boot(): void {
  const canvas = document.getElementById("shelf3d-canvas");
  if (!(canvas instanceof HTMLCanvasElement)) {
    throw new Error("The shelf3d canvas was not found.");
  }

  const scene: Shelf3DScene = initializeShelf3D(canvas);
  window.ogmaShelf3D = {
    postMessage(json: string): void {
      const message = parseOutboundMessage(json);
      if (message !== null) {
        scene.handleMessage(message);
      }
    },
    focus(): void {
      canvas.focus();
    },
  };
}

if (document.readyState === "loading") {
  document.addEventListener("DOMContentLoaded", boot, { once: true });
} else {
  boot();
}
