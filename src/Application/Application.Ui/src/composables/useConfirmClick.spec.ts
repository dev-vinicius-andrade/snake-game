import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { useConfirmClick } from "./useConfirmClick";

describe("useConfirmClick", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });
  afterEach(() => {
    vi.useRealTimers();
  });

  it("does nothing on the first click but asks to be clicked again", () => {
    const action = vi.fn();
    const confirm = useConfirmClick(3000);

    confirm.click(action);

    expect(action).not.toHaveBeenCalled();
    expect(confirm.armed.value).toBe(true);
  });

  it("does the action on the second click and is ready for the next time", () => {
    const action = vi.fn();
    const confirm = useConfirmClick(3000);

    confirm.click(action);
    confirm.click(action);

    expect(action).toHaveBeenCalledTimes(1);
    expect(confirm.armed.value).toBe(false);
  });

  it("forgets the first click after the wait, so a stray click later does nothing", () => {
    const action = vi.fn();
    const confirm = useConfirmClick(3000);

    confirm.click(action);
    vi.advanceTimersByTime(3001);
    expect(confirm.armed.value).toBe(false);
    confirm.click(action);

    expect(action).not.toHaveBeenCalled();
    expect(confirm.armed.value).toBe(true);
  });

  it("can be disarmed by hand", () => {
    const action = vi.fn();
    const confirm = useConfirmClick(3000);
    confirm.click(action);

    confirm.cancel();
    confirm.click(action);

    expect(action).not.toHaveBeenCalled();
  });
});
