"use client";

import React from "react";
import { BackupPageShell } from "@/features/backup/components/BackupPageShell";
import { BackupComplianceDashboard } from "@/features/backup/components/BackupComplianceDashboard";
import { BACKUP_COMPLIANCE_PATH } from "@/shared/backupAreaRoutes";

export default function BackupCompliancePage() {
  return (
    <BackupPageShell
      titleKey="backupDr.compliance.pageTitle"
      sectionLabelKey="nav.backupCompliance"
      sectionHref={BACKUP_COMPLIANCE_PATH}
      subtitleKey="backupDr.compliance.pageSubtitle"
    >
      <BackupComplianceDashboard />
    </BackupPageShell>
  );
}
