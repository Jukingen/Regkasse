export type TagesberichtStatusFilter = 'all' | 'Provisional' | 'Finalized' | 'Corrected';

export type TagesberichtListRow = {
  id: string;
  viennaBusinessDate: string;
  cashRegisterId: string;
  registerNumber?: string;
  reportStatus: string;
  correctionKind: string;
  grossSalesAmount: number;
  createdAtUtc: string;
  submission: {
    lifecycle: string;
    operatorHintDe?: string;
    operatorHintEn?: string | null;
    outboxStatus?: string;
  };
};

export function isProvisionalStatus(status: string | undefined): boolean {
  return status === 'Provisional';
}

export function isCorrectedRow(row: Pick<TagesberichtListRow, 'reportStatus' | 'correctionKind'>): boolean {
  return row.reportStatus === 'Superseded' || (Boolean(row.correctionKind) && row.correctionKind !== 'None');
}

export function matchesTagesberichtStatusFilter(
  row: Pick<TagesberichtListRow, 'reportStatus' | 'correctionKind'>,
  filter: TagesberichtStatusFilter
): boolean {
  if (filter === 'all') return true;
  if (filter === 'Provisional') return row.reportStatus === 'Provisional';
  if (filter === 'Finalized') return row.reportStatus === 'Finalized';
  return isCorrectedRow(row);
}

export function reportStatusTagColor(status: string): string {
  if (status === 'Finalized') return 'blue';
  if (status === 'Provisional') return 'gold';
  if (status === 'Superseded') return 'orange';
  return 'default';
}
