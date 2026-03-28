/**
 * Users module copy – i18n-ready keys (replace values or use with t(key) when i18n is added).
 * Password min length from validation.ts (single source of truth; must match backend Identity).
 */
import { PASSWORD_MIN_LENGTH } from './validation';

/** German display names for canonical system roles (align with backend Roles.Canonical). */
const ROLE_DISPLAY_NAMES: Record<string, string> = {
  SuperAdmin: 'Super-Administrator',
  Manager: 'Manager',
  Cashier: 'Kassierer',
  Waiter: 'Kellner',
  Kitchen: 'Küche',
  ReportViewer: 'Berichte (nur Lesen)',
  Accountant: 'Buchhaltung',
};

export const usersCopy = {
  title: 'Benutzerverwaltung',
  /** List page intro under the title (operator context). */
  pageIntro:
    'Benutzer suchen, nach Rolle und Status filtern und Konten verwalten. Sensible Aktionen werden im Audit protokolliert.',
  filterBandLabel: 'Filter',
  /** Card title — separates primary actions (header) from list filters. */
  filterCardTitle: 'Liste filtern',
  /** Chip row label (aligned with invoice list pattern). */
  activeFiltersLabel: 'Aktive Filter:',
  clearAllFilters: 'Alle Filter zurücksetzen',
  /** Prefix for the scope summary strip (active query context). */
  scopeSummaryLabel: 'Aktive Ansicht:',
  /** Appended to scope line while a background refetch runs (React Query isFetching). */
  listRefreshingHint: 'Aktualisiert …',
  /** Shown under intro — forensics / audit discoverability (German UI). */
  forensicsHintLead:
    'Benutzerbezogene Sicherheitsereignisse finden Sie auch im Audit-Protokoll; für RKSV-/Offline-Stichproben die Audit-Spur.',
  forensicsLinkAuditLog: 'Audit-Protokoll',
  forensicsLinkVerifications: 'RKSV Audit-Spur',
  scopeTotalLoading: 'Gesamtanzahl wird geladen…',
  scopeStatusAll: 'Status: alle',
  scopeSearchPrefix: 'Suche',
  scopeRolePrefix: 'Rolle',
  scopeStatusPrefix: 'Status',
  name: 'Name',
  email: 'E-Mail',
  role: 'Rolle',
  branch: 'Standort',
  status: 'Status',
  lastLogin: 'Letzter Login',
  actions: 'Aktionen',
  create: 'Benutzer anlegen',
  edit: 'Bearbeiten',
  view: 'Ansehen',
  deactivate: 'Deaktivieren',
  reactivate: 'Reaktivieren',
  activity: 'Aktivität',
  filterRole: 'Rolle',
  filterStatus: 'Status',
  filterBranch: 'Standort',
  searchPlaceholder: 'Name, E-Mail, Mitarbeiternummer …',
  statusActive: 'Aktiv',
  statusInactive: 'Inaktiv',
  statusAll: 'Alle',
  createUser: 'Benutzer anlegen',
  createRole: 'Rolle anlegen',
  editUser: 'Benutzer bearbeiten',
  deactivateUser: 'Benutzer deaktivieren',
  reactivateUser: 'Benutzer reaktivieren',
  reasonRequired: 'Grund (für Audit erforderlich)',
  reasonPlaceholder: 'z. B. Ausscheiden, Urlaub, …',
  reasonRequiredMessage: 'Bitte einen Grund angeben.',
  confirmDeactivate:
    'wird deaktiviert. Beleg- und Rechnungsbezüge bleiben erhalten; das Konto kann später reaktiviert werden.',
  confirmReactivate: 'wieder aktivieren?',
  okDeactivate: 'Deaktivieren',
  okReactivate: 'Reaktivieren',
  close: 'Schließen',
  save: 'Speichern',
  cancel: 'Abbrechen',
  userName: 'Benutzername',
  password: 'Passwort',
  firstName: 'Vorname',
  lastName: 'Nachname',
  employeeNumber: 'Mitarbeiternummer',
  taxNumber: 'Steuernummer',
  notes: 'Notizen',
  activityTimeline: 'Aktivitätsverlauf',
  activityFor: 'Aktivitätsverlauf für',
  activityTime: 'Zeit',
  action: 'Aktion',
  actor: 'Durchgeführt von',
  ipAddress: 'IP',
  description: 'Beschreibung',
  viewChanges: 'Änderungen ansehen',
  noFieldChanges: 'Keine Feldänderungen.',
  fieldChangesTitle: 'Geänderte Felder',
  fieldLabel: 'Feld',
  oldValue: 'Vorher',
  newValue: 'Nachher',
  filterActionType: 'Aktionstyp',
  filterDateRange: 'Zeitraum',
  filterAll: 'Alle',
  filterRoleChanges: 'Rollenänderungen',
  filterUserUpdates: 'Benutzeränderungen',
  filterSecurityActions: 'Sicherheitsrelevante Aktionen',
  filterApply: 'Anwenden',
  filterReset: 'Zurücksetzen',
  dateFrom: 'Von',
  dateTo: 'Bis',
  emptyList: 'Keine Benutzer in dieser Ansicht.',
  emptyListWithFilters:
    'Keine Benutzer für die aktuellen Filter. Filter zurücksetzen oder Suche/Rolle/Status anpassen.',
  emptyListDefaultHint: 'Hinweis: Standardmäßig werden nur aktive Konten gelistet (Status = aktiv).',
  emptyActivity: 'Keine Aktivität.',
  errorLoad: 'Benutzerliste konnte nicht geladen werden.',
  errorLoadDetailFallback: 'Keine technische Detailmeldung verfügbar.',
  errorLoadUser: 'Benutzerdaten konnten nicht geladen werden.',
  errorLoadActivity: 'Aktivitätsverlauf konnte nicht geladen werden.',
  errorLoadActivityHint: 'Übrige Benutzerdetails sind weiterhin nutzbar.',
  retry: 'Erneut versuchen',
  actionRefresh: 'Aktualisieren',
  paginationZeroResults: '0 Treffer',
  successCreate: 'Benutzer angelegt.',
  successUpdate: 'Benutzer aktualisiert.',
  successDeactivate: 'Benutzer deaktiviert.',
  successReactivate: 'Benutzer reaktiviert.',
  errorGeneric: 'Fehler.',
  noPermission: 'Nur SuperAdmin (bzw. Benutzer mit entsprechender Berechtigung) können Benutzer verwalten.',
  accessDenied: 'Sie haben keine Berechtigung, Benutzer anzuzeigen.',
  branchNotAvailable: '—',
  details: 'Details',
  resetPassword: 'Passwort zurücksetzen',
  resetPasswordUser: 'Passwort zurücksetzen',
  newPassword: `Neues Passwort (min. ${PASSWORD_MIN_LENGTH} Zeichen, Groß-/Kleinbuchstaben, Zahl, Sonderzeichen)`,
  successResetPassword: 'Passwort wurde zurückgesetzt. Sitzungen des Benutzers wurden ungültig.',
  errorResetPassword: 'Passwort konnte nicht zurückgesetzt werden.',
  errorResetPasswordUserNotFound: 'Benutzer wurde nicht gefunden.',
  errorResetPasswordForbidden: 'Keine Berechtigung, dieses Passwort zurückzusetzen.',
  sessionExpiredOrUnauthorized: 'Sitzung abgelaufen oder nicht angemeldet. Bitte erneut anmelden.',
  roleName: 'Rollenname',
  roleNameRequired: 'Rollenname erforderlich',
  // Role management (Rollen verwalten)
  manageRoles: 'Rollen verwalten',
  manageRolesDescription: 'Rollen und Berechtigungen verwalten. Änderungen an Berechtigungen wirken sich auf die Menü- und Funktionssichtbarkeit der Benutzer aus.',
  newRole: 'Neue Rolle',
  deleteRole: 'Rolle löschen',
  savePermissions: 'Berechtigungen speichern',
  /** Tooltip/alert when a system role is selected; system roles cannot be deleted. */
  systemRoleNoDelete: 'Systemrollen können nicht gelöscht werden.',
  systemRoleProtectedNoDelete: 'Systemrollen sind geschützt und können nicht gelöscht werden.',
  /** System roles (immutable): permissions cannot be changed; delete/rename not available. */
  systemRolePermissionsReadOnly:
    'Diese Systemrolle ist fest im Code verankert; Berechtigungen können hier nicht geändert werden.',
  /** Single info block: system role = read-only permissions, no delete, no rename (no rename UI). */
  systemRoleImmutableInfo:
    'Systemrollen sind fest im Backend definiert: Berechtigungen können hier nicht bearbeitet werden, Löschen und Umbenennen sind nicht möglich. Benutzer können weiterhin dieser Rolle zugewiesen werden.',
  roleHasUsers: 'Rolle kann nicht gelöscht werden: mindestens ein Benutzer ist zugewiesen.',
  /** Shown when delete is blocked because role has assigned users; instructs to reassign first. */
  roleDeleteBlockedReassignFirst: 'Diese Rolle kann nicht gelöscht werden, weil noch Benutzer zugewiesen sind. Weisen Sie diese Benutzer zuerst einer anderen Rolle zu.',
  badgeSystemRole: 'System',
  badgeCustomRole: 'Benutzerdefiniert',
  /** UI access badges (Role Capability Matrix). */
  badgePosUi: 'POS UI',
  badgeAdminUi: 'Admin UI',
  badgePosAndAdmin: 'POS + Admin',
  /** Access / login section in role detail. */
  accessSection: 'Anmeldung',
  posLogin: 'POS-Login',
  adminLogin: 'Admin-Login',
  loginYes: 'Ja',
  loginNo: 'Nein',
  /** Permission groups section. */
  permissionGroupsSection: 'Berechtigungen nach Gruppe',
  /** Compact summary e.g. "3 Gruppen" or group names. */
  permissionGroupCount: (n: number) => (n === 1 ? '1 Gruppe' : `${n} Gruppen`),
  /** Capability hints for role list (short). */
  capabilityHintPosCash: 'POS-Login, Kasse',
  capabilityHintPosOnly: 'POS-Login',
  capabilityHintAdminReports: 'Admin-Login, Berichte',
  capabilityHintAdminFull: 'Admin-Login, Vollzugriff',
  capabilityHintAdminCatalog: 'Admin-Login, Katalog & Berichte',
  capabilityHintBoth: 'POS + Admin',
  /** Summary row labels for role detail. */
  summaryPosLogin: 'POS-Login',
  summaryAdminLogin: 'Admin-Login',
  summaryReports: 'Berichte',
  summaryCashShift: 'Kasse & Schicht',
  summaryCustomer: 'Kunden',
  summaryCatalog: 'Katalog',
  summarySettingsAdmin: 'Einstellungen / Admin',
  summaryNone: '—',
  /** Empty states. */
  noRoleSelectedTitle: 'Rolle wählen',
  noRoleSelectedDescription: 'Wählen Sie links eine Rolle, um Berechtigungen und Anmeldung anzuzeigen.',
  noPermissionsInGroup: 'Keine Berechtigungen in dieser Gruppe',
  /** Role drawer left column section headings. */
  systemRolesSection: 'Systemrollen',
  customRolesSection: 'Benutzerdefinierte Rollen',
  /** Short helper text under each section heading. */
  systemRolesSectionHint:
    'Zuweisbar, hier nicht änderbar (fest im Backend).',
  customRolesSectionHint:
    'Berechtigungen bearbeitbar; löschbar nur ohne zugewiesene Benutzer.',
  /** Display label for known system roles (POS terminology). */
  roleDisplayName: (roleName: string) => ROLE_DISPLAY_NAMES[roleName] ?? roleName,
  /** Shown in user form when role catalog is still loading. */
  rolesLoading: 'Rollen werden geladen…',
  confirmCloseWithDirty: 'Ungespeicherte Änderungen verwerfen?',
  successPermissionsSaved: 'Berechtigungen gespeichert.',
  successRoleDeleted: 'Rolle gelöscht.',
  errorSavePermissions: 'Berechtigungen konnten nicht gespeichert werden.',
  errorDeleteRole: 'Rolle konnte nicht gelöscht werden.',
  noRoleSelected: 'Rolle auswählen',
  permissionsByGroup: 'Berechtigungen nach Gruppe',
  userCount: (n: number) => (n === 1 ? '1 Benutzer' : `${n} Benutzer`),
  presetLabel: 'Preset anwenden',
  presetPlaceholder: 'Vorlage wählen …',
  // Validierung (zentral; Policy = backend Program.cs Identity)
  validationRequired: 'Pflichtfeld.',
  validationEmail: 'Ungültige E-Mail-Adresse.',
  validationPasswordMin: `Min. ${PASSWORD_MIN_LENGTH} Zeichen.`,
  validationPasswordPolicy: 'Mindestens ein Großbuchstabe, ein Kleinbuchstabe, eine Zahl und ein Sonderzeichen.',
  validationMaxLength: (n: number) => `Max. ${n} Zeichen.`,
  // Reset-Passwort: Sicherheitshinweis (Policy = backend Identity)
  resetPasswordSecurityNote: `Alle Sitzungen des Benutzers werden beendet. Das neue Passwort muss mindestens ${PASSWORD_MIN_LENGTH} Zeichen haben sowie Groß- und Kleinbuchstaben, eine Zahl und ein Sonderzeichen.`,
  // Backend/Identity validation errors (DE) – for display in modal when backend returns 400
  resetPasswordErrorMinLength: 'Das Passwort muss mindestens 8 Zeichen haben.',
  resetPasswordErrorDigit: 'Das Passwort muss mindestens eine Ziffer enthalten.',
  resetPasswordErrorLowercase: 'Das Passwort muss mindestens einen Kleinbuchstaben enthalten.',
  resetPasswordErrorUppercase: 'Das Passwort muss mindestens einen Großbuchstaben enthalten.',
  resetPasswordErrorNonAlphanumeric: 'Das Passwort muss mindestens ein Sonderzeichen enthalten.',
  resetPasswordErrorGeneric: 'Das Passwort erfüllt die Anforderungen nicht.',
} as const;

/** Maps permission groupKey (slug from API) to display label for Role Capability Matrix. */
export const GROUP_KEY_LABELS: Record<string, string> = {
  user_role: 'User & Role',
  product: 'Product',
  order_sale: 'Order & Sale',
  payment: 'Payment',
  cash_shift: 'Cash & Shift',
  inventory: 'Inventory',
  customer: 'Customer',
  invoice: 'Invoice',
  settings: 'Settings',
  audit_report: 'Audit & Report',
  finanzonline: 'FinanzOnline',
  kitchen: 'Kitchen',
  tse: 'TSE',
  system: 'System',
  sonstige: 'Sonstige',
  other: 'Sonstige',
};

/**
 * Maps backend/Identity English password error strings to localized copy (currently wired to `usersCopy` DE strings).
 * Follow-up: move targets to `users.passwordErrors.*` i18n keys and pass `t` / active locale instead of DE-only copy.
 */
export function mapBackendPasswordErrorToGerman(backendMessage: string, copy: typeof usersCopy): string {
  const lower = backendMessage.toLowerCase();
  if (lower.includes('at least') && lower.includes('character')) return copy.resetPasswordErrorMinLength;
  if (lower.includes('digit') || lower.includes('number')) return copy.resetPasswordErrorDigit;
  if (lower.includes('lowercase') || lower.includes('lower case')) return copy.resetPasswordErrorLowercase;
  if (lower.includes('uppercase') || lower.includes('upper case')) return copy.resetPasswordErrorUppercase;
  if (lower.includes('non-alphanumeric') || lower.includes('non alphanumeric') || lower.includes('special')) return copy.resetPasswordErrorNonAlphanumeric;
  return copy.resetPasswordErrorGeneric;
}
