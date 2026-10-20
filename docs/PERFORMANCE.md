# Measuring smoothness

The game has a frame meter. Open the game with `?perf` in the address (for example `/snake/2d?perf`) or press **F3** in a game to show it
in the bottom left corner. It covers the last 5 seconds and refreshes twice a second. The same numbers are at `window.__snakePerf` in the
browser console: `__snakePerf.summary(performance.now())` gives one line, `__snakePerf.stats(performance.now())` the full object, and
`__snakePerf.reset()` starts over.

| Part of the line | Meaning | What is bad |
| --- | --- | --- |
| `N fps` | frames drawn per second | well under the screen's rate (60, 144) |
| `frame avg/max ms` | time from one frame to the next | max over 50 ms |
| `draw avg/max ms` | time the drawing itself took | avg over 8 ms: drawing is too heavy |
| `drop D long L` | frames the screen could have shown but did not, judged against its own rate; frames over 50 ms | any, in steady play |
| `snap avg +-jitter ms stalls S pts P/M` | time between server snapshots (30 a second is 33 ms), how uneven it is, gaps over 100 ms, body points in the latest snapshot and the most in the window | jitter over 8 ms, any stall, points in the thousands |
| `blocked N x max M ms` | stretches where the browser's main thread was blocked over 50 ms (long tasks), and the worst | any, when `drop` is high: the page itself froze |
| `gc G mem X MB +Y/s` | falls of the memory in use (garbage collections), the memory now, and the least the page uses a second | many collections, or a big `+Y/s`: too much garbage is made each frame |
| `starved X%` | share of frames with no newer snapshot to blend toward, so the world stood still | over a few percent |
| `cam jerk J rev R` | how much the camera's step changes from frame to frame, and how often it turns back on itself | jerk over 1 or many reversals while going straight |
| `work W/frame` | how many times the world was blended per frame drawn | over 1.5: something asks for the camera too often |

Reading it: **drops with a low draw time** mean the browser or the machine is busy elsewhere; **a high draw time** means the drawing is too
heavy (many segments); **jitter, stalls or starved** mean snapshots arrive unevenly (network or server), and that is what shakes the camera.
`stats().hitches` lists the last frames over 50 ms and how long ago each one was. A frame stall that comes with a snapshot gap of the same length is the page freezing, not the network: both stopped together.

Note that a hidden or background tab draws about 1 frame a second.

## Face control (camera) cost

Camera control (MediaPipe Face Landmarker) is the heaviest thing the page can run, so it is kept cheap:

- It runs on a **worker thread** when the browser can (module workers and `createImageBitmap`), so finding the face does not hold up drawing the game. If the worker cannot start, it runs on the page as before. To compare, run `localStorage.setItem('snake.faceWorker', 'off')` and reload; `'on'` or removing it goes back.
- It **looks less often when it is costly**: detection may take about a quarter of the time, so a detection that takes 10 ms runs every 40 ms and one that takes 25 ms every 100 ms (never faster than 30 a second, never slower than 10). One frame is with the worker at a time; frames that arrive meanwhile are left out.
- The head mode asks the camera for **320x240**, the eyes mode for 640x480 (the eyes are small in the frame).
- The head's transformation matrix is only computed in head mode.
- Nothing is detected while the page is hidden.
- The meter shows `face avg/max ms N Hz`: how long each detection takes and how many run a second.

