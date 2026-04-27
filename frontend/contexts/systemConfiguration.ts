/**
 * Default system settings for POS (language, printer, TSE display fields).
 * Used as the initial state and merge base when loading persisted config.
 */
export interface SystemConfiguration {
  language: string;
  theme: 'light' | 'dark' | 'system';
  notifications: boolean;
  printerSettings: {
    enabled: boolean;
    model: string;
    paperSize: '80mm' | '58mm';
    autoPrint: boolean;
    printLogo: boolean;
    printTaxDetails: boolean;
    footerText: string;
  };
  tseSettings: {
    enabled: boolean;
    connected: boolean;
    deviceId: string;
  };
}

export const defaultSystemConfig: SystemConfiguration = {
  language: 'de',
  theme: 'system',
  notifications: true,
  printerSettings: {
    enabled: false,
    model: 'EPSON TM-T88VI',
    paperSize: '80mm',
    autoPrint: true,
    printLogo: true,
    printTaxDetails: true,
    footerText: 'Vielen Dank für Ihren Einkauf!',
  },
  tseSettings: {
    enabled: true,
    connected: false,
    deviceId: '',
  },
};
