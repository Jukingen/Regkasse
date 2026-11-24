import type { AlertButton } from 'react-native';

import { showAlert, showConfirm } from '../utils/alert';
import {
  resolvePosHttpErrorMessage,
  userFacingPosHttpMessage,
  userFacingPosNetworkMessage,
  userFacingPosUnknownMessage,
} from '../utils/userFacingHttpError';

export interface APIError {
  status?: number;
  data?: any;
  message: string;
}

export const handleAPIError = (error: any): APIError => {
  console.error('API Error:', error);

  // Axios: error.response; config interceptor: error.status, error.data
  const status = error.response?.status ?? error.status;
  const data = error.response?.data ?? error.data;

  // No response and no status: offline, DNS failure or timeout.
  if (status == null && error.response == null) {
    return {
      message: userFacingPosNetworkMessage(error),
    };
  }

  const transportMessage = typeof error?.message === 'string' ? error.message : undefined;
  const msg400 =
    resolvePosHttpErrorMessage(400, data?.message, transportMessage) ?? transportMessage ?? '';

  // default: data?.message -> error?.message -> generic German text
  const msgDefault =
    (typeof data?.message === 'string' ? data.message : undefined) ??
    (typeof error?.message === 'string' ? error.message : undefined) ??
    userFacingPosUnknownMessage();

  // HTTP status code based errors
  switch (status) {
    case 400:
      return {
        status,
        data,
        message: msg400,
      };
    case 401:
      return {
        status,
        data,
        message: userFacingPosHttpMessage(401) ?? msgDefault,
      };
    case 403:
      return {
        status,
        data,
        message:
          resolvePosHttpErrorMessage(403, data?.message, transportMessage) ?? transportMessage ?? '',
      };
    case 404:
      return {
        status,
        data,
        message:
          resolvePosHttpErrorMessage(404, data?.message, transportMessage) ?? transportMessage ?? '',
      };
    case 409:
      return {
        status,
        data,
        message:
          resolvePosHttpErrorMessage(409, data?.message, transportMessage) ?? transportMessage ?? '',
      };
    case 422:
      return {
        status,
        data,
        message:
          (typeof data?.message === 'string' ? data.message : undefined) ??
          userFacingPosHttpMessage(422) ??
          msgDefault,
      };
    default:
      if (typeof status === 'number' && status >= 500 && status <= 599) {
        // Server stack messages are not actionable for cashiers.
        return {
          status,
          data,
          message: userFacingPosHttpMessage(status) ?? msgDefault,
        };
      }
      return {
        status,
        data,
        message: msgDefault,
      };
  }
};

// Alert.alert is a no-op on Expo web, so these go through the web-safe helpers.
export const showErrorAlert = (error: APIError | string, title: string = 'Fehler') => {
  const message = typeof error === 'string' ? error : error.message;
  showAlert(title, message);
};

export const showSuccessAlert = (message: string, title: string = 'Erfolg') => {
  showAlert(title, message);
};

export const showConfirmationAlert = (
  message: string,
  title: string = 'Bestätigen',
  onConfirm: () => void,
  onCancel?: () => void
) => {
  const buttons: AlertButton[] = [
    { text: 'Abbrechen', style: 'cancel', onPress: onCancel },
    { text: 'Bestätigen', style: 'destructive', onPress: onConfirm },
  ];
  showConfirm(title, message, buttons);
};

// Specific error messages for different operations (cashier-facing, German, with a next step).
export const ErrorMessages = {
  // Authentication
  LOGIN_FAILED: 'Anmeldung fehlgeschlagen. Bitte Zugangsdaten prüfen und erneut versuchen.',
  LOGOUT_FAILED: 'Abmeldung fehlgeschlagen. Bitte erneut versuchen.',
  SESSION_EXPIRED: 'Ihre Sitzung ist abgelaufen. Bitte melden Sie sich erneut an.',

  // Products
  PRODUCTS_LOAD_FAILED:
    'Produkte konnten nicht geladen werden. Bitte Verbindung prüfen und erneut versuchen.',
  PRODUCT_CREATE_FAILED: 'Produkt konnte nicht angelegt werden. Bitte erneut versuchen.',
  PRODUCT_UPDATE_FAILED: 'Produkt konnte nicht gespeichert werden. Bitte erneut versuchen.',
  PRODUCT_DELETE_FAILED: 'Produkt konnte nicht gelöscht werden. Bitte erneut versuchen.',
  PRODUCT_NOT_FOUND: 'Produkt nicht gefunden. Bitte Liste aktualisieren.',
  INSUFFICIENT_STOCK: 'Nicht genügend Bestand. Bitte Menge verringern.',

  // Customers
  CUSTOMERS_LOAD_FAILED:
    'Kunden konnten nicht geladen werden. Bitte Verbindung prüfen und erneut versuchen.',
  CUSTOMER_CREATE_FAILED: 'Kunde konnte nicht angelegt werden. Bitte erneut versuchen.',
  CUSTOMER_UPDATE_FAILED: 'Kunde konnte nicht gespeichert werden. Bitte erneut versuchen.',
  CUSTOMER_DELETE_FAILED: 'Kunde konnte nicht gelöscht werden. Bitte erneut versuchen.',
  CUSTOMER_NOT_FOUND: 'Kunde nicht gefunden. Bitte Nummer prüfen.',

  // Orders
  ORDER_CREATE_FAILED: 'Bestellung konnte nicht angelegt werden. Bitte erneut versuchen.',
  ORDER_UPDATE_FAILED: 'Bestellung konnte nicht gespeichert werden. Bitte erneut versuchen.',
  ORDER_CANCEL_FAILED: 'Bestellung konnte nicht storniert werden. Bitte erneut versuchen.',
  ORDER_COMPLETE_FAILED: 'Bestellung konnte nicht abgeschlossen werden. Bitte erneut versuchen.',
  ORDER_NOT_FOUND: 'Bestellung nicht gefunden. Bitte Nummer prüfen.',
  NO_ITEMS_IN_ORDER: 'Die Bestellung enthält keine Artikel. Bitte zuerst Artikel hinzufügen.',

  // Payments
  PAYMENT_FAILED: 'Zahlung fehlgeschlagen. Bitte erneut versuchen oder eine andere Zahlungsart wählen.',
  PAYMENT_PROCESSING_ERROR:
    'Bei der Zahlung ist ein Fehler aufgetreten. Bitte Beleg prüfen, bevor Sie erneut zahlen.',
  INVALID_PAYMENT_AMOUNT: 'Ungültiger Zahlbetrag. Bitte Betrag prüfen.',
  INSUFFICIENT_PAYMENT: 'Der Zahlbetrag reicht nicht aus. Bitte Betrag erhöhen.',

  // Reports
  REPORTS_LOAD_FAILED: 'Berichte konnten nicht geladen werden. Bitte erneut versuchen.',
  REPORT_EXPORT_FAILED: 'Bericht konnte nicht exportiert werden. Bitte erneut versuchen.',
  REPORT_PRINT_FAILED: 'Bericht konnte nicht gedruckt werden. Bitte Drucker prüfen.',

  // Hardware
  PRINTER_CONNECTION_FAILED: 'Drucker nicht erreichbar. Bitte Verbindung und Stromversorgung prüfen.',
  TSE_CONNECTION_FAILED: 'TSE nicht erreichbar. Bitte Verbindung prüfen oder den Support kontaktieren.',
  SCANNER_ERROR: 'Scanner-Fehler. Bitte erneut scannen oder den Code manuell eingeben.',

  // Network
  NETWORK_ERROR: 'Keine Netzwerkverbindung. Bitte Internet bzw. WLAN prüfen.',
  FINANZONLINE_UNAVAILABLE:
    'FinanzOnline ist momentan nicht erreichbar. Bitte später erneut versuchen.',
  PENDING_INVOICES_ERROR: 'Offene Rechnungen konnten nicht verarbeitet werden. Bitte erneut versuchen.',
  INVOICE_SUBMISSION_FAILED:
    'Rechnung konnte nicht an FinanzOnline übermittelt werden. Bitte später erneut versuchen.',
  NETWORK_TIMEOUT: 'Die Anfrage hat zu lange gedauert. Bitte erneut versuchen.',
  CONNECTION_LOST: 'Verbindung verloren. Bitte Netzwerkeinstellungen prüfen.',
  SYNC_FAILED: 'Daten konnten nicht synchronisiert werden. Bitte erneut versuchen.',

  // General
  UNKNOWN_ERROR: 'Ein unerwarteter Fehler ist aufgetreten. Bitte erneut versuchen.',
  VALIDATION_ERROR: 'Bitte Eingaben prüfen und erneut versuchen.',
  PERMISSION_DENIED: 'Sie haben keine Berechtigung für diese Aktion.',
  RESOURCE_NOT_FOUND: 'Der gesuchte Eintrag wurde nicht gefunden.',
  SERVER_ERROR: 'Serverfehler. Bitte in wenigen Sekunden erneut versuchen.',

  // Success messages
  PRODUCT_CREATED: 'Produkt wurde angelegt.',
  PRODUCT_UPDATED: 'Produkt wurde gespeichert.',
  PRODUCT_DELETED: 'Produkt wurde gelöscht.',
  CUSTOMER_CREATED: 'Kunde wurde angelegt.',
  CUSTOMER_UPDATED: 'Kunde wurde gespeichert.',
  CUSTOMER_DELETED: 'Kunde wurde gelöscht.',
  ORDER_CREATED: 'Bestellung wurde angelegt.',
  ORDER_UPDATED: 'Bestellung wurde gespeichert.',
  ORDER_CANCELLED: 'Bestellung wurde storniert.',
  ORDER_COMPLETED: 'Bestellung wurde abgeschlossen.',
  PAYMENT_SUCCESSFUL: 'Zahlung erfolgreich abgeschlossen.',
  SETTINGS_SAVED: 'Einstellungen wurden gespeichert.',
  DATA_SYNCED: 'Daten wurden synchronisiert.',
};

// Helper function to get error message based on operation type
export const getErrorMessage = (operation: string, error?: any): string => {
  const errorKey = `${operation.toUpperCase()}_FAILED`;
  return ErrorMessages[errorKey as keyof typeof ErrorMessages] || ErrorMessages.UNKNOWN_ERROR;
};

// Helper function to get success message based on operation type
export const getSuccessMessage = (operation: string): string => {
  const successKey = `${operation.toUpperCase()}_SUCCESSFUL`;
  return (
    ErrorMessages[successKey as keyof typeof ErrorMessages] || 'Vorgang erfolgreich abgeschlossen.'
  );
};
