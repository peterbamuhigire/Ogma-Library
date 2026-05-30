# Phase 14 — Skills & Slash Commands

---

## Primary skills

### `frontend-design:frontend-design`

- **Tasks:** P14-WP7-T1..T7 — `Bookshelf3DView` Avalonia shell.
- **Why:** The 3D view must integrate seamlessly with the Avalonia design system —
  toolbar, fallback overlay, theme sync — all using the premium icon and token
  system from Phase 03.
- **Artifact:** `Bookshelf3DView.axaml` following `AVALONIA-STANDARDS.md`
  conventions; all design-token references consistent with Phase 03 system.

### `typescript-effective` / `typescript-mastery`

- **Tasks:** P14-WP3-T3, P14-WP5-T1..T11 — TypeScript message types and Three.js
  scene.
- **Why:** The bridge message types must be typed and discriminated in both C# and
  TypeScript; the Three.js scene requires modern TypeScript with strict null checks
  and correct module resolution.
- **Artifact:** `src/shelf3d/src/messages.ts`, `src/shelf3d/src/scene.ts` with
  strict TypeScript; `tsconfig.json`; bundled `shelf3d.js`.

### `frontend-ux:frontend-performance`

- **Tasks:** P14-WP5-T2, P14-WP9-T1..T4 — InstancedMesh design, FPS benchmark.
- **Why:** Achieving 60 FPS with 500 book objects requires specific Three.js
  optimizations: InstancedMesh, texture atlas packing, draw-call minimization.
- **Artifact:** InstancedMesh implementation with documented design choices;
  FPS benchmark fixture and baseline JSON.

### `frontend-ux:motion-design`

- **Tasks:** P14-WP5-T3, P14-WP5-T4 — layout transitions.
- **Why:** The shelf-to-grid layout switch should animate smoothly; book hover
  should have a subtle lift effect. Motion must not exceed 200 ms for transitions
  per the design system, and must respect `prefers-reduced-motion`.
- **Artifact:** Animated layout transition using `GSAP` or `Tween.js`; reduced-
  motion media query handled in JS.

### `architecture:validation-contract`

- **Tasks:** P14-WP3-T1..T7 — typed bridge message schema and SI-3 validator.
- **Why:** The bridge is a trust boundary (SI-3); the typed message schema and
  `InboundMessageValidator` implement a formal contract between the JS world and
  the C# domain.
- **Artifact:** `InboundMessageValidator.cs` with unit tests covering all message
  types; schema documentation in code comments.

### `frontend-ux:image-compression`

- **Tasks:** P14-WP4-T1, P14-WP5-T2 — spine PNG optimization for atlas.
- **Why:** 500 spine textures packed into a texture atlas must be small enough to
  load within the 3D scene initialization time budget. Spine PNGs should be
  quantized where possible without visible quality loss.
- **Artifact:** `SpineTextureGenerator` outputs optimized PNGs (palette/quantize
  options from SkiaSharp's `SKEncodedImageFormat.Png`); atlas packing strategy
  documented in `src/shelf3d/src/atlas.ts`.

### `security-scanning:security-hardening`

- **Tasks:** P14-WP2-T1, P14-WP2-T5 — `ogma://` path-traversal guard.
- **Why:** The scheme handler is a security boundary; a path-traversal attack
  could expose arbitrary files. The skill provides the checklist for implementing
  and testing a strict containment guard.
- **Artifact:** `OgmaSchemeHandler.cs` with inline comment tracing the
  containment check to SI-3; `SchemeHandlerTest_PathTraversal_Returns403` test.

---

## Always-on skills

| Skill | How applied |
| --- | --- |
| `superpowers:test-driven-development` | SI-3 validator and scheme handler tests written before implementations |
| `superpowers:verification-before-completion` | FPS benchmark + dotnet test + TypeScript build all run before claiming WP done |
| `superpowers:requesting-code-review` + `/code-review --effort high` | End of each WP; WP8 and WP9 at high effort |
| `superpowers:systematic-debugging` | Any FPS regression or bridge latency failure |
| `documentation-generation:docs-architect` | ADR-0003 and HLD §6 updated after WP1 and WP5 |

---

## Slash commands

| Command | When |
| --- | --- |
| `/code-review --effort high` | WP3 (bridge validation), WP2 (scheme handler security), WP8 |
| `/security-review` | WP2 and WP3 — `ogma://` scheme and SI-3 validation boundary |
| `/run` | After WP7: run app on Windows and macOS to confirm 3D view loads, spine textures visible, keyboard navigation works |
| `/verify` | After WP9: confirm FPS benchmark passes on reference hardware |
| `/simplify` | After WP5 Three.js scene and WP6 ViewModel |
