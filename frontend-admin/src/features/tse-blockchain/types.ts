export interface TseBlockchainStatus {
  blockchainStatus: string;
  networkName: string;
  currentBlock: number;
  totalTransactions: number;
  isSimulated: boolean;
  updatedAt?: string | null;
  diagnosticOnly: boolean;
}

export interface TseBlockchainTransaction {
  id: string;
  transactionHash: string;
  blockNumber: number;
  isVerified: boolean;
  createdAt: string;
  signatureHash: string;
  signaturePreview?: string | null;
}

export interface TseBlockchainRecord {
  id: string;
  tenantId: string;
  transactionHash: string;
  blockHash: string;
  blockNumber: number;
  createdAt: string;
  signatureHash: string;
  signaturePreview?: string | null;
  isVerified: boolean;
  isSimulated: boolean;
  networkName: string;
  sourceId?: string | null;
  sourceType: string;
}

export interface StoreTseBlockchainSignatureRequest {
  tenantId: string;
  signatureData: string;
  sourceType?: string;
  sourceId?: string;
}
