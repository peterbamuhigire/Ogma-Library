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

export type OutboundMessage =
  | Readonly<{ type: "SetScene"; books: readonly BookSceneItem[]; camera: CameraState }>
  | Readonly<{ type: "UpdateBook"; bookId: string; book: BookSceneItem }>
  | Readonly<{ type: "RemoveBook"; bookId: string }>
  | Readonly<{ type: "SetCamera"; camera: CameraState }>
  | Readonly<{ type: "SetTheme"; themeKey: "light" | "dark" }>
  | Readonly<{ type: "SetLayout"; layout: "shelf" | "grid3d" }>;

export type InboundMessage =
  | Readonly<{ type: "BookClicked"; bookId: string }>
  | Readonly<{ type: "BookDoubleClicked"; bookId: string }>
  | Readonly<{ type: "BookHovered"; bookId: string }>
  | Readonly<{ type: "CameraChanged"; camera: CameraState }>
  | Readonly<{ type: "WebGl2Status"; supported: boolean }>
  | Readonly<{ type: "PerformanceWarning"; averageFps: number }>;

export function assertNever(value: never): never {
  throw new Error(`Unhandled shelf3d message: ${JSON.stringify(value)}`);
}
