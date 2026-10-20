type Listener = (atMs: number, durationMs: number) => void;

const listeners = new Set<Listener>();

/** Hears how long each face detection took. Returns the way to stop hearing. */
export function onDetectTiming(listener: Listener): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

/** The camera pipeline says how long a face detection took; the frame meter shows it next to the drawing time. */
export function reportDetectTiming(atMs: number, durationMs: number) {
  listeners.forEach((listener) => listener(atMs, durationMs));
}
