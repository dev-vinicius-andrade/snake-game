import { shallowRef } from "vue";
import { describe, expect, it, vi } from "vitest";
import type { JoinAssignment } from "@/types/game";
import type { KeyEventTarget } from "./useKeyboardControls";
import { useSnakeSession } from "./useSnakeSession";

const assignment: JoinAssignment = {
  gameServerUrl: "https://game.example.com/game",
  roomId: "55555555-5555-5555-5555-555555555555",
  nickname: "ana",
};

class FakeTarget implements KeyEventTarget {
  private readonly listeners = new Map<string, Set<(event: any) => void>>();
  addEventListener(type: string, listener: (event: any) => void) {
    this.listeners.set(type, (this.listeners.get(type) ?? new Set()).add(listener));
  }
  removeEventListener(type: string, listener: (event: any) => void) {
    this.listeners.get(type)?.delete(listener);
  }
  key(type: "keydown" | "keyup", code: string) {
    const event = { code, repeat: false, preventDefault: () => undefined };
    this.listeners.get(type)?.forEach((listener) => listener(event));
  }
}

function setup(options: { join?: () => Promise<JoinAssignment>; connect?: () => Promise<void>; autoFire?: () => boolean } = {}) {
  const joiner = {
    join: vi.fn(options.join ?? (async () => assignment)),
    cancel: vi.fn(async () => undefined),
  };
  const death = shallowRef<{ score: number } | null>(null);
  const roomClosed = shallowRef(false);
  const game = {
    death,
    roomClosed,
    connect: vi.fn(options.connect ?? (async () => undefined)),
    changeDirection: vi.fn(),
    shoot: vi.fn(),
    boost: vi.fn(),
    steer: vi.fn(),
    leave: vi.fn(async () => undefined),
  };
  const keyboardTarget = new FakeTarget();
  const clock = { now: 0 };
  const session = useSnakeSession({ joiner, game, keyboardTarget, now: () => clock.now, autoFire: options.autoFire });
  return { joiner, game, death, roomClosed, keyboardTarget, session, clock };
}

describe("useSnakeSession room closed", () => {
  it("ends the session with a message when the room is closed", async () => {
    const { roomClosed, session } = setup();
    await session.start("ana");

    roomClosed.value = true;

    expect(session.phase.value).toBe("error");
    expect(session.error.value).toMatch(/closed/i);
  });

  it("ignores a closed room when not playing", async () => {
    const { roomClosed, session } = setup();

    roomClosed.value = true;

    expect(session.phase.value).toBe("idle");
  });
});

describe("useSnakeSession.start", () => {
  it("joins through the manager, then connects to the game server", async () => {
    const { joiner, game, session } = setup();

    await session.start("  ana ");

    expect(joiner.join).toHaveBeenCalledWith("ana", undefined);
    expect(game.connect).toHaveBeenCalledWith(assignment);
    expect(session.phase.value).toBe("playing");
  });

  it("passes a requested room to the manager", async () => {
    const { joiner, session } = setup();

    await session.start("ana", assignment.roomId);

    expect(joiner.join).toHaveBeenCalledWith("ana", assignment.roomId);
  });

  it("is joining while the manager works", async () => {
    let finish!: (value: JoinAssignment) => void;
    const { session } = setup({ join: () => new Promise((resolve) => (finish = resolve)) });

    const pending = session.start("ana");
    expect(session.phase.value).toBe("joining");
    finish(assignment);
    await pending;

    expect(session.phase.value).toBe("playing");
  });

  it("reports an error when the manager cannot place the player", async () => {
    const { game, session } = setup({
      join: async () => {
        throw new Error("no server");
      },
    });

    await session.start("ana");

    expect(session.phase.value).toBe("error");
    expect(session.error.value).toBe("no server");
    expect(game.connect).not.toHaveBeenCalled();
  });

  it("reports an error when the game server refuses the connection", async () => {
    const { session } = setup({
      connect: async () => {
        throw new Error("room full");
      },
    });

    await session.start("ana");

    expect(session.phase.value).toBe("error");
    expect(session.error.value).toBe("room full");
  });

  it("can start again after an error", async () => {
    const join = vi.fn<[], Promise<JoinAssignment>>().mockRejectedValueOnce(new Error("busy")).mockResolvedValue(assignment);
    const { session } = setup({ join });

    await session.start("ana");
    await session.start("ana");

    expect(session.phase.value).toBe("playing");
    expect(session.error.value).toBeNull();
  });
});

describe("useSnakeSession input", () => {
  it("routes intents to the game connection", async () => {
    const { game, session } = setup();
    await session.start("ana");

    session.handleIntent({ type: "direction", direction: "left" });
    session.handleIntent({ type: "shoot" });
    session.handleIntent({ type: "boost", active: true });

    expect(game.changeDirection).toHaveBeenCalledWith("left");
    expect(game.shoot).toHaveBeenCalledTimes(1);
    expect(game.boost).toHaveBeenCalledWith(true);
  });

  it("drives the game from the keyboard once playing", async () => {
    const { game, keyboardTarget, session } = setup();
    await session.start("ana");

    keyboardTarget.key("keydown", "ArrowUp");
    keyboardTarget.key("keydown", "Space");

    expect(game.changeDirection).toHaveBeenCalledWith("up");
    expect(game.shoot).toHaveBeenCalledTimes(1);
  });

  it("ignores the keyboard before the game started", () => {
    const { game, keyboardTarget } = setup();

    keyboardTarget.key("keydown", "ArrowUp");

    expect(game.changeDirection).not.toHaveBeenCalled();
  });

  it("routes steer intents to the game connection", async () => {
    const { game, session } = setup();
    await session.start("ana");

    session.handleIntent({ type: "steer", angle: 1.25 });

    expect(game.steer).toHaveBeenCalledWith(1.25);
  });

  it("ignores steer intents unless playing", () => {
    const { game, session } = setup();

    session.handleIntent({ type: "steer", angle: 1.25 });

    expect(game.steer).not.toHaveBeenCalled();
  });

  it("lets gaze steer until the keyboard sets a direction", async () => {
    const { keyboardTarget, session, clock } = setup();
    await session.start("ana");
    expect(session.allowsGazeSteer(0)).toBe(true);

    clock.now = 1000;
    keyboardTarget.key("keydown", "ArrowUp");

    expect(session.allowsGazeSteer(1500)).toBe(false);
    expect(session.allowsGazeSteer(2000)).toBe(true);
  });

  describe("auto fire", () => {
    async function startedWith(autoFire: boolean) {
      vi.useFakeTimers();
      const { session, game, death } = setup({ autoFire: () => autoFire });
      await session.start("ana");
      return { session, game, death };
    }

    it("keeps shooting while the shoot control is held, when the perk is on", async () => {
      const { session, game } = await startedWith(true);

      session.handleIntent({ type: "shoot" });
      vi.advanceTimersByTime(350);

      expect(game.shoot.mock.calls.length).toBeGreaterThan(3);
      vi.useRealTimers();
    });

    it("stops when the control is let go", async () => {
      const { session, game } = await startedWith(true);
      session.handleIntent({ type: "shoot" });
      vi.advanceTimersByTime(250);

      session.handleIntent({ type: "shootRelease" });
      const before = game.shoot.mock.calls.length;
      vi.advanceTimersByTime(500);

      expect(game.shoot.mock.calls.length).toBe(before);
      vi.useRealTimers();
    });

    it("shoots once per press without the perk", async () => {
      const { session, game } = await startedWith(false);

      session.handleIntent({ type: "shoot" });
      vi.advanceTimersByTime(500);

      expect(game.shoot).toHaveBeenCalledTimes(1);
      vi.useRealTimers();
    });

    it("stops when the snake dies", async () => {
      const { session, game, death } = await startedWith(true);
      session.handleIntent({ type: "shoot" });
      death.value = { score: 3 };

      const before = game.shoot.mock.calls.length;
      vi.advanceTimersByTime(500);

      expect(game.shoot.mock.calls.length).toBe(before);
      vi.useRealTimers();
    });
  });

  it("does not let shooting hold gaze steering back", async () => {
    const { keyboardTarget, session, clock } = setup();
    await session.start("ana");

    clock.now = 1000;
    keyboardTarget.key("keydown", "Space");

    expect(session.allowsGazeSteer(1001)).toBe(true);
  });

  it("ignores intents unless playing", () => {
    const { game, session } = setup();

    session.handleIntent({ type: "shoot" });

    expect(game.shoot).not.toHaveBeenCalled();
  });
});

describe("useSnakeSession death and stop", () => {
  it("becomes dead when the game reports death and stops listening to keys", async () => {
    const { game, death, keyboardTarget, session } = setup();
    await session.start("ana");

    death.value = { score: 9 };
    keyboardTarget.key("keydown", "ArrowUp");

    expect(session.phase.value).toBe("dead");
    expect(game.changeDirection).not.toHaveBeenCalled();
  });

  it("releases boost on death", async () => {
    const { game, death, session } = setup();
    await session.start("ana");
    session.handleIntent({ type: "boost", active: true });

    death.value = { score: 1 };

    expect(game.boost).toHaveBeenLastCalledWith(false);
  });

  it("can play again after dying", async () => {
    const { joiner, game, death, session } = setup();
    await session.start("ana");
    death.value = { score: 2 };
    death.value = null;

    await session.start("ana");

    expect(joiner.join).toHaveBeenCalledTimes(2);
    expect(game.connect).toHaveBeenCalledTimes(2);
    expect(session.phase.value).toBe("playing");
  });

  it("stop cancels the join, leaves the game and goes idle", async () => {
    const { joiner, game, session } = setup();
    await session.start("ana");

    await session.stop();

    expect(joiner.cancel).toHaveBeenCalled();
    expect(game.leave).toHaveBeenCalled();
    expect(session.phase.value).toBe("idle");
  });

  it("stop during joining ends idle, not error", async () => {
    let fail!: (reason: Error) => void;
    const { session } = setup({ join: () => new Promise((_, reject) => (fail = reject)) });

    const pending = session.start("ana");
    await session.stop();
    fail(new Error("Join cancelled"));
    await pending;

    expect(session.phase.value).toBe("idle");
    expect(session.error.value).toBeNull();
  });
});
