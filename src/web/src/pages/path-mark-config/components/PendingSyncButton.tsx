import type { BTaskStatus } from "@/sdk/constants";

import { useState, useEffect, useCallback, useImperativeHandle, forwardRef, useRef } from "react";
import { useTranslation } from "react-i18next";
import { AiOutlineSync } from "react-icons/ai";

import PendingSyncListModal from "./PendingSyncListModal";
import { didPathMarkSyncTaskComplete, getPathMarkSyncTask } from "./pathMarkSyncTask";

import { Button, Badge } from "@/components/bakaui";
import BApi from "@/sdk/BApi";
import { useBTasksStore } from "@/stores/bTasks";

export interface PendingSyncButtonRef {
  refresh: () => void;
}

interface PendingSyncButtonProps {
  buttonSize?: "sm" | "md";
  className?: string;
  onSyncComplete?: () => void;
}

const PendingSyncButton = forwardRef<PendingSyncButtonRef, PendingSyncButtonProps>(
  ({ buttonSize = "sm", className, onSyncComplete }, ref) => {
    const { t } = useTranslation();

    const [pendingSyncCount, setPendingSyncCount] = useState(0);
    const [showPendingSyncModal, setShowPendingSyncModal] = useState(false);

    // Track previous task statuses to detect completion
    const prevTaskStatusRef = useRef<BTaskStatus>();

    // Watch BTask store for PathMark sync tasks
    const bTasks = useBTasksStore((state) => state.tasks);
    const pathMarkSyncTask = getPathMarkSyncTask(bTasks);

    // Load pending sync count
    const loadPendingSyncCount = useCallback(async () => {
      try {
        const response = await BApi.pathMark.getPendingPathMarksCount();

        setPendingSyncCount(response?.data ?? 0);
      } catch (error) {
        console.error("Failed to load pending sync count", error);
      }
    }, []);

    useEffect(() => {
      loadPendingSyncCount();
    }, [loadPendingSyncCount]);

    // Auto-refresh when PathMark sync tasks complete
    useEffect(() => {
      const previousStatus = prevTaskStatusRef.current;
      const currentStatus = pathMarkSyncTask?.status;

      if (didPathMarkSyncTaskComplete(previousStatus, currentStatus)) {
        loadPendingSyncCount();
        onSyncComplete?.();
      }

      prevTaskStatusRef.current = currentStatus;
    }, [pathMarkSyncTask?.status, loadPendingSyncCount, onSyncComplete]);

    // Expose refresh method via ref
    useImperativeHandle(
      ref,
      () => ({
        refresh: loadPendingSyncCount,
      }),
      [loadPendingSyncCount],
    );

    const handleSyncComplete = useCallback(() => {
      loadPendingSyncCount();
      onSyncComplete?.();
    }, [loadPendingSyncCount, onSyncComplete]);

    return (
      <>
        <Badge
          color="warning"
          content={pendingSyncCount}
          isInvisible={pendingSyncCount === 0}
          size="sm"
        >
          <Button
            className={className}
            color={pendingSyncCount > 0 ? "warning" : "default"}
            size={buttonSize}
            startContent={<AiOutlineSync />}
            variant="flat"
            onPress={() => setShowPendingSyncModal(true)}
          >
            {t("pathMarkConfig.action.syncMarks")}
          </Button>
        </Badge>

        {showPendingSyncModal && (
          <PendingSyncListModal
            visible={showPendingSyncModal}
            onClose={() => setShowPendingSyncModal(false)}
            onSyncComplete={handleSyncComplete}
          />
        )}
      </>
    );
  },
);

PendingSyncButton.displayName = "PendingSyncButton";

export default PendingSyncButton;
