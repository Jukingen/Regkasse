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
    pagePrimaryOperationalTruthLead:
        'Primäre operative Oberfläche für FinanzOnline je Zahlung: serverseitig gefilterte Abgleichstabelle und Zeilenaktionen (siehe Vertrags- und Metrik-Hinweise auf dieser Seite).',
    relatedSupportingLabel: 'Verwandt (unterstützend / Diagnose)',
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
    metricsFailureKindScope:
        'Transient / Permanent / Unbekannt oben sind Laufzeit-Zähler aus der Metrik-API — keine zeilenweise Fehlerklasse in der Abgleichsliste.',
    /** Retry UI honesty */
    foStatusColumnTooltip:
        'FinanzOnline-Status aus der Abgleich-API (keine eigene Fehlerklassifikation im Datenmodell).',
    foActionColumnTooltip:
        'Spiegelt nur, ob in dieser Ansicht der Button »Erneut senden« erscheint — kein separates Backend-Feld „retryable“ und keine Terminalitäts-Garantie.',
    foTimelineColumnTooltip:
        'createdAt = Zeitpunkt der Zahlungs-/Abgleichszeile; finanzOnlineRetryCount = Anzahl Versuche; finanzOnlineLastAttemptAtUtc = Zeitpunkt des letzten Sendeversuchs (unabhängig vom Ergebnis — es gibt kein separates letztes Erfolgs-/Fehlerdatum im Listen-DTO).',
    foErrorShortTooltip:
        'finanzOnlineError = einzige technische Servermeldung in diesem Listen-DTO (kein separates Roh-HTTP-/Payload-Feld). Kurzfassung in der Tabelle; vollständiger Text unter »Zeile erweitern«.',
    contractTruthPanelTitle: 'Listen-Vertrag: was die API je Zeile liefert',
    contractTruthInDtoTitle: 'Im DTO vorhanden (Rohfelder)',
    contractTruthNotInDtoTitle: 'Nicht im Listen-DTO (OpenAPI) — keine UI-Erfindung',
    contractTruthInDtoBullets: [
        'finanzOnlineStatus, finanzOnlineError, finanzOnlineReferenceId',
        'finanzOnlineRetryCount, finanzOnlineLastAttemptAtUtc (ein Zeitstempel für den letzten Versuch, alle Outcomes)',
        'paymentId, receiptNumber, totalAmount, cashRegisterId, createdAt',
    ],
    contractTruthNotInDtoBullets: [
        'Keine Correlation-ID pro Zeile (nur URL-Kontext investigationBatchCorrelationId zwischen Oberflächen).',
        'Keine Fehlerklasse pro Zeile (transient/permanent) — nur Metrik-Aggregat oben.',
        'Kein FinanzOnline-Umgebungs- oder Betriebsmodus-Feld in dieser Liste.',
        'Kein separates letztes Erfolgs- bzw. letztes Fehler-Datum.',
        'Kein separates Roh-Antwortfeld neben finanzOnlineError.',
        'Kein explizites retryable-Feld — »Erneut senden« folgt nur Status + vorhandener paymentId.',
    ],
} as const;

/**
 * FO high-level surfaces: connection/status API and dashboard metrics — not row-level reconciliation.
 */
export const OPERATOR_FO_SUMMARY_SCREEN_COPY = {
    connectionMetricsNotPaymentRowTruth:
        'Verbindung und diese Kennzahlen ersetzen keinen zahlungsbezogenen Zeilen-Abgleich (operativ).',
    abgleichPrimaryLinkLabel: 'FinanzOnline-Abgleich (Zahlungs-/Zeilenebene)',
    /** General Status card: secondary link to integration/diagnostics route */
    operationsSupportingLinkLabel: 'FinanzOnline Operations (Integration & Diagnose)',
    dashboardMetricsCardTitle: 'FinanzOnline · Kennzahlenüberblick',
    dashboardMetricsCardFootnote:
        'Nur aggregierte Metriken — keine vollständige Abgleichsliste. Für Zahlungs-/Zeilenebene den Abgleich öffnen.',
} as const;

/** RKSV General Status: connection/summary APIs only */
export const OPERATOR_RKSV_GENERAL_STATUS_COPY = {
    pageScopeAlertMessage: 'Nur Schnittstellen-Übersicht',
    pageScopeAlertDescriptionBeforeLink:
        'TSE- und FinanzOnline-Karten zeigen Felder der jeweiligen Status-APIs — nicht die zahlungsbezogene Abgleichsliste. Für operative Zeilenebene:',
} as const;

/** FinanzOnline Operations route: integration/diagnostics (not primary reconciliation list) */
export const OPERATOR_FO_OPERATIONS_PAGE_COPY = {
    pageTitle: 'FinanzOnline — Integration & Diagnose',
    breadcrumbTitle: 'FinanzOnline Integration & Diagnose',
    introLead:
        'Unterstützende Oberfläche: Integration, Konfiguration, Verbindungstest, Fehler- und Rechnungsverlauf — nicht die primäre Abgleichsliste. Operative Zahlungswahrheit:',
    introAbgleichLinkLabel: 'FinanzOnline-Abgleich',
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

const VERIFICATIONS_TOTAL_COUNT_OMITTED_NOTE =
    'Die API hat keine Gesamtzahl (totalCount) zu dieser Abfrage geliefert — die Seitensteuerung unten wechselt nur Rohseiten der Anfrage; ohne Gesamtzahl ist der Umfang des Bestands hier nicht belegbar.';

export const OPERATOR_VERIFICATIONS_COPY = {
    /** Page header — not a dedicated “verification results” pipeline */
    pageTitle: 'RKSV Audit-Spur (Signatur / Offline)',
    /** Shown under title — investigation framing */
    pageSubtitle:
        'Untersuchung über Audit-Logs (AuditLogEntryDto) — keine kanonische Verification-Result-Liste und keine Signatur-Debug-Antwort auf dieser Seite.',
    breadcrumbTitle: 'Audit-Spur',
    /** Sidebar + RKSV hub link text; page titles keep full context */
    navMenuLabel: 'Audit-Spur',
    filteredBannerTitle: 'Audit-Logs (Correlation-Filter aktiv)',
    diagnosticLine:
        'Diagnose-Ansicht: kein Ersatz für Incident-Aggregat oder Abgleichstabelle — nur Audit-Ereignisse aus der Audit-API.',
    /** Stichwort-Filter auf dem Client; keine typisierten Verification-Result-Objekte */
    keywordSampleFootnote:
        'Die Tabelle zeigt eine Stichprobe: Audit-Einträge, deren Aktion oder Entitätstyp Schlüsselwörter wie signatur, offline, receipt, payment enthält. Backend-Änderungen an Aktionsnamen können Treffer still verändern.',
    /** Table footer pagination: server-driven AuditLog pages */
    verificationsServerPaginationNote:
        'Tabellen-Pagination wechselt die Audit-API-Seite (serverseitig). Stichwort- und Schalter-Filter gelten nur für die jeweils geladene Seite — auf anderen Seiten können die Trefferzahlen abweichen.',
    /** Table footer pagination: client slice of correlation response */
    verificationsClientPaginationNote:
        'Tabellen-Pagination: seitenweise durch die gefilterte Liste dieser Correlation-Antwort (clientseitig, keine zusätzliche API-Anfrage).',
    /** When GET /api/AuditLog omits totalCount — avoids implying global completeness */
    verificationsTotalCountOmittedNote: VERIFICATIONS_TOTAL_COUNT_OMITTED_NOTE,
    /** Operator alert: client filters removed every row from the current payload */
    verificationsNoRowsAfterFiltersTitle: 'Keine Tabellenzeilen bei aktuellen Filtern',
    verificationsNoRowsAfterFiltersBody: (apiRows: number) =>
        `Die Audit-API hat ${apiRows} Rohzeile(n) für diese Ansicht geliefert; Stichwort-Stichprobe und/oder Schalter blenden alles aus. Die Eingrenzung gilt nur für die bereits geladene Liste (bei offener Liste: nur die aktuelle API-Seite) — nicht stillschweigend für das gesamte Audit-Log.`,
    /** Compact scope reminder when some raw rows are hidden by keyword sample */
    verificationsPartialScopeAlertTitle: 'Teilansicht dieser geladenen Antwort',
    verificationsPartialScopeNote: (apiRows: number, keywordRows: number, displayedRows: number) =>
        `Sichtbarkeitsumfang (nur diese geladene Antwort): ${apiRows} Rohzeilen → ${keywordRows} nach Stichwort-Stichprobe → ${displayedRows} mit Schaltern. Weitere API-Seiten werden nicht automatisch zusammengeführt.`,
    /** Ant Design Table empty — no raw rows on this server page */
    verificationsTableEmptyNoRawRows: 'Keine Audit-Zeilen in dieser API-Antwort (Rohseite). Andere Seiten prüfen oder Filter in der API-Abfrage anpassen, falls verfügbar.',
    /** Ant Design Table empty — correlation response had nothing */
    verificationsTableEmptyCorrelation: 'Keine Audit-Zeilen in dieser Correlation-Antwort.',
    /** Table body empty while raw payload had rows (client filters) */
    verificationsTableEmptyAllFiltered:
        'Alle Rohzeilen dieser Antwort sind ausgeblendet (Stichwort-Stichprobe oder Schalter). Der Hinweis oberhalb der Tabelle erläutert den Umfang.',
    /** Pagination showTotal — server-driven pages, async Table mode */
    verificationsServerPaginationShowTotal: (
        range: [number, number],
        displayedRows: number,
        apiRows: number,
        totalCount: number | undefined,
    ) => {
        const tablePart = `Tabelle: ${range[0]}–${range[1]} von ${displayedRows} sichtbaren Zeilen (Stichwort+Schalter)`;
        const rawPart = ` · Rohantwort: ${apiRows} Zeile(n)`;
        const globalPart =
            totalCount !== undefined
                ? ` · Gesamt Audit-Log laut API: ${totalCount} (Pagination = API-Seiten)`
                : ` · Gesamtbestand: nicht gemeldet (Pagination = API-Seitenwechsel)`;
        return `${tablePart}${rawPart}${globalPart}`;
    },
    /** Pagination showTotal — client slice of one correlation response */
    verificationsCorrelationPaginationShowTotal: (range: [number, number], filteredTotal: number) =>
        `Tabelle: ${range[0]}–${range[1]} von ${filteredTotal} gefilterten Zeilen (eine Correlation-Antwort, clientseitig paginiert)`,
    /**
     * Single collapsed panel: pagination semantics, optional correlation how-to (list mode),
     * keyword sampling, API-total caveat, contract boundary — keeps the viewport focused on the table.
     */
    verificationsContextCollapseTitle: 'Kontext, Methodik, Pagination und Vertragsgrenze',
    /** One scannable line — primary hierarchy above the table */
    verificationsPrimaryStripList: (
        displayedRows: number,
        keywordRows: number,
        apiRows: number,
        meta?: { totalCount?: number },
    ) => {
        const parts = [
            `${displayedRows} sichtbar in der Tabelle (Stichwort+Schalter)`,
            `${apiRows} Rohzeilen in dieser API-Antwort`,
            `${keywordRows} Treffer der Stichwort-Stichprobe`,
        ];
        if (meta?.totalCount != null) {
            parts.push(`Gesamt laut API: ${meta.totalCount} Audit-Einträge`);
        }
        return parts.join(' · ');
    },
    verificationsPrimaryStripCorrelation: (displayedRows: number, keywordRows: number, apiRows: number) =>
        `${displayedRows} sichtbar in der Tabelle · ${apiRows} Rohzeilen in der Correlation-Antwort · ${keywordRows} Stichwort-Treffer`,
    /** Inside collapse — current API page position (list mode) */
    verificationsCollapseApiPageLine: (
        page: number,
        pageSize: number,
        totalPages: number | undefined,
    ) =>
        totalPages != null
            ? `Abfrage-Seite ${page} von ${totalPages} (${pageSize} Einträge pro Anfrage).`
            : `Abfrage-Seite ${page} (${pageSize} Einträge pro Anfrage).`,
    unfilteredSummary: (
        apiRows: number,
        keywordRows: number,
        displayedRows: number,
        meta?: {
            totalCount?: number;
            page?: number;
            pageSize?: number;
            totalPages?: number;
            /** false when the API payload has no totalCount field (undefined), after load */
            totalCountReported?: boolean;
        },
    ) => {
        const parts: string[] = [];
        if (meta?.totalCount != null) {
            parts.push(`Gesamt laut API: ${meta.totalCount} Audit-Einträge`);
        }
        if (meta?.page != null && meta?.pageSize != null) {
            parts.push(
                `Seite ${meta.page}${meta.totalPages != null ? ` von ${meta.totalPages}` : ''} (${meta.pageSize} pro Anfrage)`,
            );
        }
        parts.push(`${apiRows} Zeilen in dieser Antwort`);
        parts.push(`Stichwort-Stichprobe auf dieser Seite: ${keywordRows}`);
        parts.push(`mit Schaltern angezeigt: ${displayedRows}`);
        let out = `${parts.join(' · ')}.`;
        if (meta?.totalCountReported === false) {
            out = `${out} ${VERIFICATIONS_TOTAL_COUNT_OMITTED_NOTE}`;
        }
        return out;
    },
    filteredSummary: (apiRows: number, keywordRows: number, displayedRows: number) =>
        `Audit-API (Correlation): ${apiRows} Zeilen in der Antwort · nach Stichwort-Stichprobe ${keywordRows} · mit Schaltern angezeigt ${displayedRows}.`,
    rowSourceBadgeShort: 'Audit-API',
    rowSourceBadgeTooltip:
        'Zeile stammt aus GET /api/AuditLog (Orval: AuditLogEntryDto). Kein separater Verification-Result-Endpunkt und keine Signatur-Debug-Antwort auf dieser Seite.',
    linksColumnTooltip:
        'Links nur bei Payment/Receipt und nur wenn entityId eine gültige Nicht-Nil-UUID ist (gleiche UUID-Policy wie Register-Deep-Links).',
    correlationColumnTooltip:
        'correlationId aus der Audit-Zeile. Link setzt den Correlation-Query auf dieser Seite (ersetzt die aktuelle Filterung).',
    filterByThisCorrelationLabel: 'Diese Correlation',
    correlationFilterHintTitle: 'Correlation eingrenzen',
    correlationFilterHintBody:
        'Nutzen Sie den Link «Diese Correlation» in einer Zeile oder setzen Sie die URL-Query correlationId. Die Audit-API liefert dann GET /api/AuditLog/correlation/{id} für dieselbe Correlation.',
    deepLinkPaymentLabel: 'Zahlungen (paymentId-Filter)',
    deepLinkReceiptLabel: 'Belegdetail',
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
    /** List page (`/invoices`): operator framing aligned with de-DE truth copy */
    pageTitle: 'Rechnungen',
    listPageLead:
        'Servergefilterte Rechnungsliste mit Stapelaktionen (Druck, Export, Abgleich). Details öffnen für TSE-/Kontextspuren und FinanzOnline-Verknüpfungen.',
    actionBatchPrint: 'Stapel: Druck',
    actionBatchExport: 'Stapel: Export',
    actionBatchReconcile: 'Stapel: Abgleich',
    actionExportCsvAll: 'CSV-Export (alle)',
    actionRefresh: 'Aktualisieren',
    listSearchPlaceholder: 'Suche Nr., Kunde …',
    listStatusPlaceholder: 'Status',
    listRegisterPlaceholder: 'Register-UUID (cash_registers.Id)',
    clearRegisterFilter: 'Filter löschen',
    emptyListDefault: 'Keine Rechnungen. Filter anpassen.',
    emptyListDateRange: 'Keine Rechnungen im gewählten Zeitraum. Filter anpassen.',
    clientFilteredRowHint:
        'Hinweis: Sichtbare Zeilen sind clientseitig nach Register-Link gefiltert — Gesamtzahl und Seiten beziehen sich weiterhin auf die Serverliste.',
    activeFiltersLabel: 'Aktive Filter',
    clearAllFilters: 'Alle Filter zurücksetzen',
    filterTagSearchPrefix: 'Suche',
    filterTagStatusPrefix: 'Status',
    filterTagDateRangePrefix: 'Zeitraum',
    filterTagRegisterUuid: 'Register (UUID)',
    filterTagRegisterApi: 'Register (API)',
    filterTagInvalidRegisterShort: 'Ohne Register-UUID (Client)',
    listSummaryApiTotal: 'Treffer gesamt (API)',
    listSummaryRowsThisPage: 'Zeilen auf dieser Seite',
    listSummaryClientFilterNote:
        'Client-Filter aktiv: sichtbare Zeilen können auf einer Seite weniger sein als die eingestellte Seitengröße.',
    paginationZeroResults: '0 Treffer',
    emptyListMoreHint:
        'Tipp: Suche, Status, Zeitraum oder Register eingrenzen. Storno-Referenz und vollständige TSE-Daten siehe Detailansicht.',
    listLoadingTip: 'Rechnungen werden geladen…',
    listRefreshingHint: 'Aktualisiert …',
    listStaleAfterErrorNote:
        'Hinweis: Tabelle zeigt die letzte erfolgreiche Antwort — der aktuelle Abruf ist fehlgeschlagen.',
    listErrorGeneric:
        'Die Liste konnte nicht geladen werden. Bitte erneut versuchen oder die Filter prüfen.',
    listErrorUnauthorized: 'Nicht angemeldet oder Sitzung abgelaufen. Bitte neu anmelden.',
    listErrorForbidden: 'Keine Berechtigung für die Rechnungsliste.',
    listErrorNotFound: 'Listen-Endpunkt nicht gefunden. Bitte Support informieren.',
    listErrorServer: 'Serverfehler. Bitte später erneut versuchen.',
    listErrorNetwork: 'Netzwerkfehler. Verbindung prüfen und erneut versuchen.',
    emptyListLoadFailedTitle: 'Liste nicht verfügbar',
    emptyListLoadFailedHint:
        'Verbindung, Berechtigung und Filter prüfen. Filter lockern oder zurücksetzen, dann erneut laden.',
    dateRangeBlocksQueryTitle: 'Abfrage pausiert',
    dateRangeBlocksQuerySuffix: 'Die Liste wird erst geladen, wenn der Zeitraum gültig ist.',
    emptyListInvalidDateRange: 'Ungültiger Zeitraum',
    listColumnKassenShort: 'Kasse (Anz.)',
    listColumnTseShort: 'TSE',
    listColumnInvoiceNumber: 'Rechnung Nr.',
    listColumnDate: 'Datum',
    listColumnTotal: 'Betrag',
    listColumnStatus: 'Status',
    listColumnActions: 'Aktionen',
    rowActionDetailTooltip: 'Details',
    /** Row action tooltips (icon buttons use compact labels + these hints). */
    rowActionDetailExtendedTooltip: 'Rechnungsdialog: Positionen, TSE, Register, FinanzOnline-Kontext.',
    rowActionPrintTooltip: 'PDF im neuen Tab öffnen',
    rowActionPrintCompact: 'Druck',
    rowActionCreditNoteTooltip: 'Gutschrift erstellen (Dialog)',
    rowActionCreditCompact: 'Gutschrift',
    detailModalTitlePrefix: 'Rechnung:',
    registerFilterInvalidTitle: 'Register-Filter ohne sicheren Deep-Link',
    registerFilterInvalidDescription:
        'Der Wert wird an die Listen-API übergeben, erfüllt aber nicht die UUID-Regel für sichere Kassen-Deep-Links (FinanzOnline-Abgleich). Kasse in der Abgleichsansicht gezielt wählen.',
    registerListFilterApiFootnote:
        'Freitext wird unverändert an die Listen-API übergeben; Deep-Links zur Abgleichsseite nur mit gültiger UUID.',
    invalidRegisterOnlyCheckboxLabel: 'Nur ohne gültige Register-UUID (kein Link-FK)',
    reconciliationHandoffFooter:
        'Öffnet die FinanzOnline-Abgleichsansicht mit Filtern. Gültige Payment- und Batch-Correlation-IDs werden in der URL als Kontext bzw. Fokus mitgeführt — siehe Hinweis auf der Abgleichsseite.',
    /** Shown when invoice.cashRegisterId is non-empty but not a link-safe non-nil UUID — URL omits cashRegisterId query. */
    reconciliationHandoffRegisterFilterOmitted:
        'Hinweis: Der Register-FK dieser Rechnung ist nicht link-sicher (kein gültiger Nicht-Nil-UUID). Die Abgleich-URL setzt daher keinen cashRegisterId-Filter; Zahlungs- und Batch-Kontext in der URL bleiben erhalten.',
    detailProvenanceFooter:
        'Ohne typisiertes Feld invoiceDataProvenance im OpenAPI-Client bleibt Persistenz vs. Zahlungsableitung unscharf (keine Heuristik aus anderen Feldern). Verknüpfungen nutzen cashRegisterId (Maschinenbezug), nicht kassenId — siehe Vertragslücken.',
    /** Shown when JSON still carries invoiceDataProvenance before Orval schema catch-up. */
    detailProvenanceUntypedApiNote:
        'Wert aus API-Antwort; im generierten Client noch nicht als Pflichtfeld typisiert.',
    detailRegisterMachineLabel: 'Register (Maschinenbezug)',
    detailFoLinkWithContext: 'FinanzOnline-Abgleich (mit URL-Kontext)',
    detailFoLinkRegisterOnly: 'FinanzOnline-Abgleich (diese Kasse, mit URL-Kontext)',
    correlationPathsLabel: 'Batch-Correlation / Untersuchung',
    contractInvoiceItemsTitle: 'Vertragslage (Positionen)',

    /** --- Invoice list surface: consistent de-DE (domain terms FinanzOnline / TSE / Abgleich / UUID kept) --- */
    listColumnCustomer: 'Kunde (Snapshot)',
    creditNoteTagShort: 'GS',

    invoiceStatusDraft: 'Entwurf',
    invoiceStatusSent: 'Gesendet',
    invoiceStatusPaid: 'Bezahlt',
    invoiceStatusPartiallyPaid: 'Teilweise bezahlt',
    invoiceStatusUnpaid: 'Offen',
    invoiceStatusOverdue: 'Überfällig',
    invoiceStatusCancelled: 'Storniert',
    invoiceStatusCreditNote: 'Gutschrift',
    invoiceStatusUnknown: 'Unbekannt',

    paymentMethodBar: 'Bar',
    paymentMethodCard: 'Karte',
    paymentMethodTransfer: 'Überweisung',
    paymentMethodCheck: 'Scheck',
    paymentMethodVoucher: 'Gutschein',
    paymentMethodMobile: 'Mobil',

    displayUnknownInvoice: 'Unbekannt',

    toastCreditNoteCreated: 'Gutschrift erstellt.',
    toastCreditNoteExists: 'Für diese Rechnung existiert bereits eine Gutschrift.',
    toastCreditNoteBadRequest: 'Ungültige Anfrage.',
    toastCreditNoteFailed: 'Gutschrift konnte nicht erstellt werden.',

    toastBatchPrint: (ok: number, fail: number) =>
        `Stapel-Druck: ${ok} erfolgreich, ${fail} fehlgeschlagen`,
    toastBatchExport: (ok: number, fail: number) =>
        `Stapel-Export: ${ok} exportiert, ${fail} fehlgeschlagen`,
    toastExportCsvOk: 'CSV-Export gestartet.',
    toastExportCsvFailed: 'CSV-Export fehlgeschlagen.',
    toastPdfSessionExpired: 'Sitzung abgelaufen. Bitte erneut anmelden.',
    toastPdfNotFound: 'Rechnung nicht gefunden oder gelöscht.',
    toastPdfFailed: 'PDF konnte nicht erzeugt werden.',
    toastReconciliationRetryTriggerFailed: 'Abgleich-Retry konnte nicht ausgelöst werden.',

    batchReconcileModalTitle: 'Stapel: FinanzOnline-Abgleich erneut senden?',
    batchReconcileModalBody: (n: number) =>
        `FinanzOnline-Abgleich für ${n} ausgewählte Zeile(n) anstoßen?`,
    batchReconcileModalOk: 'Ausführen',
    batchReconcileModalCancel: 'Abbrechen',
    toastBatchReconcileSummary: (ok: number, fail: number, skipped: number, already: number) =>
        `Abgleich: ${ok} ok, ${fail} fehlgeschlagen, ${skipped} übersprungen, ${already} bereits übermittelt.`,

    handoffLabelSubmissionId: 'Submission-ID',
    handoffLabelSubmittedAt: 'Übermittelt am',

    detailModalClose: 'Schließen',
    detailModalPrint: 'Drucken',
    detailModalReconciliationRetry: 'Abgleich erneut senden',
    detailEmptyLoadFailed: 'Keine Details geladen oder Fehler beim Laden.',

    descLabelCompany: 'Unternehmen',
    descLabelTotalAmount: 'Brutto gesamt',
    descLabelTaxAmount: 'Steuerbetrag',
    descLabelPaymentMethod: 'Zahlungsart',

    creditNoteModalTitle: 'Gutschrift erstellen',
    creditNoteModalOk: 'Gutschrift erstellen',
    creditNoteModalCancel: 'Abbrechen',
    creditNoteAlertMessage: 'Hinweis zur Gutschrift',
    creditNoteAlertDescription: 'Erstellt eine Gutschrift mit negativen Beträgen (buchhalterische Wirkung prüfen).',
    formReasonCodeLabel: 'Grund (Code)',
    formReasonTextLabel: 'Begründung',
    formReasonPlaceholder: 'Grund wählen…',
    formReasonCodeRequired: 'Bitte einen Grund wählen.',
    formReasonTextRequired: 'Bitte die Begründung eingeben.',
    formReasonTextAreaPlaceholder: 'Kurz begründen, warum die Gutschrift nötig ist …',

    creditReasonReturn: 'Retoure',
    creditReasonError: 'Rechnungsfehler',
    creditReasonDiscount: 'Rabatt',
    creditReasonCancel: 'Vollstornierung',
    creditReasonOther: 'Sonstiges',

    csvExportHeaderRow:
        'Rechnungsnummer;Rechnungsdatum;Kundenname;Firmenname;Bruttobetrag;Status;Belegart;StornoReferenz;KassenIdAnzeige;RegisterFK;TseSignatur',

    toastNoPaymentLinkedForReconcile: 'Kein verknüpftes Payment für den Abgleich gefunden.',
    finanzOnlineToastSubmitOk: 'FinanzOnline-Übermittlung erfolgreich abgeschlossen.',
    finanzOnlineToastAlreadySubmitted: 'Bereits als übermittelt markiert.',
    modalReconciliationSuccessTitle: 'Abgleich erfolgreich',
    modalAlreadySubmittedTitle: 'Bereits übermittelt',
    modalReconciliationFailedTitle: 'Abgleich fehlgeschlagen',
    modalReconciliationErrorTitle: 'Abgleich-Fehler',
    modalOpenFinanzOnlineQueue: 'Zum Abgleich',
    batchReconcileFinishedTitle: 'Stapel-Abgleich abgeschlossen',

    descRegisterFkMachine: 'Register (FK, nur Maschine)',
    descKassenIdDisplay: 'Kassen-ID / Nummer (Anzeige)',
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
    /** Opens audit-spur page with correlation query */
    verificationsAudit: 'Audit-Spur (Correlation)',
} as const;
