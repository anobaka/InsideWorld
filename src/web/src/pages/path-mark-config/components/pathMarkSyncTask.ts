import type { BTask } from "@/core/models/BTask";

import { BTaskStatus } from "@/sdk/constants";

export const PATH_MARK_SYNC_TASK_ID = "SyncPathMarks";

const activeStatuses = new Set<BTaskStatus>([
  BTaskStatus.NotStarted,
  BTaskStatus.Running,
  BTaskStatus.Paused,
  BTaskStatus.Cancelling,
  BTaskStatus.Pausing,
  BTaskStatus.Resuming,
]);

export const getPathMarkSyncTask = (tasks?: BTask[]): BTask | undefined =>
  tasks?.find((task) => task.id === PATH_MARK_SYNC_TASK_ID);

export const isPathMarkSyncTaskActive = (task?: BTask): boolean =>
  task != null && activeStatuses.has(task.status);

/**
 * Returns the real overall progress exposed by the single global path-mark sync task.
 * Zero/missing progress is represented as undefined so callers can render an indeterminate
 * indicator instead of implying that an individual mark has its own measurable task.
 */
export const getPathMarkSyncProgress = (task?: BTask): number | undefined => {
  if (!isPathMarkSyncTaskActive(task) || task?.percentage == null || task.percentage <= 0) {
    return undefined;
  }

  return Math.min(100, Math.max(0, task.percentage));
};

export const didPathMarkSyncTaskComplete = (
  previousStatus?: BTaskStatus,
  currentStatus?: BTaskStatus,
): boolean =>
  previousStatus != null &&
  activeStatuses.has(previousStatus) &&
  (currentStatus == null || currentStatus === BTaskStatus.Completed);
