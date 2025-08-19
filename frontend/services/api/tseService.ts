import { apiClient } from './config';

export interface TseStatus {
  isConnected: boolean;
  serialNumber: string;
  certificateStatus: string;
  memoryStatus: string;
  lastSignatureTime: string;
  canCreateInvoices: boolean;
  errorMessage?: string;
  kassenId: string;
  finanzOnlineEnabled: boolean;
}

export interface TseDevice {
  id: string;
  serialNumber: string;
  deviceType: string;
  vendorId: string;
  productId: string;
  isConnected: boolean;
  lastConnectionTime: string;
  lastSignatureTime: string;
  certificateStatus: string;
  memoryStatus: string;
  canCreateInvoices: boolean;
  errorMessage?: string;
  timeoutSeconds: number;
  isActive: boolean;
  kassenId: string;
  finanzOnlineUsername: string;
  finanzOnlineEnabled: boolean;
  lastFinanzOnlineSync: string;
  pendingInvoices: number;
  pendingReports: number;
}

export interface TseConnectionRequest {
  serialNumber: string;
}

export interface TseConnectionResponse {
  success: boolean;
  message: string;
  errorMessage?: string;
  deviceInfo?: {
    serialNumber: string;
    deviceType: string;
    kassenId: string;
    certificateStatus: string;
    memoryStatus: string;
  };
}

export interface TseSignatureRequest {
  invoiceNumber: string;
  totalAmount: number;
  taxDetails: string;
}

export interface TseSignatureResponse {
  success: boolean;
  tseSignature: string;
  timestamp: string;
  kassenId: string;
  message: string;
}

// TSE durumu kontrol et
export const checkTseStatus = async (): Promise<TseStatus> => {
  try {
    const response = await apiClient.get<TseStatus>('/tse/status');
    return response;
  } catch (error) {
    console.error('TSE status check failed:', error);
    throw error;
  }
};

// TSE cihazlarını listele
export const getTseDevices = async (): Promise<TseDevice[]> => {
  try {
    const response = await apiClient.get<TseDevice[]>('/tse/devices');
    return response;
  } catch (error) {
    console.error('TSE devices fetch failed:', error);
    throw error;
  }
};

// TSE cihazına bağlan
export const connectTseDevice = async (request: TseConnectionRequest): Promise<TseConnectionResponse> => {
  try {
    const response = await apiClient.post<TseConnectionResponse>('/tse/connect', request);
    return response;
  } catch (error) {
    console.error('TSE connection failed:', error);
    throw error;
  }
};

// TSE cihazından bağlantıyı kes
export const disconnectTseDevice = async (): Promise<TseConnectionResponse> => {
  try {
    const response = await apiClient.post<TseConnectionResponse>('/tse/disconnect');
    return response;
  } catch (error) {
    console.error('TSE disconnection failed:', error);
    throw error;
  }
};

// TSE imzası oluştur
export const createTseSignature = async (request: TseSignatureRequest): Promise<TseSignatureResponse> => {
  try {
    const response = await apiClient.post<TseSignatureResponse>('/tse/signature', request);
    return response;
  } catch (error) {
    console.error('TSE signature creation failed:', error);
    throw error;
  }
};

// TSE cihazı bağlantı durumunu periyodik olarak kontrol et
export const startTseMonitoring = (callback: (status: TseStatus) => void, intervalMs: number = 2 * 60 * 1000) => {
  const checkStatus = async () => {
    try {
      const status = await checkTseStatus();
      callback(status);
    } catch (error) {
      console.error('TSE monitoring failed:', error);
    }
  };

  // İlk kontrol
  checkStatus();

  // Periyodik kontrol
  const intervalId = setInterval(checkStatus, intervalMs);

  // Cleanup fonksiyonu
  return () => {
    clearInterval(intervalId);
  };
};

// TSE cihazı bağlantı testi
export const testTseConnection = async (serialNumber: string): Promise<boolean> => {
  try {
    const response = await connectTseDevice({ serialNumber });
    return response.success;
  } catch (error) {
    console.error('TSE connection test failed:', error);
    return false;
  }
};

// TSE cihazı durumunu güncelle
export const updateTseStatus = async (deviceId: string, updates: Partial<TseDevice>): Promise<TseDevice> => {
  try {
    const response = await apiClient.put<TseDevice>(`/tse/devices/${deviceId}`, updates);
    return response;
  } catch (error) {
    console.error('TSE device update failed:', error);
    throw error;
  }
};
