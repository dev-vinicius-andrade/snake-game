import { onBeforeUnmount, onMounted, type Ref } from "vue";
import { drawFrame, logicalSize, type RenderOptions } from "@/render/pixelRenderer";
import type { GameSnapshot } from "@/types/game";

export interface RenderSource {
  snapshot: GameSnapshot | null;
  options: RenderOptions;
}

/**
 * Redraws the canvas every animation frame from the latest source. The canvas bitmap stays at logical
 * (pixel art) size; CSS scales it up without smoothing.
 */
export function useCanvasRenderer(
  canvas: Ref<HTMLCanvasElement | null>,
  source: () => RenderSource,
  /** Told after each frame is drawn: when, and how long the drawing took. */
  onFrame?: (nowMs: number, drawMs: number) => void
) {
  let frame = 0;

  function tick() {
    frame = requestAnimationFrame(tick);
    const element = canvas.value;
    const { snapshot, options } = source();
    const context = element?.getContext("2d");
    if (!element || !context || !snapshot) return;

    const size = options.view ?? logicalSize(snapshot.room, options.pixelSize);
    if (element.width !== size.width) element.width = size.width;
    if (element.height !== size.height) element.height = size.height;
    context.imageSmoothingEnabled = false;
    const started = performance.now();
    drawFrame(context, snapshot, options);
    const finished = performance.now();
    onFrame?.(finished, finished - started);
  }

  onMounted(() => {
    frame = requestAnimationFrame(tick);
  });
  onBeforeUnmount(() => cancelAnimationFrame(frame));
}
