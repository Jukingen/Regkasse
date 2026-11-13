export type RksvAusfallEpisode = {
  id: string;
  tenantId: string;
  deviceId: string | null;
  deviceSerial: string | null;
  episodeType: string;
  operationKind: string;
  begruendung: string;
  beginnUtc: string | null;
  endeUtc: string | null;
  status: string;
  outboxMessageId: string | null;
  externalReference: string | null;
  certificateSerial: string | null;
  kassenId: string | null;
  cashRegisterId: string | null;
  relatedAusfallEpisodeId: string | null;
  operatorNote: string | null;
  createdBy: string | null;
  approvedBy: string | null;
  approvedAtUtc: string | null;
  lastErrorCode: string | null;
  lastErrorMessage: string | null;
  createdAtUtc: string;
  updatedAtUtc: string | null;
};

export type RksvAusfallTriggerRequest = {
  deviceId?: string;
  cashRegisterId?: string;
  episodeType: string;
  operationKind: string;
  begruendung: string;
  beginnUtc?: string;
  endeUtc?: string;
  certificateSerial?: string;
  kassenId?: string;
  relatedAusfallEpisodeId?: string;
  operatorNote?: string;
  enqueueImmediately?: boolean;
};

export type RksvAusfallTriggerResponse = {
  success: boolean;
  errorCode?: string | null;
  message?: string | null;
  episode?: RksvAusfallEpisode | null;
};
