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
| 3. MediaPipe source: joystick mapping from eye blendshapes, neutral and gain calibration, blink to shoot, far gaze to boost, camera permission and tracking-lost UI | next |

Mouse aim is off by default (a resting hand would steer the snake). The HUD button switches it on and the choice is stored in `localStorage`.

## Open points for milestone 3

- Real webcam quality (light, glasses, latency) can only be judged by trying it.
- Blink to shoot needs a deliberate blink of at least 500 ms so natural blinks do not fire.
- Eye-only play is tiring: keep the keyboard working next to gaze, and consider a single-switch option for shoot and boost.
