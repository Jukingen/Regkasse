import { API_BASE_URL } from './config';

export interface TseStatus {
  isConnected: boolean;
  serialNumber: string;
  lastSignatureCounter: number;
  lastSignatureTime: string;
  memoryStatus: string;
  certificateStatus: string;
}

export interface TseSignatureResult {
  signature: string;
  signatureCounter: number;
  time: string;
  processType: string;
  serialNumber: string;
}

export interface TseDailyReport {
  date: string;
  signature: string;
  cashRegisterId: string;
  receiptCount: number;
  totalAmount: number;
  taxStandard: number;
  taxReduced: number;
  taxSpecial: number;
}

class TseService {
  private baseUrl = `${API_BASE_URL}/tse`;

  async getStatus(): Promise<TseStatus> {
    try {
      const response = await fetch(`${this.baseUrl}/status`, {
        method: 'GET',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${await this.getToken()}`
        }
      });

      if (!response.ok) {
        throw new Error('TSE status fetch failed');
      }

      return await response.json();
    } catch (error) {
      console.error('TSE status error:', error);
      throw error;
    }
  }

  async signTransaction(processData: string, processType: string = 'SIGN'): Promise<TseSignatureResult> {
    try {
      const response = await fetch(`${this.baseUrl}/sign`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${await this.getToken()}`
        },
        body: JSON.stringify({
          processData,
          processType
        })
      });

      if (!response.ok) {
        throw new Error('TSE signature failed');
      }

      return await response.json();
    } catch (error) {
      console.error('TSE signature error:', error);
      throw error;
    }
  }

  async generateDailyReport(): Promise<TseSignatureResult> {
    try {
      const response = await fetch(`${this.baseUrl}/daily-report`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${await this.getToken()}`
        }
      });

      if (!response.ok) {
        throw new Error('Daily report generation failed');
      }

      return await response.json();
    } catch (error) {
      console.error('Daily report error:', error);
      throw error;
    }
  }

  async validateSignature(signature: string, processData: string): Promise<boolean> {
    try {
      const response = await fetch(`${this.baseUrl}/validate`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${await this.getToken()}`
        },
        body: JSON.stringify({
          signature,
          processData
        })
      });

      if (!response.ok) {
        return false;
      }

      const result = await response.json();
      return result.isValid;
    } catch (error) {
      console.error('Signature validation error:', error);
      return false;
    }
  }

  async initializeHardware(): Promise<boolean> {
    try {
      const response = await fetch(`${this.baseUrl}/initialize`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${await this.getToken()}`
        }
      });

      return response.ok;
    } catch (error) {
      console.error('TSE initialization error:', error);
      return false;
    }
  }

  private async getToken(): Promise<string> {
    // AsyncStorage'dan token'ı al
    const AsyncStorage = require('@react-native-async-storage/async-storage');
    return await AsyncStorage.getItem('token') || '';
  }
}

export const tseService = new TseService();
export default tseService; 