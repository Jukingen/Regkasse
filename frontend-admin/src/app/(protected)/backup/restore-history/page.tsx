"use client";

import React from "react";
import { BackupPageShell } from "@/features/backup/components/BackupPageShell";
import { RestoreHistoryView } from "@/features/backup/components/RestoreHistoryView";
import { BACKUP_RESTORE_HISTORY_PATH } from "@/shared/backupAreaRoutes";

export default function RestoreHistoryPage() {
  return (
    <BackupPageShell
      titleKey="backupDr.restoreHistory.pageTitle"
      sectionLabelKey="nav.backupRestoreHistory"
      sectionHref={BACKUP_RESTORE_HISTORY_PATH}
      subtitleKey="backupDr.restoreHistory.pageSubtitle"
    >
      <RestoreHistoryView />
    </BackupPageShell>
  );
}
