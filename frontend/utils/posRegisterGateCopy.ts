/**
 * Single source of German POS copy for cash-register resolution / payment gate.
 * Align scenarios with backend codes where possible (see `mapBackendCashRegisterCodeToHint`).
 */

import type { RegisterListFailureKind } from './registerListError';

/** Backend diagnostic codes from POST /api/pos/payment or PUT settings. */
export const POS_CASH_REGISTER_CODES = {
  REQUIRED: 'CASH_REGISTER_REQUIRED',
  INVALID: 'CASH_REGISTER_INVALID',
  NOT_FOUND: 'CASH_REGISTER_NOT_FOUND',
  FORBIDDEN: 'CASH_REGISTER_FORBIDDEN',
  CLOSED: 'CASH_REGISTER_CLOSED',
  SELECTION_REQUIRED: 'CASH_REGISTER_SELECTION_REQUIRED',
} as const;

export type RegisterGateReadinessInput = {
  loading: boolean;
  error: boolean;
  nextAction?: string | null;
  messageCode?: string | null;
};

export type PosRegisterGateContext = {
  /** True when GET /user/settings failed (not “no assignment”). */
  settingsLoadFailed: boolean;
  registerListFailureKind: RegisterListFailureKind | null;
  registerListLoading: boolean;
  registerPicklistCount: number;
  /** POST /api/pos/cash-register/ensure-ready (POS entry). */
  posReadinessLoading?: boolean;
  posReadinessError?: boolean;
  posReadinessNextAction?: string | null;
  posReadinessMessageCode?: string | null;
};

/** Single object for payment/settings banners (avoids duplicated parameter lists in screens). */
export function buildPosRegisterGateContext(input: {
  settingsLoadFailed: boolean;
  registerListFailureKind: RegisterListFailureKind | null;
  registerListLoading: boolean;
  registerPicklistCount: number;
  readiness?: RegisterGateReadinessInput | null;
}): PosRegisterGateContext {
  const r = input.readiness;
  return {
    settingsLoadFailed: input.settingsLoadFailed,
    registerListFailureKind: input.registerListFailureKind,
    registerListLoading: input.registerListLoading,
    registerPicklistCount: input.registerPicklistCount,
    posReadinessLoading: r?.loading,
    posReadinessError: r?.error,
    posReadinessNextAction: r?.nextAction ?? null,
    posReadinessMessageCode: r?.messageCode ?? null,
  };
}

/** Backend PosCashRegisterReadinessMessageCodes (ensure-ready). */
export const POS_READINESS_MESSAGE_CODES = {
  READY: 'CASH_REGISTER_READY',
  AUTO_OPENED: 'CASH_REGISTER_AUTO_OPENED',
  REQUIRED: 'CASH_REGISTER_REQUIRED',
  CLOSED: 'CASH_REGISTER_CLOSED',
  FORBIDDEN: 'CASH_REGISTER_FORBIDDEN',
  CONFLICT: 'CASH_REGISTER_CONFLICT',
  NOT_FOUND: 'CASH_REGISTER_NOT_FOUND',
  ACTOR_ALREADY_OPEN: 'CASH_REGISTER_ACTOR_ALREADY_OPEN',
} as const;

export function registerGateBannerTitle(ctx: PosRegisterGateContext): string {
  if (ctx.settingsLoadFailed) return 'Kasseneinstellungen nicht ladbar';
  if (ctx.posReadinessLoading) return 'Kasse wird vorbereitet…';
  if (ctx.posReadinessError) return 'Kassenbereitschaft nicht ladbar';
  if (ctx.posReadinessMessageCode === POS_READINESS_MESSAGE_CODES.CONFLICT) {
    return 'Kasse bereits in Verwendung';
  }
  if (ctx.posReadinessMessageCode === POS_READINESS_MESSAGE_CODES.ACTOR_ALREADY_OPEN) {
    return 'Bereits eine Kasse geöffnet';
  }
  if (
    ctx.posReadinessNextAction === 'open_register' &&
    ctx.posReadinessMessageCode === POS_READINESS_MESSAGE_CODES.FORBIDDEN
  ) {
    return 'Kasse öffnen nicht erlaubt';
  }
  if (
    ctx.posReadinessNextAction === 'open_register' &&
    ctx.posReadinessMessageCode === POS_READINESS_MESSAGE_CODES.CLOSED
  ) {
    return 'Kasse ist geschlossen';
  }
  if (ctx.posReadinessNextAction === 'none' && ctx.posReadinessMessageCode === POS_READINESS_MESSAGE_CODES.REQUIRED) {
    return 'Keine Kasse im System';
  }
  if (ctx.posReadinessNextAction === 'select_register' && ctx.registerPicklistCount === 0) {
    return 'Kasse auswählen';
  }
  if (ctx.registerListLoading) return 'Kasse wird ermittelt…';
  if (ctx.registerPicklistCount > 1) return 'Kasse wählen';
  if (ctx.registerPicklistCount === 1) return 'Kasse wird zugewiesen…';
  if (ctx.registerListFailureKind === 'forbidden' || ctx.registerListFailureKind === 'unauthorized') {
    return 'Zahlung derzeit nicht möglich';
  }
  if (ctx.registerListFailureKind === 'network' || ctx.registerListFailureKind === 'unknown') {
    return 'Kassenliste nicht erreichbar';
  }
  return 'Keine Kasse verfügbar';
}

export function registerGateBannerIntro(): string {
  return 'Für eine fiskal gültige Zahlung ist eine verwendbare Registrierkasse (geöffnet) und Zuordnung erforderlich.';
}

export function registerGateBannerDetail(ctx: PosRegisterGateContext): string {
  if (ctx.settingsLoadFailed) {
    return 'Die Kasseneinstellungen konnten nicht vom Server geladen werden. Prüfen Sie die Verbindung und tippen Sie auf „Erneut versuchen“, oder melden Sie sich erneut an.';
  }
  if (ctx.posReadinessLoading) {
    return 'Die Kasse wird mit dem Server abgeglichen. Bitte kurz warten.';
  }
  if (ctx.posReadinessError) {
    return 'Die serverseitige Kassenbereitschaft konnte nicht geladen werden. Nutzen Sie „Kassenbereitschaft erneut versuchen“ oder warten Sie, bis die Profil-Einstellungen geladen sind.';
  }
  if (ctx.posReadinessMessageCode === POS_READINESS_MESSAGE_CODES.ACTOR_ALREADY_OPEN) {
    return 'Sie haben bereits eine andere Kasse geöffnet. Schließen Sie diese zuerst in der Kassenverwaltung, oder nutzen Sie dieselbe Kasse weiter.';
  }
  if (ctx.posReadinessNextAction === 'open_register' && ctx.posReadinessMessageCode === POS_READINESS_MESSAGE_CODES.CLOSED) {
    return 'Die Kasse ist geschlossen. Bitte in der Kassenverwaltung öffnen (Schicht starten), sofern Ihre Rolle das erlaubt — oder wenden Sie sich an eine berechtigte Person.';
  }
  if (ctx.posReadinessNextAction === 'open_register' && ctx.posReadinessMessageCode === POS_READINESS_MESSAGE_CODES.FORBIDDEN) {
    return 'Automatisches oder manuelles Öffnen ist für Ihre Rolle nicht möglich oder deaktiviert. Bitte Administrator oder eine Rolle mit Schicht-/Kassenrechten.';
  }
  if (ctx.posReadinessNextAction === 'select_register' && ctx.registerPicklistCount === 0) {
    if (ctx.registerListFailureKind === 'forbidden' || ctx.registerListFailureKind === 'unauthorized') {
      return 'Mehrere Kassen sind angelegt, aber hier keine Liste wählbar. Bitte in den Einstellungen zuweisen (falls möglich) oder den Administrator um Zuweisung / Berechtigung „Kassenansicht“ bitten.';
    }
    return 'Mehrere Kassen: Bitte in den Einstellungen unter „Kasse“ eine Registrierkasse zuweisen. Ohne Zuweisung ist keine fiskal gültige Zahlung möglich.';
  }
  if (ctx.registerListLoading) {
    return 'Bitte kurz warten, die Kassenliste wird geladen.';
  }
  if (ctx.registerPicklistCount > 1) {
    return 'Mehrere Kassen: Bitte unten eine Kasse antippen. Die Zuordnung wird in Ihrem Profil gespeichert (unter Einstellungen ebenfalls möglich).';
  }
  if (ctx.registerPicklistCount === 1) {
    return 'Eine Kasse wird zugewiesen…';
  }
  if (ctx.registerListFailureKind === 'forbidden') {
    return 'Die Kassenliste ist nicht einsehbar. Bitte Administrator: Berechtigung „Kassenansicht“ oder Zuweisung einer Kasse.';
  }
  if (ctx.registerListFailureKind === 'unauthorized') {
    return 'Die Anmeldung ist ungültig oder abgelaufen. Bitte melden Sie sich erneut an.';
  }
  if (ctx.registerListFailureKind === 'network') {
    return 'Die Kassenliste konnte nicht geladen werden (Netzwerk). Prüfen Sie die Verbindung und tippen Sie auf „Erneut laden“.';
  }
  if (ctx.registerListFailureKind === 'unknown') {
    return 'Die Kassenliste konnte nicht geladen werden. Versuchen Sie „Erneut laden“ oder wenden Sie sich an den Administrator.';
  }
  if (ctx.registerPicklistCount > 0) {
    return 'Es ist keine geöffnete Kasse verfügbar, oder die Zuordnung fehlt. Wählen Sie unten eine Kasse oder weisen Sie in den Einstellungen zu; ggf. Kasse zuerst öffnen.';
  }
  return 'Es ist keine verwendbare Kasse verfügbar. Legen Sie eine Kasse an, öffnen Sie sie (Schicht), oder weisen Sie in den Einstellungen zu — ohne wählbare Liste nur über Administrator/Einstellungen.';
}

export function registerGateFooterHint(ctx: PosRegisterGateContext): string {
  if (ctx.settingsLoadFailed) {
    return '„Zahlen“ ist deaktiviert: Kasseneinstellungen fehlen — „Erneut versuchen“ oder erneut anmelden.';
  }
  if (ctx.posReadinessLoading) {
    return '„Zahlen“ ist kurz deaktiviert: Kassenbereitschaft wird geladen.';
  }
  if (ctx.posReadinessMessageCode === POS_READINESS_MESSAGE_CODES.CONFLICT) {
    return '„Zahlen“ ist deaktiviert: Kassenkonflikt — andere Sitzung aktiv.';
  }
  if (ctx.posReadinessMessageCode === POS_READINESS_MESSAGE_CODES.ACTOR_ALREADY_OPEN) {
    return '„Zahlen“ ist deaktiviert: Sie haben bereits eine andere Kasse geöffnet — bitte zuerst schließen.';
  }
  if (
    ctx.posReadinessNextAction === 'open_register' &&
    (ctx.posReadinessMessageCode === POS_READINESS_MESSAGE_CODES.CLOSED ||
      ctx.posReadinessMessageCode === POS_READINESS_MESSAGE_CODES.FORBIDDEN)
  ) {
    return '„Zahlen“ ist deaktiviert: Kasse muss geöffnet werden bzw. fehlende Berechtigung.';
  }
  if (ctx.posReadinessError) {
    return '„Zahlen“ ist deaktiviert: Kassenbereitschaft fehlt — „Erneut versuchen“ oder Einstellungen prüfen.';
  }
  if (ctx.posReadinessNextAction === 'select_register' && ctx.registerPicklistCount === 0) {
    return '„Zahlen“ ist deaktiviert: Kasse in den Einstellungen zuweisen oder Administrator informieren.';
  }
  if (ctx.registerListLoading) return 'Kasse wird geladen… „Zahlen“ ist danach freigeschaltet, sobald eine Kasse feststeht.';
  if (ctx.registerPicklistCount > 1) return 'Bitte zuerst eine Kasse wählen — dann wird „Zahlen“ aktiv.';
  if (ctx.registerPicklistCount === 1) return 'Eine Kasse wird zugewiesen — gleich wird „Zahlen“ freigeschaltet.';
  if (ctx.registerListFailureKind === 'forbidden') {
    return '„Zahlen“ ist deaktiviert: keine wählbare Kasse — Administrator kann zuweisen oder Berechtigung erteilen.';
  }
  if (ctx.registerListFailureKind === 'unauthorized') return '„Zahlen“ ist deaktiviert: bitte erneut anmelden.';
  if (ctx.registerListFailureKind === 'network' || ctx.registerListFailureKind === 'unknown') {
    return '„Zahlen“ ist deaktiviert: Kassenliste fehlt — „Erneut laden“ oder Administrator.';
  }
  if (ctx.registerPicklistCount > 0) {
    return '„Zahlen“ ist deaktiviert: keine verwendbare Kasse — unten wählen oder in den Einstellungen zuweisen.';
  }
  return '„Zahlen“ ist deaktiviert: Kasse anlegen/öffnen oder in den Einstellungen zuweisen.';
}

export function registerGateAlertMessage(ctx: PosRegisterGateContext): string {
  if (ctx.settingsLoadFailed) {
    return 'Kasseneinstellungen konnten nicht geladen werden. Verbindung prüfen oder erneut anmelden.';
  }
  if (ctx.posReadinessMessageCode === POS_READINESS_MESSAGE_CODES.CONFLICT) {
    return 'Diese Kasse wird bereits von einem anderen Benutzer verwendet.';
  }
  if (ctx.posReadinessMessageCode === POS_READINESS_MESSAGE_CODES.ACTOR_ALREADY_OPEN) {
    return 'Sie haben bereits eine andere Registrierkasse geöffnet. Bitte schließen Sie diese zuerst.';
  }
  if (
    ctx.posReadinessNextAction === 'open_register' &&
    ctx.posReadinessMessageCode === POS_READINESS_MESSAGE_CODES.CLOSED
  ) {
    return 'Die Kasse ist geschlossen. Bitte zuerst öffnen (Schicht).';
  }
  if (
    ctx.posReadinessNextAction === 'open_register' &&
    ctx.posReadinessMessageCode === POS_READINESS_MESSAGE_CODES.FORBIDDEN
  ) {
    return 'Sie dürfen diese Kasse nicht öffnen. Bitte Administrator oder berechtigte Rolle.';
  }
  if (ctx.posReadinessError) {
    return 'Kassenbereitschaft nicht ladbar. Verbindung prüfen oder erneut versuchen.';
  }
  if (ctx.posReadinessNextAction === 'select_register' && ctx.registerPicklistCount === 0) {
    return 'Bitte weisen Sie in den Einstellungen eine Kasse zu, oder bitten Sie den Administrator — hier ist keine Auswahl verfügbar.';
  }
  if (ctx.registerPicklistCount > 0) {
    return 'Bitte wählen Sie zuerst eine Kasse aus der Liste, bevor Sie bezahlen.';
  }
  if (ctx.registerListFailureKind === 'forbidden') {
    return 'Keine Kasse wählbar: fehlende Berechtigung oder leere Liste. Administrator informieren.';
  }
  if (ctx.registerListFailureKind === 'unauthorized') {
    return 'Anmeldung ungültig. Bitte erneut anmelden.';
  }
  if (ctx.registerListFailureKind === 'network' || ctx.registerListFailureKind === 'unknown') {
    return 'Kassenliste nicht ladbar. Verbindung prüfen oder „Erneut laden“.';
  }
  return 'Keine geöffnete Registrierkasse verfügbar oder zugewiesen. Kasse anlegen/öffnen oder zuweisen.';
}

export function mapBackendCashRegisterCodeToGerman(code: string | undefined): string | null {
  if (!code) return null;
  switch (code) {
    case POS_CASH_REGISTER_CODES.REQUIRED:
      return 'Kasse erforderlich: Bitte eine Registrierkasse zuweisen.';
    case POS_CASH_REGISTER_CODES.INVALID:
      return 'Ungültige Kassen-ID.';
    case POS_CASH_REGISTER_CODES.NOT_FOUND:
      return 'Kasse wurde nicht gefunden.';
    case POS_CASH_REGISTER_CODES.FORBIDDEN:
      return 'Diese Kasse ist für Ihren Benutzer nicht freigegeben.';
    case POS_CASH_REGISTER_CODES.CLOSED:
      return 'Die Kasse ist nicht geöffnet. Bitte zuerst die Kasse öffnen oder eine andere wählen.';
    case POS_CASH_REGISTER_CODES.SELECTION_REQUIRED:
      return 'Mehrere Kassen: Bitte in den Einstellungen oder unten eine Kasse auswählen.';
    default:
      return null;
  }
}
