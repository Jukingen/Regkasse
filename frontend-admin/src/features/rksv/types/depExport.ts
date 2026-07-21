/** BMF RKSV DEP §7 export root (Anlage Z3). Property names match backend JSON exactly. */

export type RksvDepBelegeGruppe = {
  Signaturzertifikat: string;
  Zertifizierungsstellen: string[];
  'Belege-kompakt': string[];
};

export type RksvDepExportRoot = {
  'Belege-Gruppe': RksvDepBelegeGruppe[];
};

export type DepExportRequestParams = {
  cashRegisterId: string;
  fromUtc: string;
  toUtc: string;
  includeSpecialReceipts: boolean;
  includeDailyClosings: boolean;
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
