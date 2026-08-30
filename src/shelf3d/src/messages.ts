export const BRIDGE_PROTOCOL_VERSION = "shelf3d-v1" as const;

export type CameraState = Readonly<{
  x: number;
  y: number;
  z: number;
  targetX: number;
  targetY: number;
  targetZ: number;
  fov: number;
}>;

export type BookSceneItem = Readonly<{
  bookId: string;
  title: string;
  author: string;
  spineUri: string;
  coverUri?: string | null;
}>;

type Versioned = Readonly<{ version?: typeof BRIDGE_PROTOCOL_VERSION }>;

export type OutboundMessage =
  | (Versioned & Readonly<{ type: "SetScene"; books: readonly BookSceneItem[]; camera: CameraState }>)
  | (Versioned & Readonly<{ type: "UpdateBook"; bookId: string; book: BookSceneItem }>)
  | (Versioned & Readonly<{ type: "RemoveBook"; bookId: string }>)
  | (Versioned & Readonly<{ type: "SetCamera"; camera: CameraState }>)
  | (Versioned & Readonly<{ type: "SetTheme"; themeKey: "light" | "dark" }>)
  | (Versioned & Readonly<{ type: "SetLayout"; layout: "shelf" | "grid3d" }>);

export type InboundMessage =
  | Readonly<{ type: "BookClicked"; bookId: string }>
  | Readonly<{ type: "BookDoubleClicked"; bookId: string }>
  | Readonly<{ type: "BookHovered"; bookId: string }>
  | Readonly<{ type: "CameraChanged"; camera: CameraState }>
  | Readonly<{ type: "WebGl2Status"; supported: boolean }>
  | Readonly<{ type: "PerformanceWarning"; averageFps: number }>
  | Readonly<{
      type: "PerformanceMetrics";
      averageFps: number;
      frameTimeMs: number;
      drawCalls: number;
      sceneBookCount: number;
      residentBookCount: number;
      reducedMotion: boolean;
    }>;

export function assertNever(value: never): never {
  throw new Error(`Unhandled shelf3d message: ${JSON.stringify(value)}`);
}
