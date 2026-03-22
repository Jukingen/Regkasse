/**
 * Central German (de-DE) copy for operator truth, diagnostics, and investigation UX.
 * Do not imply legal/accounting finality where data is derived, diagnostic, or best-effort.
 *
 * i18n: Strings are keyed by semantic role; for a translation layer, map these keys to catalogs
 * without renaming concepts (API / Anzeige / Verknüpft / Diagnose / Abgleich / Aggregat).
 */

// --- Provenance badges (short cell label + hover + Tag color) ---

export const OPERATOR_TRUTH_BADGE_KINDS = [
    'authoritative_api',
    'derived_from_foreign_row',
    'display_only_label',
    'diagnostic_support',
    'link_incomplete',
] as const;

export type OperatorTruthBadgeKind = (typeof OPERATOR_TRUTH_BADGE_KINDS)[number];

type BadgeDef = { shortLabel: string; tooltip: string; antColor: string };

export const OPERATOR_TRUTH_BADGE: Record<OperatorTruthBadgeKind, BadgeDef> = {
    authoritative_api: {
        shortLabel: 'API',
        tooltip:
            'Wert kommt direkt aus dem zugehörigen API-Feld. Für technische Verknüpfungen (Filter, Deep-Links) nutzbar — keine Bewertung von Buchhaltung oder Rechtskonformität.',
        antColor: 'geekblue',
    },
    derived_from_foreign_row: {
        shortLabel: 'Verknüpft',
        tooltip:
            'Wert stammt aus einer anderen API-Zeile (z. B. FinanzOnline-Abgleich), nicht aus der Primärzeile dieser Tabelle.',
        antColor: 'purple',
    },
    display_only_label: {
        shortLabel: 'Anzeige',
        tooltip:
            'Nur Anzeige- oder Textkennung (z. B. Kassen-ID auf dem Beleg). Nicht als alleiniger Maschinenbezug für Links oder Filter verwenden.',
        antColor: 'gold',
    },
    diagnostic_support: {
        shortLabel: 'Diagnose',
        tooltip: 'Unterstützung für Support und Analyse; keine primäre Geschäfts- oder Buchungswahrheit.',
        antColor: 'default',
    },
    link_incomplete: {
        shortLabel: 'Ohne Link',
        tooltip:
            'Kein gültiger Register-UUID-Bezug in den API-Feldern — eingrenzende Deep-Links (z. B. Abgleich pro Kasse) sind nicht zuverlässig möglich.',
        antColor: 'warning',
    },
};

// --- Triage layout (three-layer framing) ---

export const OPERATOR_TRIAGE_COPY = {
    summaryStripLabel: 'Operativ – Kurzüberblick',
    businessDefaultTitle: 'Beleg & Geschäftsdaten',
    technicalTitle: 'Technik & Rohdaten',
    technicalIntro:
        'Signatur, Positionen und strukturierte Rohausgaben — für Support und Nachweis (Diagnose, keine alleinige Geschäftswahrheit). Zuerst den operativen Block und die Geschäftsdaten prüfen.',
} as const;

// --- Shared loading / error / empty (honest, no false certainty) ---

export const OPERATOR_SHARED_COPY = {
    unknownErrorDetail: 'Keine technische Detailmeldung verfügbar.',
    /** Use with Error.message when present */
    loadFailedList: 'Liste konnte nicht geladen werden',
    loadFailedBatch: 'Batch konnte nicht geladen werden',
    loadFailedIncident: 'Incident-Aggregat konnte nicht geladen werden',
    notFoundIncidentTitle: 'Kein Incident-Aggregat',
    notFoundIncidentDescription:
        'Für diese Correlation-ID liefert die API kein zusammengefasstes Ergebnis — nicht als „keine Daten in der Kasse“ interpretieren.',
    loadingIncident: 'Lade Incident-Aggregat…',
    loadingBatchDetail: 'Lade Batch-Details…',
    loadingInvoiceDetail: 'Rechnungsdetails werden geladen…',
    emptyBatchForCorrelation: 'Keine Batch-Details für diese Correlation-ID.',
    /** Refetch / stale: operator hint */
    refetchHintToolbar: 'Daten manuell aktualisieren (Cache kann veraltet sein).',
    /** Cross-screen navigation block heading (links to incident / replay / FO queue) */
    investigateFurtherLabel: 'Weiter untersuchen',
    retryLoadShort: 'Erneut laden',
} as const;

// --- Investigation URL context (display-only vs API filter) ---

export const OPERATOR_INVESTIGATION_CONTEXT_COPY = {
    bannerTitle: 'Untersuchungskontext (nur Anzeige, kein API-Filter)',
    bannerBody:
        'Die Abgleichsliste wird weiterhin nur über Status, Kasse und Zeitraum vom Server gefiltert. Die Batch-Correlation in der URL dient der Orientierung beim Wechsel zwischen Oberflächen.',
    focusPaymentLine: 'Zusätzlich — nur Zeilenhervorhebung in der aktuellen Ergebnisliste (kein Server-Filter):',
    focusPaymentOnlyTitle: 'Fokus-Zahlung (nur Hervorhebung)',
    focusPaymentOnlyBody:
        'Die passende Zeile wird in der geladenen Tabelle markiert. Liegt die Zahlung außerhalb von Limit oder Zeitraum, erscheint sie nicht — das ist kein Beleg dafür, dass sie im System fehlt.',
    syncUrlWithFiltersLink: 'URL mit aktuellen Filtern übernehmen',
    /** FO rows have no correlation field */
    foRowsNoCorrelationNote:
        'Abgleichszeilen enthalten keine Batch-Correlation — die URL übernimmt sie nur zur Orientierung zwischen Incident, Replay und dieser Ansicht.',
} as const;

// --- FinanzOnline reconciliation queue ---

export const OPERATOR_FO_QUEUE_COPY = {
    queryRejectedRegisterTitle: 'Register-Parameter ungültig',
    queryRejectedRegisterDescription: (raw: string) =>
        `«${raw}» ist keine gültige Register-UUID — wird nicht als Kassenfilter gesetzt (verhindert irreführende Deep-Links).`,
    queryRejectedFocusPaymentTitle: 'Zahlungs-Fokus verworfen',
    queryRejectedFocusPaymentDescription: (raw: string) =>
        `«${raw}» ist keine gültige Zahlungs-UUID — keine Zeilenhervorhebung (verhindert falsche Zuordnung).`,
    emptyListTitle: 'Keine Abgleichszeilen',
    emptyListDescription: 'Keine Zahlungen für die gewählten Filter. Status oder Zeitraum anpassen.',
    businessSectionTitle: 'Suchkontext & Abgleichszeilen',
    businessSectionDescription:
        'Filter steuern die serverseitige Liste. Kasse nur als gültige UUID aus Stammdaten wählen, damit Deep-Links und API-Filter übereinstimmen.',
    summaryReconciliationParagraph:
        'Zeilen mit Status Pending, Failed oder NeedsReconciliation können mit Erneut senden erneut an FinanzOnline angestoßen werden — das spiegelt nur die UI-/Statuslogik, keine zusätzliche Backend-Garantie. Referenz- und Fehlertexte je Zeile prüfen. Abgleichszeilen enthalten keine Correlation-ID; Zuordnung zu Incident/Replay über andere Ansichten oder den URL-Kontext (investigationBatchCorrelationId).',
    /** Retry UI honesty */
    foStatusColumnTooltip:
        'FinanzOnline-Status aus der Abgleich-API (keine eigene Fehlerklassifikation im Datenmodell).',
    foActionColumnTooltip:
        'Spiegelt nur, ob in dieser Ansicht der Button »Erneut senden« erscheint — kein separates Backend-Feld „retryable“ und keine Terminalitäts-Garantie.',
    foTimelineColumnTooltip:
        'createdAt = Zeitpunkt der Abgleichszeile; Retries / letzter Versuch = finanzOnlineRetryCount / finanzOnlineLastAttemptAtUtc (API-Rohfelder).',
    foErrorShortTooltip: 'Kurzfassung; vollständiger Text unter »Zeile erweitern«.',
} as const;

// --- FinanzOnline retry row (mirrors button, not server terminality) ---

export const OPERATOR_FO_RETRY_UI_COPY = {
    retryAvailable: {
        tagLabel: 'Retry-UI',
        tagColor: 'blue',
        tooltip:
            'In dieser Ansicht ist Erneut senden für diese Zeile aktiv (API-Status: Pending, Failed oder NeedsReconciliation). Terminalität entscheidet das Backend, nicht dieses Label.',
    },
    submittedNoRetry: {
        tagLabel: 'Eingereicht',
        tagColor: 'default',
        tooltip: 'Kein Erneut senden in dieser Tabelle bei API-Status Submitted.',
    },
    otherStatus: {
        tagLabel: 'Sonstiger Status',
        tagColor: 'default',
        tooltip:
            'API-Status nicht in der Retry-Button-Liste — kein Erneut senden in dieser Ansicht (keine automatische „Endgültig“-Bewertung).',
    },
    empty: {
        tagLabel: '—',
        tagColor: 'default',
        tooltip: 'Kein FinanzOnline-Status in der API-Zeile.',
    },
} as const;

// --- Incident ---

export const OPERATOR_INCIDENT_COPY = {
    foStatusFromJoinTooltip: 'FinanzOnline-Status aus der Abgleich-Zeile (Join über paymentId).',
    paymentsCardIntro:
        'Pro Zahlung: Payment und Beleg aus dem Batch; FinanzOnline-Felder aus dem Abgleich-DTO (Join über paymentId) — abgeleitet, nicht als eigene Primärzeile. Zeile erweitern für vollständige Fehlertexte und kopierbare Correlation.',
    foAggregateLine: (submitted: number, openOrProblem: number) =>
        `FinanzOnline (Aggregat über den Incident-Endpunkt): ${submitted} submitted, ${openOrProblem} offen / prüfen — keine Zeilen-genaue FO-Wahrheit.`,
    foActionIncidentTooltip:
        'Gleiche Logik wie »FO-Aktion (UI)« auf der Abgleichsseite (sichtbarer Retry-Button) — keine zusätzliche Backend-Garantie.',
    timesColumnIncidentTooltip:
        'Replay-Zeit aus Batch-Item; FO-Felder aus Abgleich-DTO nur wenn Join über paymentId (abgeleitet).',
    foRefColumnTooltip: 'finanzOnlineReferenceId aus der Abgleich-Zeile (wenn Join).',
    registerFkColumnTooltip:
        'Register-FK aus Abgleich-Zeile bei Treffer (abgeleitet); ReplayBatchPaymentItemDto hat keine Register-FK im API-Vertrag.',
    expandDtoNote:
        'Kein separates Zeilen-Erfolg/Misserfolg-Feld in ReplayBatchPaymentItemDto; kein Actor/Initiator. FO-Daten nur wenn Join über paymentId trifft.',
} as const;

// --- Replay batch detail ---

export const OPERATOR_REPLAY_COPY = {
    investigationPathTitle: 'Untersuchungspfad (getrennte Datenquellen)',
    investigationPathIntro:
        'Jeder Link lädt eine andere API- oder Listenquelle. Keine zusammengelegte „eine Wahrheit“ — Angaben können sich je nach Endpunkt unterscheiden.',
    batchCorrelationContextBadgeNote: 'Batch- und Audit-Correlation auf dieser Seite (API)',
    verificationsFallbackLabel: 'Verifications:',
    verificationsFallbackMid: 'fehlt —',
    verificationsFallbackLinkLabel: 'Audit-Filter mit Batch-Correlation',
    verificationsFallbackAfterLink: '(kann weniger oder andere Treffer liefern).',
    paymentsDtoGapTitle: 'Zeilen ohne Erfolgs-/Fehler-Felder',
    paymentsDtoGapBody:
        'enthält kein Ergebnis pro Zahlung, keine eigene Correlation pro Zahlung und keine FO-Statusfelder — Bewertung über Incident- oder Abgleichsansicht. Zeile erweitern: Correlation-IDs kopieren und Offline-ID.',
    observabilityCoverageFootnote: 'Stichproben aus dem Replay-Loop (Coverage, Diagnose).',
    observabilityOfflineSyncedFootnote: 'Zähler OFFLINE_SYNCED aus Audit-API (Rohwert).',
    observabilityFinalFailureFootnote:
        'Zähler FINAL_FAILURE aus Audit-API — Audit-Label, keine automatische Endgültigkeitsbewertung des Backends.',
} as const;

// --- Verifications (audit list) ---

export const OPERATOR_VERIFICATIONS_COPY = {
    filteredBannerTitle: 'Audit-Logs (gefiltert, Correlation-Parameter)',
    diagnosticLine:
        'Diagnose-Ansicht: keine Ersatz für Incident-Aggregat oder Abgleichstabelle — nur Audit-Ereignisse nach Correlation.',
    unfilteredIntro:
        'Signatur-, Zahlungs- und Offline-Replay-Audit (OFFLINE_CREATED / OFFLINE_SYNCED, max. 100 Einträge).',
    filteredIntro: (count: number) =>
        `Audit-Logs für die gewählte Correlation (${count} Einträge in dieser Antwort — nicht als vollständige Systemabdeckung interpretieren).`,
} as const;

// --- Register deep-link honesty (shared wherever cashRegisterId / FK is shown) ---

export const OPERATOR_REGISTER_LINK_COPY = {
    uuidNotLinkSafeTitle: 'Register-Feld vom Server ohne link-sichere UUID',
    uuidNotLinkSafeDescription:
        'Der angezeigte Wert stammt unverändert aus der API. Er entspricht nicht dem erwarteten UUID-Format (oder ist Nil-UUID). Deep-Link zur Abgleichsseite pro Kasse ist deaktiviert — falsche Identifier werden nicht in die URL gelegt.',
    noMachineUuidHint:
        'Kein gültiger Register-UUID-Bezug — Abgleich pro Kasse nicht zuverlässig per Deep-Link eingrenzbar.',
    missingRegisterFkInApiHint: 'Kein Register-FK in der API-Antwort — nur Anzeige-Kassen-ID prüfen.',
} as const;

// --- Invoice list / detail (truth-critical strings only) ---

export const OPERATOR_INVOICE_COPY = {
    registerFilterInvalidTitle: 'Register-Filter ohne sicheren Deep-Link',
    registerFilterInvalidDescription:
        'Der Wert wird an die Listen-API übergeben, erfüllt aber nicht die UUID-Regel für sichere Kassen-Deep-Links (FinanzOnline-Abgleich). Kasse in der Abgleichsansicht gezielt wählen.',
    registerListFilterApiFootnote:
        'Freitext wird unverändert an die Listen-API übergeben; Deep-Links zur Abgleichsseite nur mit gültiger UUID.',
    invalidRegisterOnlyCheckboxLabel: 'Nur ohne gültige Register-UUID (kein Link-FK)',
    reconciliationHandoffFooter:
        'Öffnet die FinanzOnline-Abgleichsansicht mit Filtern. Gültige Payment- und Batch-Correlation-IDs werden in der URL als Kontext bzw. Fokus mitgeführt — siehe Hinweis auf der Abgleichsseite.',
    detailProvenanceFooter:
        'Verknüpfungen nutzen cashRegisterId (Maschinenbezug), nicht kassenId. Ob die Zeile rein persistiert oder aus Zahlung abgeleitet ist, liefert das API-Detail derzeit nicht als eigenes Feld — siehe Vertragslücken in der technischen Doku.',
    detailRegisterMachineLabel: 'Register (Maschinenbezug)',
    detailFoLinkWithContext: 'FinanzOnline-Abgleich (mit URL-Kontext)',
    detailFoLinkRegisterOnly: 'FinanzOnline-Abgleich (diese Kasse, mit URL-Kontext)',
    correlationPathsLabel: 'Batch-Correlation / Untersuchung',
    contractInvoiceItemsTitle: 'Vertragslage (Positionen)',
} as const;

// --- Cross-screen link labels (stable wording) ---

export const OPERATOR_LINK_LABELS = {
    incidentAggregate: 'Incident (Aggregat)',
    /** Breadcrumb / index — route `/rksv/replay-batch` without correlation */
    replayBatch: 'Replay-Batch',
    /** Detail page — `/rksv/replay-batch/{correlationId}` */
    replayBatchDetail: 'Replay-Batch-Detail',
    finanzQueueContext: 'FinanzOnline-Abgleich (Kontext)',
    /** FO queue opened with register filter only (no investigation URL context) */
    finanzQueueThisRegister: 'FinanzOnline-Abgleich (diese Kasse)',
    verificationsAudit: 'Verifications (Audit)',
} as const;
