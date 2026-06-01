import type { BackupArtifactResponseDto, BackupRunResponseDto } from '@/api/generated/model';

/** Alias for backup run table/detail presentation (OpenAPI DTO includes formatted fields). */
export type BackupRunPresentationDto = BackupRunResponseDto;

export type BackupArtifactPresentationDto = BackupArtifactResponseDto & {
  formattedSize?: string | null;
};
