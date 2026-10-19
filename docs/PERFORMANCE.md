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
| `starved X%` | share of frames with no newer snapshot to blend toward, so the world stood still | over a few percent |
| `cam jerk J rev R` | how much the camera's step changes from frame to frame, and how often it turns back on itself | jerk over 1 or many reversals while going straight |
| `work W/frame` | how many times the world was blended per frame drawn | over 1.5: something asks for the camera too often |

Reading it: **drops with a low draw time** mean the browser or the machine is busy elsewhere; **a high draw time** means the drawing is too
heavy (many segments); **jitter, stalls or starved** mean snapshots arrive unevenly (network or server), and that is what shakes the camera.
Note that a hidden or background tab draws about 1 frame a second.
