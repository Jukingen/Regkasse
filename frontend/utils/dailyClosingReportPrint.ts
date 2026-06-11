import * as FileSystem from 'expo-file-system';
import { Platform } from 'react-native';
import * as Print from 'expo-print';

import type { PosDailyClosingReportDto } from '../services/api/shiftService';
import {
  formatDailyClosingReportHtml,
  type DailyClosingReportLabels,
} from './dailyClosingReportFormat';

export type { DailyClosingReportLabels } from './dailyClosingReportFormat';

async function printWeb(html: string): Promise<void> {
  return new Promise((resolve, reject) => {
    try {
      const iframe = document.createElement('iframe');
      iframe.style.display = 'none';
      document.body.appendChild(iframe);

      const iframeDoc = iframe.contentDocument || iframe.contentWindow?.document;
      if (!iframeDoc) {
        throw new Error('Failed to access iframe document');
      }

      iframeDoc.open();
      iframeDoc.write(html);
      iframeDoc.close();

      iframe.onload = () => {
        setTimeout(() => {
          try {
            iframe.contentWindow?.focus();
            iframe.contentWindow?.print();
            setTimeout(() => {
              if (document.body.contains(iframe)) {
                document.body.removeChild(iframe);
              }
              resolve();
            }, 1000);
          } catch (err) {
            if (document.body.contains(iframe)) {
              document.body.removeChild(iframe);
            }
            reject(err);
          }
        }, 300);
      };
    } catch (error) {
      reject(error);
    }
  });
}

export async function printDailyClosingReport(
  report: PosDailyClosingReportDto,
  labels: DailyClosingReportLabels,
  formatLocale: string
): Promise<void> {
  const html = formatDailyClosingReportHtml(report, labels, formatLocale);
  if (Platform.OS === 'web') {
    await printWeb(html);
    return;
  }
  await Print.printAsync({ html, width: 300 });
}

async function printWebPdf(blob: Blob): Promise<void> {
  return new Promise((resolve, reject) => {
    try {
      const url = URL.createObjectURL(blob);
      const iframe = document.createElement('iframe');
      iframe.style.display = 'none';
      iframe.src = url;
      document.body.appendChild(iframe);
      iframe.onload = () => {
        setTimeout(() => {
          try {
            iframe.contentWindow?.focus();
            iframe.contentWindow?.print();
            setTimeout(() => {
              if (document.body.contains(iframe)) {
                document.body.removeChild(iframe);
              }
              URL.revokeObjectURL(url);
              resolve();
            }, 1000);
          } catch (err) {
            if (document.body.contains(iframe)) {
              document.body.removeChild(iframe);
            }
            URL.revokeObjectURL(url);
            reject(err);
          }
        }, 400);
      };
    } catch (error) {
      reject(error);
    }
  });
}

async function blobToBase64(blob: Blob): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      const result = reader.result as string;
      resolve(result.includes(',') ? result.split(',')[1] : result);
    };
    reader.onerror = () => reject(new Error('Failed to read PDF blob'));
    reader.readAsDataURL(blob);
  });
}

/** Prints a server-generated daily closing PDF (multi-language). */
export async function printDailyClosingReportPdf(blob: Blob, dailyClosingId: string): Promise<void> {
  if (Platform.OS === 'web' && typeof document !== 'undefined') {
    await printWebPdf(blob);
    return;
  }

  const fileUri = `${FileSystem.documentDirectory}tagesabschluss_${dailyClosingId}.pdf`;
  const base64 = await blobToBase64(blob);
  await FileSystem.writeAsStringAsync(fileUri, base64, {
    encoding: FileSystem.EncodingType.Base64,
  });
  await Print.printAsync({ uri: fileUri });
}
