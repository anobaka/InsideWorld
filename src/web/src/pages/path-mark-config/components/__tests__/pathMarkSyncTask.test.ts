import type { BTask } from "@/core/models/BTask";

import { describe, expect, it } from "vitest";

import {
  didPathMarkSyncTaskComplete,
  getPathMarkSyncProgress,
  getPathMarkSyncTask,
  isPathMarkSyncTaskActive,
  PATH_MARK_SYNC_TASK_ID,
} from "../pathMarkSyncTask";

import { BTaskStatus } from "@/sdk/constants";

const task = (id: string, status: BTaskStatus, percentage?: number) =>
  ({ id, status, percentage }) as BTask;

describe("path-mark synchronization task", () => {
  it("uses only the global task and ignores legacy per-mark task ids", () => {
    const legacyTask = task("SyncPathMark_42", BTaskStatus.Running, 75);
    const globalTask = task(PATH_MARK_SYNC_TASK_ID, BTaskStatus.Running, 90);

    expect(getPathMarkSyncTask([legacyTask])).toBeUndefined();
    expect(getPathMarkSyncTask([legacyTask, globalTask])).toBe(globalTask);
  });

  it("treats every non-terminal global task status as active", () => {
    expect(isPathMarkSyncTaskActive(task(PATH_MARK_SYNC_TASK_ID, BTaskStatus.NotStarted))).toBe(
      true,
    );
    expect(isPathMarkSyncTaskActive(task(PATH_MARK_SYNC_TASK_ID, BTaskStatus.Paused))).toBe(true);
    expect(isPathMarkSyncTaskActive(task(PATH_MARK_SYNC_TASK_ID, BTaskStatus.Cancelling))).toBe(
      true,
    );
    expect(isPathMarkSyncTaskActive(task(PATH_MARK_SYNC_TASK_ID, BTaskStatus.Completed))).toBe(
      false,
    );
  });

  it("uses indeterminate progress until the global task reports a positive percentage", () => {
    expect(
      getPathMarkSyncProgress(task(PATH_MARK_SYNC_TASK_ID, BTaskStatus.Running)),
    ).toBeUndefined();
    expect(
      getPathMarkSyncProgress(task(PATH_MARK_SYNC_TASK_ID, BTaskStatus.Running, 0)),
    ).toBeUndefined();
    expect(getPathMarkSyncProgress(task(PATH_MARK_SYNC_TASK_ID, BTaskStatus.Running, 87))).toBe(87);
    expect(
      getPathMarkSyncProgress(task(PATH_MARK_SYNC_TASK_ID, BTaskStatus.Completed, 100)),
    ).toBeUndefined();
  });

  it("detects completion or cleanup only after an active task", () => {
    expect(didPathMarkSyncTaskComplete(BTaskStatus.Running, BTaskStatus.Completed)).toBe(true);
    expect(didPathMarkSyncTaskComplete(BTaskStatus.Paused, undefined)).toBe(true);
    expect(didPathMarkSyncTaskComplete(BTaskStatus.Completed, undefined)).toBe(false);
    expect(didPathMarkSyncTaskComplete(undefined, BTaskStatus.Completed)).toBe(false);
  });
});
