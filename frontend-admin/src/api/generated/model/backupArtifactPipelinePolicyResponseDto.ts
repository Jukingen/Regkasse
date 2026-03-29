/**
 * Swagger-aligned type (orval regen may replace). Backup artifact pipeline policy surface.
 */
export interface BackupArtifactPipelinePolicyResponseDto {
  artifactStagingRootConfigured?: boolean;
  effectiveAdapterKind?: string;
  externalArchiveRequirement?: string;
  externalArchiveRootConfigured?: boolean;
  operatorNotes?: string[];
  stagingOnDiskHashReverificationExpected?: boolean;
  willRunExternalArchiveAfterStagingVerificationWhenEligible?: boolean;
}
