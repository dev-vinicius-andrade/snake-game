import { resolveFaceLandmarkerUrls } from "@/config/env";
import type { LandmarkerLike } from "./mediapipeGazeSource";

/**
 * Loads MediaPipe Face Landmarker with blendshapes. The library is imported on demand, so players who never turn
 * on camera gaze do not download it. Tries the GPU first and falls back to the CPU.
 */
export async function loadFaceLandmarker(urls = resolveFaceLandmarkerUrls()): Promise<LandmarkerLike> {
  const { FaceLandmarker, FilesetResolver } = await import("@mediapipe/tasks-vision");
  const fileset = await FilesetResolver.forVisionTasks(urls.wasmUrl);
  const create = (delegate: "GPU" | "CPU") =>
    FaceLandmarker.createFromOptions(fileset, {
      baseOptions: { modelAssetPath: urls.modelUrl, delegate },
      runningMode: "VIDEO",
      numFaces: 1,
      outputFaceBlendshapes: true,
      outputFacialTransformationMatrixes: false,
    });

  const landmarker = await create("GPU").catch(() => create("CPU"));
  // The video handed in by the gaze source is an HTMLVideoElement; `VideoLike` only names what the source uses of it.
  return {
    detectForVideo: (video, timestampMs) => landmarker.detectForVideo(video as unknown as HTMLVideoElement, timestampMs),
    close: () => landmarker.close(),
  };
}
