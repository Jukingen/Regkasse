import { apiClient } from './config';

export interface NetworkStatus {
  isInternetAvailable: boolean;
  isFinanzOnlineAvailable: boolean;
  lastChecked: string;
  status: 'DISCONNECTED' | 'INTERNET_ONLY' | 'FULLY_CONNECTED';
  canProcessInvoices: boolean;
  canSubmitToFinanzOnline: boolean;
  recommendations: string[];
}

export interface HealthStatus {
  timestamp: string;
  internet: {
    available: boolean;
    status: 'HEALTHY' | 'UNHEALTHY';
  };
  finanzOnline: {
    available: boolean;
    status: 'HEALTHY' | 'UNHEALTHY';
  };
  overall: {
    status: 'OPERATIONAL' | 'DEGRADED';
    message: string;
  };
}

class NetworkService {
  private baseUrl = '/api/network';

  // Network durumunu getir
  async getNetworkStatus(): Promise<NetworkStatus> {
    try {
      const response = await apiClient.get<NetworkStatus>(`${this.baseUrl}/status`);
      return response.data;
    } catch (error) {
      console.error('Network status fetch failed:', error);
      // Fallback durumu
      return {
        isInternetAvailable: false,
        isFinanzOnlineAvailable: false,
        lastChecked: new Date().toISOString(),
        status: 'DISCONNECTED',
        canProcessInvoices: false,
        canSubmitToFinanzOnline: false,
        recommendations: ['İnternet bağlantısını kontrol edin', 'TSE cihazının bağlı olduğundan emin olun']
      };
    }
  }

  // Bağlantı testi yap
  async testConnection(url: string): Promise<{ url: string; isAvailable: boolean; message: string }> {
    try {
      const response = await apiClient.post<{ url: string; isAvailable: boolean; message: string }>(
        `${this.baseUrl}/test`,
        { url }
      );
      return response.data;
    } catch (error) {
      console.error('Connection test failed:', error);
      throw error;
    }
  }

  // Health check yap
  async getHealthStatus(): Promise<HealthStatus> {
    try {
      const response = await apiClient.get<HealthStatus>(`${this.baseUrl}/health`);
      return response.data;
    } catch (error) {
      console.error('Health check failed:', error);
      throw error;
    }
  }

  // Monitoring başlat (Admin only)
  async startMonitoring(): Promise<{ message: string }> {
    try {
      const response = await apiClient.post<{ message: string }>(`${this.baseUrl}/monitoring/start`);
      return response.data;
    } catch (error) {
      console.error('Start monitoring failed:', error);
      throw error;
    }
  }

  // Monitoring durdur (Admin only)
  async stopMonitoring(): Promise<{ message: string }> {
    try {
      const response = await apiClient.post<{ message: string }>(`${this.baseUrl}/monitoring/stop`);
      return response.data;
    } catch (error) {
      console.error('Stop monitoring failed:', error);
      throw error;
    }
  }

  // Periyodik network durumu kontrolü
  async startPeriodicCheck(callback: (status: NetworkStatus) => void, intervalMs: number = 2 * 60 * 1000): Promise<number> {
    const checkStatus = async () => {
      try {
        const status = await this.getNetworkStatus();
        callback(status);
      } catch (error) {
        console.error('Periodic network check failed:', error);
      }
    };

    // İlk kontrolü hemen yap
    await checkStatus();
    
    // Periyodik kontrolü başlat
    return setInterval(checkStatus, intervalMs);
  }

  // Network durumu mesajını getir
  getStatusMessage(status: NetworkStatus): string {
    switch (status.status) {
      case 'FULLY_CONNECTED':
        return 'Tüm bağlantılar normal çalışıyor';
      case 'INTERNET_ONLY':
        return 'İnternet var, FinanzOnline bağlantısı yok';
      case 'DISCONNECTED':
        return 'İnternet bağlantısı yok';
      default:
        return 'Bağlantı durumu bilinmiyor';
    }
  }

  // Fiş kesme izni kontrolü
  canProcessInvoices(status: NetworkStatus): boolean {
    return status.canProcessInvoices;
  }

  // FinanzOnline gönderim izni kontrolü
  canSubmitToFinanzOnline(status: NetworkStatus): boolean {
    return status.canSubmitToFinanzOnline;
  }
}

export const networkService = new NetworkService(); 