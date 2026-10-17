# Eye tracking and aim input

Goal: the snake can be controlled with the eyes, for accessibility. Keyboard stays fully supported.

## Decisions

- Camera gaze with **MediaPipe Face Landmarker** (Apache-2.0). **No WebGazer**: it is GPLv3 and this repo is MIT.
- **Pointer source** for everyone: mouse, touch, and hardware eye trackers that move the system cursor.
- Server turn rate default 360 degrees per second. Keyboard turns stay instant.
- Camera frames are processed in the browser and never uploaded. Camera use is opt-in.

## Pipeline

```
GazeSource (pointer | camera)  ->  stage coordinates 0..1
  -> stageToWorld (room units)  -> OneEuro smoothing
  -> aimStep (dead zone, hysteresis, minimum angle change)  -> steer intent
  -> InputArbiter (keyboard direction wins for 1 s)  -> useGameConnection.steer (30 Hz, 2 degree dead band)
  -> hub `Steer { angle }`  -> server turns the heading at most TurnRateDegreesPerSecond
```

Angles are radians in screen coordinates: 0 points right, positive turns clockwise (y grows downwards), up is -PI/2.
The server and the client use the same convention.

## Status

| Milestone | State |
|-----------|-------|
| 1. Server angle steering (`Steer`, heading, turn rate, shot range) | done |
| 2. Client steer intent, aim steering, One Euro smoothing, arbiter, pointer source, `useGazeControls`, HUD toggle "Mouse aim" | done |
| 3. MediaPipe source: joystick mapping from eye blendshapes, calibration, blink to shoot, far gaze to boost, camera permission and tracking-lost UI | built, **not tried with a real camera** (see below) |

Mouse aim is off by default (a resting hand would steer the snake). The HUD button switches it on and the choice is stored in `localStorage`.

## Open points for milestone 3

- Real webcam quality (light, glasses, latency) can only be judged by trying it.
- Blink to shoot needs a deliberate blink of at least 500 ms so natural blinks do not fire.
- Eye-only play is tiring: keep the keyboard working next to gaze, and consider a single-switch option for shoot and boost.

## Milestone 3: camera gaze (how it works)

Turn it on with the "Eye control" button (bottom right of the game view). Mouse aim and eye control exclude each other.

1. **Camera and model.** `createMediaPipeGazeSource` asks for the camera first (so the permission prompt comes first), then loads
   MediaPipe Face Landmarker on demand (a separate chunk, so nobody else downloads it). It reads the 52 face blendshapes of every video frame.
   The video is not mirrored: MediaPipe names `*Left` and `*Right` from the subject's point of view.
2. **Eye signal.** `extractEyeSignals` turns the eight `eyeLook*` blendshapes into a horizontal and vertical value in -1..1 and the two
   `eyeBlink*` into one blink score. While a blink is closing, the look blendshapes move too, so the gaze is held from just before it.
3. **Calibration.** Five dots (centre, left, right, up, down), about 1.5 s each: the first half second is skipped while the eyes travel,
   then the median of 30 frames is kept. A least squares line per axis maps the eye signal to the stage (a negative gain is fine, so a flipped
   convention calibrates itself). The result is stored in `localStorage` and reused. "Recalibrate" forgets it.
4. **Steering.** The snake turns toward where you look, measured from its own head, like aiming with a mouse. Looking at the snake does
   nothing, wherever it is on the screen. The calibrated gaze point is smoothed (One Euro). A dead zone of 15 % of the smaller side of the
   room around the head means "keep your heading", and a turn needs the gaze to stay in one direction (within about 20 degrees) for 0.3 s,
   so a glance at the leaderboard or at another snake does not steer. Once steering, it follows the gaze without waiting again until the gaze
   returns to the dead zone. A keyboard direction keeps gaze steering out of the way for one second. Without a snake (or on the start page)
   there is nothing to aim from and nothing steers.
5. **Shoot.** A deliberate blink: eyes shut for 0.5 to 2 s, reported when they open. Natural blinks (about 0.1 to 0.4 s) are ignored.
6. **Boost.** Keep looking far from the middle (40 % of the smaller side of the room) for 0.4 s; it ends when the gaze comes back in.
7. **Tracking lost.** After 0.6 s without a face nothing fires, a running boost ends, and the panel says "Face not found". The snake keeps its heading.

## Not verified

- No webcam was available while this was built. The pure parts are covered by specs (blendshape maths, calibration fit, blink, far gaze, tracking,
  frame processing, camera source lifecycle with fakes, the whole `useEyeControl` flow) and the denied-permission path was seen in the browser pane
  (error text shown, mouse aim switched off). Real tracking quality, thresholds and the dead zone and far radius defaults need to be tried and tuned.
- Calibrate on the **start page** ("Eye control" button, bottom right): nothing moves there, and the calibration is saved for the game.
  Turning eye control on in the game with a saved calibration starts steering at once. Recalibrating inside a game still works but the snake keeps moving.
- Head movement changes what the eyes signal. Keep roughly the same position and distance as during calibration, or recalibrate.
- Model and wasm come from Google storage and jsDelivr by default. Set `VITE_FACE_LANDMARKER_MODEL_URL` and `VITE_MEDIAPIPE_WASM_URL`
  (see `.env.example`) to serve them yourself. Camera images are never sent anywhere either way.
- Camera access needs `https` or `localhost`.

## Choosing the camera, and when no face is found

- With **Eye control** on, the panel lists the cameras (real ones first). Pick one and it is reopened at once, the choice is remembered
  in `localStorage`, and calibration is kept. If a saved camera is gone, the browser default is used. Names only appear after the camera
  permission was given, because browsers hide them before that.
- A small mirrored **preview** shows what the camera sees (bottom left). Only the preview is mirrored; the model reads the raw frame.
- A camera that looks virtual (OBS, Snap Camera, NDI, XSplit, ManyCam, Streamlabs, anything with "virtual" in the name) triggers a warning
  when a real camera exists. The default camera of a browser is often a virtual one on machines that stream or record.
- The console prints a line every two seconds while the camera runs:
  `[eye] frames=330 landmarks=0 withFace=0 incomplete=0 skipped=0 missing=- video=640x480 ready=4 brightness=0.02 hint=no face found: ...`
  - `landmarks` is how many frames the model found a face in. `withFace` is how many of those also gave blendshapes. `incomplete` and `missing` name eye blendshapes that were absent.
  - `brightness` near 0 means black frames (wrong or covered camera). `skipped` counts loop runs without a new video frame.
  - `landmarks=0` with a lit, real picture in the preview: improve the light, face the lens, and move closer.
