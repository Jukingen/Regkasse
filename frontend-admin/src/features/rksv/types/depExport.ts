/** BMF RKSV DEP §7 export root (Anlage Z3). Property names match backend JSON exactly. */

export type RksvDepBelegeGruppe = {
  Signaturzertifikat: string;
  Zertifizierungsstellen: string[];
  'Belege-kompakt': string[];
};

export type RksvDepExportRoot = {
  'Belege-Gruppe': RksvDepBelegeGruppe[];
};

/** Inline envelope when `includeEnvelope=true` (camelCase ASP.NET JSON). */
export type RksvDepExportEnvelope = {
  legalNotice?: string;
  dep: RksvDepExportRoot;
  belegCount?: number;
  belegeGruppeCount?: number;
  cashRegisterId?: string;
  registerNumber?: string;
  fromUtc?: string;
  toUtc?: string;
  isDemo?: boolean;
  isSimulated?: boolean;
  simulationNote?: string | null;
  environment?: string;
  formatValidated?: boolean;
  legacyJwsCount?: number;
  f5CompliantJwsCount?: number;
  legacyJwsWarning?: string | null;
  prueftoolCompatible?: boolean;
  /** Server history id for `/api/admin/rksv/dep-export/download/{id}`. */
  historyId?: string | null;
  /** Alias of historyId (DepExportResult.exportId). */
  exportId?: string | null;
  fileName?: string | null;
  downloadUrl?: string | null;
  expiresAt?: string | null;
  fileSizeBytes?: number | null;
};

export type DepExportRequestParams = {
  cashRegisterId: string;
  fromUtc: string;
  toUtc: string;
  includeSpecialReceipts: boolean;
  includeDailyClosings: boolean;
};

export type DepExportLiveMeta = {
  isSimulated: boolean;
  simulationNote: string | null;
  environment: string | null;
  legacyJwsCount: number;
  f5CompliantJwsCount: number;
  legacyJwsWarning: string | null;
  prueftoolCompatible: boolean;
  historyId?: string | null;
  exportId?: string | null;
  fileName?: string | null;
  downloadUrl?: string | null;
  expiresAt?: string | null;
  fileSizeBytes?: number | null;
};

export type CertificateInfo = {
  serialNumber: string;
  certificateDerBase64: string;
  thumbprint: string;
};

/** Admin test-material API response for Prüftool verification. */
export type CryptoMaterial = {
  aesKeyBase64: string;
  certificates: CertificateInfo[];
  turnoverCounters: Record<string, string>;
};

export type DepExportStats = {
  groupCount: number;
  totalSignatures: number;
  certificateThumbprints: string[];
};

export function computeDepExportStats(
  exportResult: RksvDepExportRoot | null | undefined
): DepExportStats | null {
  const groups = exportResult?.['Belege-Gruppe'];
  if (!groups?.length) {
    return exportResult ? { groupCount: 0, totalSignatures: 0, certificateThumbprints: [] } : null;
  }

  const totalSignatures = groups.reduce(
    (sum, group) => sum + (group['Belege-kompakt']?.length ?? 0),
    0
  );
  const certificateThumbprints = groups
    .map((group) => group.Signaturzertifikat?.slice(0, 16) ?? '')
    .filter((value) => value.length > 0);

  return {
    groupCount: groups.length,
    totalSignatures,
    certificateThumbprints,
  };
}
