import { apiClient } from './config';

export interface FinanzOnlineConfig {
  apiUrl: string;
  username: string;
  autoSubmit: boolean;
  submitInterval: number;
  retryAttempts: number;
  enableValidation: boolean;
  isEnabled: boolean;
}

export interface FinanzOnlineStatus {
  isConnected: boolean;
  apiVersion: string;
  lastSync: string;
  pendingInvoices: number;
  pendingReports: number;
  errorMessage?: string;
}

export interface FinanzOnlineSubmitRequest {
  invoiceNumber: string;
  totalAmount: number;
  tseSignature: string;
  taxDetails: string;
  invoiceDate: string;
  kassenId: string;
}

export interface FinanzOnlineSubmitResponse {
  success: boolean;
  message: string;
  submissionId: string;
  timestamp: string;
}

export interface FinanzOnlineError {
  code: string;
  message: string;
  timestamp: string;
  invoiceNumber: string;
  retryCount: number;
}

export interface FinanzOnlineTestResponse {
  success: boolean;
  message: string;
  apiVersion: string;
  responseTime: number;
  timestamp: string;
}

// FinanzOnline konfigürasyonunu al
export const getFinanzOnlineConfig = async (): Promise<FinanzOnlineConfig> => {
  try {
    const response = await apiClient.get<FinanzOnlineConfig>('/finanzonline/config');
    return response;
  } catch (error) {
    console.error('FinanzOnline config fetch failed:', error);
    throw error;
  }
};

// FinanzOnline konfigürasyonunu güncelle
export const updateFinanzOnlineConfig = async (config: Partial<FinanzOnlineConfig>): Promise<FinanzOnlineConfig> => {
  try {
    const response = await apiClient.put<FinanzOnlineConfig>('/finanzonline/config', config);
    return response;
  } catch (error) {
    console.error('FinanzOnline config update failed:', error);
    throw error;
  }
};

// FinanzOnline durumunu kontrol et
export const getFinanzOnlineStatus = async (): Promise<FinanzOnlineStatus> => {
  try {
    const response = await apiClient.get<FinanzOnlineStatus>('/finanzonline/status');
    return response;
  } catch (error) {
    console.error('FinanzOnline status check failed:', error);
    throw error;
  }
};

// Faturayı FinanzOnline'a gönder
export const submitInvoiceToFinanzOnline = async (request: FinanzOnlineSubmitRequest): Promise<FinanzOnlineSubmitResponse> => {
  try {
    const response = await apiClient.post<FinanzOnlineSubmitResponse>('/finanzonline/submit-invoice', request);
    return response;
  } catch (error) {
    console.error('FinanzOnline invoice submission failed:', error);
    throw error;
  }
};

// FinanzOnline hatalarını al
export const getFinanzOnlineErrors = async (): Promise<FinanzOnlineError[]> => {
  try {
    const response = await apiClient.get<FinanzOnlineError[]>('/finanzonline/errors');
    return response;
  } catch (error) {
    console.error('FinanzOnline errors fetch failed:', error);
    throw error;
  }
};

// FinanzOnline bağlantı testi
export const testFinanzOnlineConnection = async (): Promise<FinanzOnlineTestResponse> => {
  try {
    const response = await apiClient.post<FinanzOnlineTestResponse>('/finanzonline/test-connection');
    return response;
  } catch (error) {
    console.error('FinanzOnline connection test failed:', error);
    throw error;
  }
};

// FinanzOnline otomatik gönderim ayarlarını güncelle
export const updateAutoSubmitSettings = async (autoSubmit: boolean, interval: number): Promise<FinanzOnlineConfig> => {
  try {
    const response = await updateFinanzOnlineConfig({
      autoSubmit,
      submitInterval: interval
    });
    return response;
  } catch (error) {
    console.error('FinanzOnline auto-submit settings update failed:', error);
    throw error;
  }
};

// FinanzOnline retry ayarlarını güncelle
export const updateRetrySettings = async (retryAttempts: number): Promise<FinanzOnlineConfig> => {
  try {
    const response = await updateFinanzOnlineConfig({
      retryAttempts
    });
    return response;
  } catch (error) {
    console.error('FinanzOnline retry settings update failed:', error);
    throw error;
  }
};

// FinanzOnline validasyon ayarlarını güncelle
export const updateValidationSettings = async (enableValidation: boolean): Promise<FinanzOnlineConfig> => {
  try {
    const response = await updateFinanzOnlineConfig({
      enableValidation
    });
    return response;
  } catch (error) {
    console.error('FinanzOnline validation settings update failed:', error);
    throw error;
  }
};

// FinanzOnline durumunu periyodik olarak kontrol et
export const startFinanzOnlineMonitoring = (callback: (status: FinanzOnlineStatus) => void, intervalMs: number = 5 * 60 * 1000) => {
  const checkStatus = async () => {
    try {
      const status = await getFinanzOnlineStatus();
      callback(status);
    } catch (error) {
      console.error('FinanzOnline monitoring failed:', error);
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

// FinanzOnline bağlantı durumunu kontrol et
export const isFinanzOnlineConnected = async (): Promise<boolean> => {
  try {
    const status = await getFinanzOnlineStatus();
    return status.isConnected;
  } catch (error) {
    console.error('FinanzOnline connection check failed:', error);
    return false;
  }
};

// FinanzOnline'da bekleyen fatura sayısını al
export const getPendingInvoiceCount = async (): Promise<number> => {
  try {
    const status = await getFinanzOnlineStatus();
    return status.pendingInvoices;
  } catch (error) {
    console.error('FinanzOnline pending invoice count fetch failed:', error);
    return 0;
  }
};
