'use client';

import { InfoCircleOutlined } from '@ant-design/icons';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert,
  Button,
  Card,
  Col,
  DatePicker,
  Empty,
  Input,
  Modal,
  Row,
  Space,
  Table,
  Tag,
  Tooltip,
  Typography,
} from 'antd';
import type { ColumnsType } from 'antd/es/table';
import dayjs from 'dayjs';
import Link from 'next/link';
import { usePathname, useRouter, useSearchParams } from 'next/navigation';
/**
 * Bu ana bileşen RKSV Sonderbelege işlemlerini daha anlaşılır kart düzeninde sunar.
 */
import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';

import type { ReceiptListItemDto } from '@/api/generated/model';
import { getApiReceiptsList } from '@/api/generated/receipts/receipts';
import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { dateColumnRender } from '@/components/DateColumn';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useAdminCashRegisterList } from '@/features/cash-registers/hooks/useAdminCashRegisterList';
import { rawRegisterStatus as rawCashRegisterStatus } from '@/features/cash-registers/utils/registerStatus';
import { ReprintButton } from '@/features/payments/components/ReprintButton';
import {
  isRksvFinanzOnlineTrackedSpecialReceiptKind,
  rksvFinanzOnlineSubmissionStatusLabelDe,
  rksvFinanzOnlineSubmissionStatusTagColor,
} from '@/features/receipts/utils/rksvFinanzOnlineSubmissionUi';
import { reportPdfTypeFromSpecialReceiptKind } from '@/features/reports/api/reportPdfApi';
import { StoredReportPdfButton } from '@/features/reports/components/StoredReportPdfButton';
import { SonderbelegeRegisterPicker } from '@/features/rksv-operations/components/SonderbelegeRegisterPicker';
import { rksvSpecialReceiptKindLabelDe } from '@/features/rksv-operations/rksvSpecialReceiptDisplay';
import {
  formatSonderbelegeRegisterLabel,
  isSonderbelegeRegisterTenantMismatch,
  sonderbelegeStatusVisual,
} from '@/features/rksv-operations/utils/sonderbelegeRegisterDisplay';
import { CreateMonatsbelegModal } from '@/features/rksv/components/CreateMonatsbelegModal';
import { LateMonatsbelegCreationCard } from '@/features/rksv/components/LateMonatsbelegCreationCard';
import { MonatsbelegInfoCard } from '@/features/rksv/components/MonatsbelegInfoCard';
import { MonatsbelegTimeline } from '@/features/rksv/components/MonatsbelegTimeline';
import type { MonthCardStatus } from '@/features/rksv/components/MonthCard';
import { StartbelegStatus } from '@/features/rksv/components/StartbelegStatus';
import {
  monatsbelegQueryKeys,
  useCashRegisterMonatsbeleg,
} from '@/features/rksv/hooks/useMonatsbeleg';
import type { ReceiptLateCreationFields } from '@/features/rksv/types/receiptLateCreation';
import { receiptIsLateCreated } from '@/features/rksv/types/receiptLateCreation';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { useTenantList } from '@/features/tenancy/hooks/useTenantList';
import { switchDevTenantContext } from '@/features/tenancy/services/setTenantAndRefresh';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useCanAccessPath } from '@/hooks/useCanAccessPath';
import { useNotify } from '@/hooks/useNotify';
import { useTenant } from '@/hooks/useTenant';
import { useI18n } from '@/i18n';
import { formatDateTime } from '@/i18n/formatting';
import { customInstance } from '@/lib/axios';
import { ADMIN_NAV_GROUP_LABELS, ADMIN_OVERVIEW_CRUMB } from '@/shared/adminShellLabels';
import { PERMISSIONS } from '@/shared/auth/permissions';
import { usePermissions } from '@/shared/auth/usePermissions';
import { formatEUR } from '@/shared/utils/currency';
import {
  getMonthDifference,
  getViennaCalendarYear,
  getViennaCalendarYearMonth,
} from '@/shared/utils/viennaCalendar';

type OrvalReceiptRow = ReceiptListItemDto & ReceiptLateCreationFields;

type MissingMonatsbelegTableRow = {
  key: string;
  year: number;
  month: number;
  isMissing: boolean;
  isOverdue: boolean;
  monthDiff: number;
};

function monatsbelegDelaySeverity(monthDiff: number): { label: string; color: string } {
  if (monthDiff <= 0) return { label: 'Aktuell', color: 'green' };
  if (monthDiff <= 1) return { label: 'Info', color: 'blue' };
  if (monthDiff <= 6) return { label: 'Warnung', color: 'orange' };
  return { label: 'Achtung', color: 'red' };
}

function formatMonthYearDe(year: number, month: number): string {
  return new Intl.DateTimeFormat('de-DE', {
    month: 'long',
    year: 'numeric',
    timeZone: 'Europe/Vienna',
  }).format(new Date(Date.UTC(year, month - 1, 1)));
}

function normalizeSpecialKind(kind: string | null | undefined): string {
  return String(kind ?? '')
    .trim()
    .toLowerCase();
}

function isKind(row: OrvalReceiptRow, kind: string): boolean {
  return normalizeSpecialKind(row.rksvSpecialReceiptKind) === kind;
}

function specialReceiptPurposeDe(kind: string): string {
  switch (kind) {
    case 'startbeleg':
      return 'Erster RKSV-Beleg zur Aktivierung einer Kasse.';
    case 'monatsbeleg':
      return 'Monatlicher Kontrollbeleg zur RKSV-Nachweisführung.';
    case 'jahresbeleg':
      return 'Jährlicher Kontrollbeleg für den Jahresabschluss.';
    case 'nullbeleg':
      return 'Nullumsatz-Beleg für Kontrolle, Test oder Sonderfälle.';
    case 'schlussbeleg':
      return 'Endgültige Stilllegung der Kasse (kein weiterer Verkauf).';
    default:
      return 'RKSV-Sonderbeleg.';
  }
}

function specialReceiptBadge(kind: string): { text: string; color: string } {
  switch (kind) {
    case 'startbeleg':
      return { text: 'Start', color: 'blue' };
    case 'monatsbeleg':
      return { text: 'Monats', color: 'green' };
    case 'jahresbeleg':
      return { text: 'Jahres', color: 'gold' };
    case 'nullbeleg':
      return { text: 'Null', color: 'purple' };
    case 'schlussbeleg':
      return { text: 'Schluss', color: 'red' };
    default:
      return { text: 'Sonderbeleg', color: 'default' };
  }
}

function titleWithTooltip(title: string, tooltipText: string): React.ReactNode {
  return (
    <Space size={6}>
      <span>{title}</span>
      <Tooltip title={tooltipText}>
        <InfoCircleOutlined style={{ color: '#8c8c8c' }} />
      </Tooltip>
    </Space>
  );
}

export default function RksvSonderbelegePage() {
  const { modal } = useAntdApp();
  const notify = useNotify();
  const { t } = useI18n();

  const { hasPermission, isSuperAdmin } = usePermissions();
  const { refreshToken } = useAuth();
  const { setTenant } = useTenant();
  const currentTenant = useCurrentTenant();
  const ambientTenantId = currentTenant.tenantId?.trim() || undefined;
  const canCreateRegister = useCanAccessPath('/kassenverwaltung');
  const searchParams = useSearchParams();
  const pathname = usePathname();
  const router = useRouter();
  const queryClient = useQueryClient();

  const canNull = hasPermission(PERMISSIONS.RKSV_NULLBELEG_CREATE);
  const canStart = hasPermission(PERMISSIONS.RKSV_STARTBELEG_CREATE);
  const canMonat = hasPermission(PERMISSIONS.RKSV_MONATSBELEG_CREATE);
  const canJahr = hasPermission(PERMISSIONS.RKSV_JAHRESBELEG_CREATE);
  const canSchluss = hasPermission(PERMISSIONS.RKSV_SCHLUSSBELEG_CREATE);
  // Demo tools: SuperAdmin only (`system.critical`) + catalog permission + development.
  // Manager must never see this card even if a custom role somehow grants rksv.test-helper.
  const canTestHelper =
    hasPermission(PERMISSIONS.SYSTEM_CRITICAL) && hasPermission(PERMISSIONS.RKSV_TEST_HELPER);
  const canTseSimulation = hasPermission(PERMISSIONS.RKSV_TSE_SIMULATION);
  const isDevelopment = process.env.NODE_ENV === 'development';

  const { tenants, isLoading: tenantsLoading } = useTenantList({
    enabled: isSuperAdmin,
  });

  // Ambient JWT/header tenant only — Super Admin must rebind before creating Sonderbelege.
  const { registers, isLoading: registersLoading } = useAdminCashRegisterList({
    allowAllTenants: false,
    allowTenantScopedDefault: true,
    excludeDecommissioned: false,
    enabled: Boolean(ambientTenantId),
  });

  const { year: viennaYear, month: viennaMonth } = useMemo(() => getViennaCalendarYearMonth(), []);
  const defaultYear = useMemo(() => getViennaCalendarYear(), []);
  const defaultPastMonatsbelegPeriod = useMemo(() => {
    const month = viennaMonth === 1 ? 12 : viennaMonth - 1;
    const year = viennaMonth === 1 ? viennaYear - 1 : viennaYear;
    return { year, month };
  }, [viennaYear, viennaMonth]);
  /** Latest selectable Monatsbeleg month: previous Vienna calendar month (current month excluded). */
  const maxMonatsbelegMonth = useMemo(
    () =>
      dayjs(
        `${defaultPastMonatsbelegPeriod.year}-${String(defaultPastMonatsbelegPeriod.month).padStart(2, '0')}-01`
      ),
    [defaultPastMonatsbelegPeriod]
  );

  const [registerId, setRegisterId] = useState<string | undefined>(undefined);
  const [monatPeriod, setMonatPeriod] = useState(() =>
    dayjs(
      `${defaultPastMonatsbelegPeriod.year}-${String(defaultPastMonatsbelegPeriod.month).padStart(2, '0')}-01`
    )
  );
  const [jahrPeriod, setJahrPeriod] = useState(() => dayjs(`${defaultYear}-01-01`));
  const [nullPeriod, setNullPeriod] = useState(() =>
    dayjs(`${viennaYear}-${String(viennaMonth).padStart(2, '0')}-01`)
  );
  const [jbEarly, setJbEarly] = useState('');
  const [reasonShort, setReasonShort] = useState('');
  const [busy, setBusy] = useState<string | null>(null);

  const [schlussModalOpen, setSchlussModalOpen] = useState(false);
  const [schlussConfirmText, setSchlussConfirmText] = useState('');
  const [monatsbelegModalOpen, setMonatsbelegModalOpen] = useState(false);
  const [selectedMonatsbelegYear, setSelectedMonatsbelegYear] = useState(
    defaultPastMonatsbelegPeriod.year
  );
  const [selectedMonatsbelegMonth, setSelectedMonatsbelegMonth] = useState(
    defaultPastMonatsbelegPeriod.month
  );

  const didAutoSelectRef = useRef(false);

  useEffect(() => {
    const q = searchParams.get('registerId')?.trim();
    if (q) setRegisterId(q);

    const yearRaw = searchParams.get('year')?.trim();
    const monthRaw = searchParams.get('month')?.trim();
    const year = yearRaw ? Number(yearRaw) : NaN;
    const month = monthRaw ? Number(monthRaw) : NaN;
    if (
      Number.isInteger(year) &&
      year >= 2020 &&
      year <= 2100 &&
      Number.isInteger(month) &&
      month >= 1 &&
      month <= 12
    ) {
      setMonatPeriod(dayjs(`${year}-${String(month).padStart(2, '0')}-01`));
    }
  }, [searchParams]);

  const syncSonderbelegeQuery = useCallback(
    (next: { registerId?: string | null; tenantId?: string | null }) => {
      const params = new URLSearchParams(searchParams.toString());
      if (next.registerId !== undefined) {
        const rid = next.registerId?.trim();
        if (rid) params.set('registerId', rid);
        else params.delete('registerId');
      }
      if (next.tenantId !== undefined) {
        const tid = next.tenantId?.trim();
        if (tid) params.set('tenantId', tid);
        else params.delete('tenantId');
      }
      const qs = params.toString();
      router.replace(qs ? `${pathname}?${qs}` : pathname, { scroll: false });
    },
    [pathname, router, searchParams]
  );

  const confirmAndSwitchTenant = useCallback(
    (
      target: { id: string; slug: string; name: string; licenseValidUntilUtc?: string | null },
      keepRegisterId?: string
    ) => {
      modal.confirm({
        title: t('rksvHub.sonderbelege.switchTenantConfirmTitle'),
        content: t('rksvHub.sonderbelege.switchTenantConfirm', {
          tenantName: target.name || target.slug,
        }),
        okText: t('common.buttons.confirm'),
        cancelText: t('common.buttons.cancel'),
        onOk: async () => {
          const licenseValidUntilUtc = target.licenseValidUntilUtc ?? null;
          const licenseValid = Boolean(
            licenseValidUntilUtc && new Date(licenseValidUntilUtc).getTime() > Date.now()
          );
          setTenant({
            id: target.id,
            slug: target.slug,
            name: target.name,
            licenseValid,
            licenseValidUntilUtc,
          });
          const tokenOk = await refreshToken(target.id);
          if (!tokenOk) {
            notify.errorKey('adminShell.tenant.devSwitcher.refreshFailed');
            return;
          }
          const params = new URLSearchParams(searchParams.toString());
          params.set('tenantId', target.id);
          if (keepRegisterId) params.set('registerId', keepRegisterId);
          else params.delete('registerId');
          const qs = params.toString();
          window.history.replaceState(null, '', qs ? `${pathname}?${qs}` : pathname);
          await switchDevTenantContext({ slug: target.slug, id: target.id });
        },
      });
    },
    [modal, notify, pathname, refreshToken, searchParams, setTenant, t]
  );

  const handleRegisterChange = useCallback(
    (nextId: string | undefined) => {
      setRegisterId(nextId);
      syncSonderbelegeQuery({ registerId: nextId ?? null });
    },
    [syncSonderbelegeQuery]
  );

  const handleTenantSwitchRequest = useCallback(
    (nextTenantId: string) => {
      if (!nextTenantId || nextTenantId.toLowerCase() === (ambientTenantId ?? '').toLowerCase()) {
        return;
      }
      const row = tenants.find((tenant) => tenant.id === nextTenantId);
      if (!row) {
        return;
      }
      confirmAndSwitchTenant({
        id: row.id,
        slug: row.slug,
        name: row.name,
        licenseValidUntilUtc: row.licenseValidUntilUtc,
      });
    },
    [ambientTenantId, confirmAndSwitchTenant, tenants]
  );

  const handleRegisterTenantMismatch = useCallback(
    (register: (typeof registers)[number]) => {
      if (!isSuperAdmin) {
        return;
      }
      const row = tenants.find((tenant) => tenant.id === String(register.tenantId));
      const slug = row?.slug || register.tenantSlug?.trim();
      if (!slug) {
        return;
      }
      confirmAndSwitchTenant(
        {
          id: String(register.tenantId),
          slug,
          name: row?.name || register.tenantName?.trim() || slug,
          licenseValidUntilUtc: row?.licenseValidUntilUtc,
        },
        String(register.id)
      );
    },
    [confirmAndSwitchTenant, isSuperAdmin, tenants]
  );

  // Auto-select once: if exactly one register is available (and none was preselected
  // via query param or user action), pick it so Manager sees the register immediately.
  useEffect(() => {
    if (didAutoSelectRef.current || registersLoading) return;
    if (registerId) {
      didAutoSelectRef.current = true;
      return;
    }
    if (searchParams.get('registerId')?.trim()) return;
    if (registers.length === 1 && registers[0]?.id) {
      const id = String(registers[0].id);
      setRegisterId(id);
      syncSonderbelegeQuery({ registerId: id });
      didAutoSelectRef.current = true;
    }
  }, [registers, registersLoading, registerId, searchParams, syncSonderbelegeQuery]);

  useEffect(() => {
    const focus = searchParams.get('focus')?.trim();
    if (
      focus !== 'startbeleg' &&
      focus !== 'schlussbeleg' &&
      focus !== 'monatsbeleg' &&
      focus !== 'test-helper'
    ) {
      return;
    }
    const id =
      focus === 'startbeleg'
        ? 'rksv-focus-startbeleg'
        : focus === 'schlussbeleg'
          ? 'rksv-focus-schlussbeleg'
          : focus === 'test-helper'
            ? 'rksv-focus-test-helper'
            : searchParams.get('year') || searchParams.get('month')
              ? 'rksv-monatsbeleg-timeline'
              : 'rksv-missing-monatsbelege';
    requestAnimationFrame(() => {
      document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    });
  }, [searchParams]);

  const monatsbelegStatusQuery = useCashRegisterMonatsbeleg(registerId ?? '', {
    enabled: Boolean(registerId?.trim()),
  });

  const SONDERBELEG_RECEIPT_SCAN_PAGE_SIZE = 100;

  const { data: receiptScan, isLoading: scanLoading } = useQuery({
    queryKey: ['rksv-sonderbelege-recent-special', SONDERBELEG_RECEIPT_SCAN_PAGE_SIZE],
    queryFn: async () => {
      const res = await getApiReceiptsList({
        page: 1,
        pageSize: SONDERBELEG_RECEIPT_SCAN_PAGE_SIZE,
        sort: 'issuedAt:desc',
      });
      const items = (res.items ?? []) as OrvalReceiptRow[];
      return items.filter((x) => Boolean(x.rksvSpecialReceiptKind?.trim()));
    },
  });

  const selectedRegister = useMemo(
    () => registers.find((r) => String(r.id ?? '') === String(registerId ?? '')),
    [registers, registerId]
  );
  const isTenantMismatch = isSonderbelegeRegisterTenantMismatch(selectedRegister, ambientTenantId);
  const orphanRegisterSelection = Boolean(
    !registersLoading && registerId?.trim() && !selectedRegister
  );
  const tenantBlocksCreate = isTenantMismatch || orphanRegisterSelection || !ambientTenantId;
  const selectedRegisterStatus = selectedRegister
    ? rawCashRegisterStatus(selectedRegister)
    : undefined;
  const selectedRegisterIsDecommissioned = selectedRegisterStatus === 5;
  const selectedRegisterHasOpenSession = selectedRegisterStatus === 2;
  const canCreateSchlussbelegNow = selectedRegisterStatus === 1;
  const selectedStatusVisual = sonderbelegeStatusVisual(selectedRegisterStatus);
  const selectedStatusLabel =
    selectedStatusVisual.key === 'unknown'
      ? selectedRegisterStatus != null
        ? t('rksvHub.sonderbelege.status.unknown', { status: selectedRegisterStatus })
        : '—'
      : t(`rksvHub.sonderbelege.status.${selectedStatusVisual.key}`);

  const registerScopedReceipts = useMemo(
    () =>
      (receiptScan ?? []).filter(
        (row) => String(row.cashRegisterId ?? '') === String(registerId ?? '')
      ),
    [receiptScan, registerId]
  );

  const monatYear = monatPeriod.year();
  const monatMonth = monatPeriod.month() + 1;
  const monatMonthDiff = getMonthDifference(monatYear, monatMonth);
  /** Current unfinished Vienna month and future months are not allowed for Monatsbeleg. */
  const monatIsCurrentOrFutureMonth = monatMonthDiff <= 0;
  const jahrYear = jahrPeriod.year();

  const hasStartbelegForRegister = useMemo(() => {
    if (selectedRegister?.startbelegCreatedAtUtc) return true;
    return registerScopedReceipts.some((row) => isKind(row, 'startbeleg'));
  }, [registerScopedReceipts, selectedRegister?.startbelegCreatedAtUtc]);

  const startbelegCreatedAtUtc = useMemo(() => {
    if (selectedRegister?.startbelegCreatedAtUtc) return selectedRegister.startbelegCreatedAtUtc;
    const row = registerScopedReceipts.find((r) => isKind(r, 'startbeleg'));
    return row?.issuedAt ?? row?.createdAt ?? null;
  }, [registerScopedReceipts, selectedRegister?.startbelegCreatedAtUtc]);

  const hasNullbelegForRegister = useMemo(
    () => registerScopedReceipts.some((row) => isKind(row, 'nullbeleg')),
    [registerScopedReceipts]
  );

  const hasMonatsbelegForPeriod = useMemo(
    () =>
      registerScopedReceipts.some(
        (row) =>
          isKind(row, 'monatsbeleg') &&
          Number(row.rksvSpecialReceiptYear ?? 0) === monatYear &&
          Number(row.rksvSpecialReceiptMonth ?? 0) === monatMonth
      ),
    [registerScopedReceipts, monatYear, monatMonth]
  );

  const hasJahresbelegForYear = useMemo(
    () =>
      registerScopedReceipts.some(
        (row) => isKind(row, 'jahresbeleg') && Number(row.rksvSpecialReceiptYear ?? 0) === jahrYear
      ),
    [registerScopedReceipts, jahrYear]
  );

  const hasSchlussbelegForRegister = useMemo(
    () => registerScopedReceipts.some((row) => isKind(row, 'schlussbeleg')),
    [registerScopedReceipts]
  );

  const recentSpecialReceipts = useMemo(
    () => (registerId ? registerScopedReceipts : (receiptScan ?? [])).slice(0, 10),
    [registerId, registerScopedReceipts, receiptScan]
  );

  const monthlyTimelineRows = useMemo(
    () =>
      Array.from({ length: 12 }, (_, idx) => {
        const month = idx + 1;
        const monatsbelegRow = registerScopedReceipts.find(
          (row) =>
            isKind(row, 'monatsbeleg') &&
            Number(row.rksvSpecialReceiptYear ?? 0) === monatYear &&
            Number(row.rksvSpecialReceiptMonth ?? 0) === month
        );
        // Only completed (past) Vienna months are required; current month stays pending.
        const isPastMonth =
          monatYear < viennaYear || (monatYear === viennaYear && month < viennaMonth);
        const status: MonthCardStatus = monatsbelegRow
          ? 'completed'
          : isPastMonth
            ? 'missing'
            : 'pending';
        return {
          month,
          status,
          receiptId: monatsbelegRow?.receiptId?.trim() || undefined,
        };
      }),
    [registerScopedReceipts, monatYear, viennaYear, viennaMonth]
  );

  const invalidateLists = useCallback(async () => {
    await queryClient.invalidateQueries({ queryKey: ['rksv-sonderbelege-recent-special'] });
    await queryClient.invalidateQueries({ queryKey: ['/api/Receipts/list'] });
  }, [queryClient]);

  const refetchMonatsbelegData = useCallback(async () => {
    await invalidateLists();
    await queryClient.invalidateQueries({ queryKey: monatsbelegQueryKeys.statusOverview });
    if (registerId?.trim()) {
      await queryClient.invalidateQueries({
        queryKey: monatsbelegQueryKeys.registerStatus(registerId.trim()),
      });
      await monatsbelegStatusQuery.refetch();
    }
  }, [invalidateLists, monatsbelegStatusQuery, queryClient, registerId]);

  const missingMonatsbelegRows = useMemo((): MissingMonatsbelegTableRow[] => {
    const apiMissing = monatsbelegStatusQuery.data?.missingMonths ?? [];
    if (apiMissing.length > 0) {
      return apiMissing
        .filter((entry) => getMonthDifference(entry.year, entry.month) > 0)
        .map((entry) => ({
          key: `${entry.year}-${String(entry.month).padStart(2, '0')}`,
          year: entry.year,
          month: entry.month,
          isMissing: true,
          isOverdue: entry.isOverdue,
          monthDiff: getMonthDifference(entry.year, entry.month),
        }))
        .sort((a, b) => {
          const anchorA = a.year * 12 + (a.month - 1);
          const anchorB = b.year * 12 + (b.month - 1);
          return anchorA - anchorB;
        });
    }

    return monthlyTimelineRows
      .filter((row) => row.status === 'missing')
      .map((row) => ({
        key: `${monatYear}-${String(row.month).padStart(2, '0')}`,
        year: monatYear,
        month: row.month,
        isMissing: true,
        isOverdue: getMonthDifference(monatYear, row.month) > 0,
        monthDiff: getMonthDifference(monatYear, row.month),
      }));
  }, [monatsbelegStatusQuery.data?.missingMonths, monatYear, monthlyTimelineRows]);

  const openMissingMonatsbelegModal = useCallback(
    (year: number, month: number) => {
      if (!registerId) {
        notify.warning('rksvHub.sonderbelege.selectRegister');
        return;
      }
      if (tenantBlocksCreate) {
        notify.errorKey('rksvHub.sonderbelege.tenantMismatchTitle');
        return;
      }
      if (!canMonat) {
        notify.warning('rksvHub.sonderbelege.permissionDenied');
        return;
      }
      if (getMonthDifference(year, month) <= 0) {
        notify.errorKey('rksvHub.sonderbelege.pastMonthOnly');
        return;
      }
      setSelectedMonatsbelegYear(year);
      setSelectedMonatsbelegMonth(month);
      setMonatsbelegModalOpen(true);
    },
    [registerId, tenantBlocksCreate, canMonat, notify]
  );

  const postJson = useCallback(async (path: string, body: object) => {
    return customInstance<{ paymentId?: string; receiptNumber?: string; message?: string }>({
      url: path,
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      data: body,
    });
  }, []);

  const ensureRegisterForCreate = useCallback((): string | undefined => {
    if (!registerId) {
      notify.warning('rksvHub.sonderbelege.selectRegister');
      return undefined;
    }
    if (tenantBlocksCreate) {
      notify.errorKey('rksvHub.sonderbelege.tenantMismatchTitle');
      return undefined;
    }
    return registerId;
  }, [notify, registerId, tenantBlocksCreate]);

  const onNullbeleg = useCallback(async () => {
    const cashRegisterId = ensureRegisterForCreate();
    if (!cashRegisterId) return;
    setBusy('null');
    try {
      await postJson('/api/rksv/special-receipts/nullbeleg', {
        cashRegisterId,
        year: viennaYear,
        month: viennaMonth,
        reason: reasonShort.trim() || 'Nullbeleg für Prüfzwecke',
        actsAsJahresbeleg: null,
      });
      notify.successKey('rksvHub.sonderbelege.nullbelegSuccess');
      await invalidateLists();
    } catch (e: unknown) {
      notify.apiError(e, { logContext: 'RKSV.nullbeleg', fallbackKey: 'common.errorGeneric' });
    } finally {
      setBusy(null);
    }
  }, [ensureRegisterForCreate, registerId, viennaYear, viennaMonth, reasonShort, postJson, invalidateLists, notify]);

  const onStartbeleg = useCallback(async () => {
    const cashRegisterId = ensureRegisterForCreate();
    if (!cashRegisterId) return;
    setBusy('start');
    try {
      await postJson('/api/rksv/special-receipts/startbeleg', {
        cashRegisterId,
        reason: reasonShort.trim() || 'Admin Startbeleg',
      });
      notify.successKey('rksvHub.sonderbelege.startbelegSuccess');
      await invalidateLists();
    } catch (e: unknown) {
      notify.apiError(e, { logContext: 'RKSV.startbeleg', fallbackKey: 'common.errorGeneric' });
    } finally {
      setBusy(null);
    }
  }, [ensureRegisterForCreate, registerId, reasonShort, postJson, invalidateLists, notify]);

  const openMonatsbelegModal = useCallback(() => {
    openMissingMonatsbelegModal(monatYear, monatMonth);
  }, [monatYear, monatMonth, openMissingMonatsbelegModal]);

  const onJahresbeleg = useCallback(async () => {
    const cashRegisterId = ensureRegisterForCreate();
    if (!cashRegisterId) return;
    setBusy('jahr');
    try {
      await postJson('/api/rksv/special-receipts/jahresbeleg', {
        cashRegisterId,
        year: jahrYear,
        reason: 'Admin Jahresbeleg',
        earlyReason: jbEarly.trim() || null,
      });
      notify.successKey('rksvHub.sonderbelege.jahresbelegSuccess');
      await invalidateLists();
    } catch (e: unknown) {
      notify.apiError(e, { logContext: 'RKSV.jahresbeleg', fallbackKey: 'common.errorGeneric' });
    } finally {
      setBusy(null);
    }
  }, [ensureRegisterForCreate, registerId, jahrYear, jbEarly, postJson, invalidateLists, notify]);

  const onSchlussbeleg = useCallback(async () => {
    const cashRegisterId = ensureRegisterForCreate();
    if (!cashRegisterId) return;
    if (!canCreateSchlussbelegNow) {
      notify.errorKey('rksvHub.sonderbelege.endbelegRequiresClosedSession');
      return;
    }
    setBusy('schluss');
    try {
      await postJson('/api/rksv/special-receipts/schlussbeleg', {
        cashRegisterId,
        reason: reasonShort.trim() || 'Admin Schlussbeleg',
      });
      notify.successKey('rksvHub.sonderbelege.schlussbelegSuccess');
      await invalidateLists();
    } catch (e: unknown) {
      notify.apiError(e, { logContext: 'RKSV.schlussbeleg', fallbackKey: 'common.errorGeneric' });
    } finally {
      setBusy(null);
    }
  }, [ensureRegisterForCreate, registerId, canCreateSchlussbelegNow, reasonShort, postJson, invalidateLists, notify]);

  const confirmJahresbeleg = useCallback(() => {
    modal.confirm({
      title: 'Jahresbeleg erstellen',
      content: 'Dieser Vorgang kann nicht rückgängig gemacht werden.',
      okText: 'Erstellen',
      cancelText: 'Abbrechen',
      onOk: () => onJahresbeleg(),
    });
  }, [modal, onJahresbeleg]);

  const submitSchlussModal = useCallback(async () => {
    if (!canCreateSchlussbelegNow) {
      notify.errorKey('rksvHub.sonderbelege.endbelegClosedNoSession');
      return;
    }
    if (schlussConfirmText.trim().toUpperCase() !== 'ENDBELEG') {
      notify.errorKey('rksvHub.sonderbelege.endbelegConfirmExact');
      return;
    }
    await onSchlussbeleg();
    setSchlussModalOpen(false);
    setSchlussConfirmText('');
  }, [canCreateSchlussbelegNow, schlussConfirmText, onSchlussbeleg, notify]);

  const openSchlussbelegDialog = useCallback(() => {
    if (!ensureRegisterForCreate()) return;
    if (!canCreateSchlussbelegNow) {
      notify.errorKey('rksvHub.sonderbelege.endbelegRequiresClosedRegister');
      return;
    }
    setSchlussConfirmText('');
    setSchlussModalOpen(true);
  }, [ensureRegisterForCreate, registerId, canCreateSchlussbelegNow, notify]);

  const onBulkCreateMissingMonatsbelege = useCallback(async () => {
    const cashRegisterId = ensureRegisterForCreate();
    if (!cashRegisterId) return;

    const prevMonth = viennaMonth === 1 ? 12 : viennaMonth - 1;
    const prevYear = viennaMonth === 1 ? viennaYear - 1 : viennaYear;

    const hasPrevMonthMonatsbeleg = registerScopedReceipts.some(
      (row) =>
        isKind(row, 'monatsbeleg') &&
        Number(row.rksvSpecialReceiptYear ?? 0) === prevYear &&
        Number(row.rksvSpecialReceiptMonth ?? 0) === prevMonth
    );

    if (hasPrevMonthMonatsbeleg) {
      notify.info('rksvHub.sonderbelege.prevMonthMonatsbelegExists');
      return;
    }

    setBusy('demo-bulk');
    try {
      await postJson('/api/rksv/special-receipts/monatsbeleg?force=true', {
        cashRegisterId,
        year: prevYear,
        month: prevMonth,
        reason: 'Demo Helper: Monatsbeleg Vormonat',
      });
      await invalidateLists();
      notify.successKey('rksvHub.sonderbelege.monatsbelegCreatedForPeriod', {
        period: formatMonthYearDe(prevYear, prevMonth),
      });
    } catch (e: unknown) {
      notify.apiError(e, {
        logContext: 'RKSV.demoMonatsbeleg',
        fallbackKey: 'common.errorGeneric',
      });
    } finally {
      setBusy(null);
    }
  }, [
    ensureRegisterForCreate,
    registerId,
    registerScopedReceipts,
    viennaYear,
    viennaMonth,
    postJson,
    invalidateLists,
    notify,
  ]);

  const onCreateDemoNullbelegForCurrentMonth = useCallback(async () => {
    const cashRegisterId = ensureRegisterForCreate();
    if (!cashRegisterId) return;

    setBusy('demo-null');
    try {
      await postJson('/api/rksv/special-receipts/nullbeleg', {
        cashRegisterId,
        year: viennaYear,
        month: viennaMonth,
        reason: 'Demo Helper: Test-Nullbeleg',
        actsAsJahresbeleg: viennaMonth === 12 ? true : null,
      });
      notify.successKey('rksvHub.sonderbelege.demoNullbelegSuccess');
      await invalidateLists();
    } catch (e: unknown) {
      notify.apiError(e, {
        logContext: 'RKSV.demoNullbeleg',
        fallbackKey: 'common.errorGeneric',
      });
    } finally {
      setBusy(null);
    }
  }, [ensureRegisterForCreate, registerId, viennaYear, viennaMonth, postJson, invalidateLists, notify]);

  const onResetTseSimulation = useCallback(async () => {
    setBusy('demo-tse-reset');
    try {
      setMonatPeriod(dayjs(`${viennaYear}-${String(viennaMonth).padStart(2, '0')}-01`));
      setJahrPeriod(dayjs(`${defaultYear}-01-01`));
      setJbEarly('');
      setReasonShort('');
      await queryClient.invalidateQueries({ queryKey: ['/api/tse/health'] });
      notify.successKey('rksvHub.sonderbelege.demoTseResetSuccess');
    } finally {
      setBusy(null);
    }
  }, [viennaYear, viennaMonth, defaultYear, queryClient, notify]);

  const specialColumns: ColumnsType<OrvalReceiptRow> = useMemo(
    () => [
      {
        title: 'Belegnummer',
        dataIndex: 'receiptNumber',
        key: 'receiptNumber',
        render: (t: string, row) => <Link href={`/receipts/${row.receiptId}`}>{t || '—'}</Link>,
      },
      {
        title: 'Typ',
        dataIndex: 'rksvSpecialReceiptKind',
        key: 'kind',
        render: (k: string | null | undefined) => (
          <Typography.Text>{rksvSpecialReceiptKindLabelDe(k)}</Typography.Text>
        ),
      },
      {
        title: 'Periode',
        key: 'period',
        render: (_: unknown, row) => {
          const y = Number(row.rksvSpecialReceiptYear ?? 0);
          const m = Number(row.rksvSpecialReceiptMonth ?? 0);
          if (y > 0 && m > 0)
            return `${formatMonthYearDe(y, m)} (${y}-${String(m).padStart(2, '0')})`;
          if (y > 0) return String(y);
          return '—';
        },
      },
      {
        title: 'Status',
        key: 'status',
        render: (_: unknown, row) => {
          const kind = normalizeSpecialKind(row.rksvSpecialReceiptKind);
          if (kind === 'schlussbeleg') return <Tag color="red">Stillgelegt</Tag>;
          if (kind === 'startbeleg') return <Tag color="blue">Initial erstellt</Tag>;
          if (receiptIsLateCreated(row)) return <Tag color="orange">Verspätet erstellt</Tag>;
          return <Tag color="green">Erstellt</Tag>;
        },
      },
      {
        title: 'Datum',
        dataIndex: 'issuedAt',
        key: 'issuedAt',
        render: dateColumnRender('datetime'),
      },
      {
        title: 'FinanzOnline',
        key: 'fon',
        render: (_: unknown, row) => {
          if (!isRksvFinanzOnlineTrackedSpecialReceiptKind(row.rksvSpecialReceiptKind)) {
            return <Typography.Text type="secondary">—</Typography.Text>;
          }
          const st = row.rksvFinanzOnlineSubmissionStatus;
          if (!st?.trim()) return <Typography.Text type="secondary">—</Typography.Text>;
          return (
            <Tag color={rksvFinanzOnlineSubmissionStatusTagColor(st)}>
              {rksvFinanzOnlineSubmissionStatusLabelDe(st)}
            </Tag>
          );
        },
      },
      {
        title: 'Betrag',
        dataIndex: 'grandTotal',
        key: 'grandTotal',
        align: 'right',
        render: (v: number | undefined) => formatEUR(v ?? 0),
      },
      {
        title: 'PDF',
        key: 'pdf',
        width: 90,
        align: 'center',
        render: (_: unknown, row) =>
          row.paymentId ? (
            <StoredReportPdfButton
              reportType={reportPdfTypeFromSpecialReceiptKind(row.rksvSpecialReceiptKind)}
              targetId={row.paymentId}
              fileNameBase={row.receiptNumber}
              size="small"
            />
          ) : (
            '—'
          ),
      },
      {
        title: 'Aktionen',
        key: 'actions',
        render: (_: unknown, row) => (
          <Space wrap>
            <Link href={`/receipts/${row.receiptId}`}>
              <Button size="small">Anzeigen</Button>
            </Link>
            {row.paymentId ? (
              <ReprintButton
                paymentId={row.paymentId}
                receiptNumber={row.receiptNumber}
                size="small"
              />
            ) : null}
          </Space>
        ),
      },
    ],
    []
  );

  const actionDisabledBase =
    !registerId ||
    busy !== null ||
    selectedRegisterIsDecommissioned ||
    tenantBlocksCreate;

  const missingMonatsbelegColumns: ColumnsType<MissingMonatsbelegTableRow> = useMemo(
    () => [
      {
        title: 'Periode',
        key: 'period',
        render: (_, record) => formatMonthYearDe(record.year, record.month),
      },
      {
        title: 'Rückstand',
        key: 'delay',
        width: 160,
        render: (_, record) => {
          const severity = monatsbelegDelaySeverity(record.monthDiff);
          return (
            <Space size={6}>
              <Tag color={severity.color}>{severity.label}</Tag>
              {record.monthDiff > 0 ? (
                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                  {record.monthDiff} {record.monthDiff === 1 ? 'Monat' : 'Monate'}
                </Typography.Text>
              ) : (
                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                  aktueller Monat
                </Typography.Text>
              )}
            </Space>
          );
        },
      },
      {
        title: 'Status',
        key: 'status',
        width: 120,
        render: (_, record) =>
          record.isOverdue ? <Tag color="red">Überfällig</Tag> : <Tag color="orange">Fehlt</Tag>,
      },
      {
        title: 'Aktion',
        key: 'action',
        width: 160,
        render: (_, record) => (
          <Space>
            {record.isMissing ? (
              <Button
                type="primary"
                size="small"
                disabled={actionDisabledBase || !canMonat}
                onClick={() => openMissingMonatsbelegModal(record.year, record.month)}
              >
                {record.monthDiff > 0 ? 'Nachträglich erstellen' : 'Erstellen'}
              </Button>
            ) : null}
          </Space>
        ),
      },
    ],
    [actionDisabledBase, canMonat, openMissingMonatsbelegModal]
  );

  return (
    <>
      <AdminPageHeader
        title="RKSV Sonderbelege"
        breadcrumbs={[
          ADMIN_OVERVIEW_CRUMB,
          { title: ADMIN_NAV_GROUP_LABELS.rksv, href: '/rksv' },
          { title: 'RKSV Sonderbelege' },
        ]}
      />

      <Card style={{ marginBottom: 16 }}>
        <Space orientation="vertical" style={{ width: '100%' }} size="middle">
          <SonderbelegeRegisterPicker
            registers={registers}
            loading={registersLoading}
            registerId={registerId}
            onRegisterChange={handleRegisterChange}
            showTenantFilter={isSuperAdmin}
            tenants={tenants.map((row) => ({ id: row.id, name: row.name, slug: row.slug }))}
            tenantsLoading={tenantsLoading}
            ambientTenantId={ambientTenantId}
            onTenantSwitchRequest={handleTenantSwitchRequest}
            onRegisterTenantMismatch={handleRegisterTenantMismatch}
          />
          {!registersLoading && registers.length === 0 ? (
            <Empty
              description={
                <Space orientation="vertical" size={4}>
                  <Typography.Text strong>
                    {t('rksvHub.sonderbelege.emptyRegistersTitle')}
                  </Typography.Text>
                  <Typography.Text type="secondary">
                    {t('rksvHub.sonderbelege.emptyRegistersDescription')}
                  </Typography.Text>
                </Space>
              }
            >
              {canCreateRegister ? (
                <Link href="/kassenverwaltung">
                  <Button type="primary">{t('rksvHub.sonderbelege.createRegister')}</Button>
                </Link>
              ) : null}
            </Empty>
          ) : null}
          {isTenantMismatch || orphanRegisterSelection ? (
            <Alert
              type="error"
              showIcon
              title={
                isTenantMismatch
                  ? t('rksvHub.sonderbelege.tenantMismatchTitle')
                  : t('rksvHub.sonderbelege.orphanRegisterTitle')
              }
              description={
                <Space orientation="vertical" size={8} style={{ width: '100%' }}>
                  <span>
                    {isTenantMismatch
                      ? t('rksvHub.sonderbelege.tenantMismatchDescription')
                      : t('rksvHub.sonderbelege.orphanRegisterDescription')}
                  </span>
                  <Button
                    size="small"
                    onClick={() => {
                      setRegisterId(undefined);
                      syncSonderbelegeQuery({ registerId: null });
                    }}
                  >
                    {t('rksvHub.sonderbelege.clearRegisterSelection')}
                  </Button>
                </Space>
              }
            />
          ) : null}
          <div>
            <Typography.Text type="secondary">
              Optionaler Grund / Notiz (für Sonderbelege)
            </Typography.Text>
            <Input
              value={reasonShort}
              onChange={(e) => setReasonShort(e.target.value)}
              maxLength={450}
              style={{ marginTop: 8 }}
            />
          </div>
          {selectedRegister && !isTenantMismatch ? (
            <Alert
              type={selectedRegisterIsDecommissioned ? 'warning' : 'info'}
              showIcon
              title={
                <Space wrap>
                  <span>
                    {t('rksvHub.sonderbelege.operatingStatus')}: {selectedStatusLabel}
                  </span>
                  <Tag color={selectedStatusVisual.color}>
                    {selectedStatusVisual.emoji} {selectedStatusLabel}
                  </Tag>
                </Space>
              }
              description={
                selectedRegisterIsDecommissioned
                  ? t('rksvHub.sonderbelege.decommissionedHint')
                  : t('rksvHub.sonderbelege.registerWithTenant', {
                      label: formatSonderbelegeRegisterLabel(selectedRegister, {
                        includeTenant: true,
                      }),
                    })
              }
            />
          ) : isTenantMismatch || orphanRegisterSelection ? null : (
            <Alert type="info" showIcon title={t('rksvHub.sonderbelege.selectRegisterFirst')} />
          )}
        </Space>
      </Card>

      <LateMonatsbelegCreationCard
        cashRegisterId={registerId}
        canCreate={canMonat}
        disabled={actionDisabledBase}
        onSuccess={() => {
          void refetchMonatsbelegData();
        }}
      />

      {isDevelopment && canTestHelper ? (
        <Card
          id="rksv-focus-test-helper"
          title="Test Helper (Demo-Modus)"
          style={{ marginBottom: 16 }}
        >
          <Space orientation="vertical" style={{ width: '100%' }} size="middle">
            <Alert
              type="warning"
              showIcon
              title="Demo-Modus: Beachten Sie, dass diese Belege nur zu Testzwecken dienen und nicht für den Produktivbetrieb verwendet werden dürfen."
            />
            <Space wrap>
              <Button
                onClick={() => void onBulkCreateMissingMonatsbelege()}
                loading={busy === 'demo-bulk'}
                disabled={!registerId || busy !== null || tenantBlocksCreate}
              >
                Monatsbeleg für Vormonat erstellen
              </Button>
              <Button
                onClick={() => void onCreateDemoNullbelegForCurrentMonth()}
                loading={busy === 'demo-null'}
                disabled={!registerId || busy !== null || tenantBlocksCreate}
              >
                Test-Nullbeleg für aktuellen Monat erstellen
              </Button>
              {canTseSimulation ? (
                <Button
                  danger
                  onClick={() => void onResetTseSimulation()}
                  loading={busy === 'demo-tse-reset'}
                  disabled={busy !== null}
                >
                  TSE-Simulation zurücksetzen
                </Button>
              ) : null}
            </Space>
          </Space>
        </Card>
      ) : null}

      <Row gutter={[16, 16]} style={{ marginBottom: 16 }}>
        <Col xs={24} md={12}>
          <Card
            title={titleWithTooltip(
              'Startbeleg',
              'Der Startbeleg muss unmittelbar nach der ersten Inbetriebnahme der Kasse erstellt werden, bevor ein regulärer Zahlungsvorgang durchgeführt werden kann. Nur ein Startbeleg pro Kasse möglich.'
            )}
            id="rksv-focus-startbeleg"
          >
            <Space orientation="vertical" style={{ width: '100%' }}>
              <Typography.Paragraph style={{ marginBottom: 0 }}>
                Erster Beleg nach Kassenaktivierung. Nur einmal pro Kasse möglich.
              </Typography.Paragraph>
              <Typography.Text type="secondary">
                Hinweis: Vor dem ersten regulären Verkauf muss der Startbeleg vorhanden sein.
              </Typography.Text>
              <StartbelegStatus
                exists={hasStartbelegForRegister}
                createdAtUtc={startbelegCreatedAtUtc}
                loading={Boolean(registerId) && (registersLoading || scanLoading)}
              />
              <Button
                type="primary"
                onClick={() => void onStartbeleg()}
                disabled={actionDisabledBase || hasStartbelegForRegister || !canStart}
                loading={busy === 'start'}
                block
              >
                Startbeleg erstellen
              </Button>
            </Space>
          </Card>
        </Col>

        <Col xs={24} md={12}>
          <Card
            title={titleWithTooltip(
              'Monatsbeleg',
              'Für jeden Kalendermonat muss ein Monatsbeleg erstellt werden, spätestens bis zum Ende des Folgemonats. Der Monatsbeleg dient der monatlichen Kontrolle der Registrierkasse.'
            )}
          >
            <Space orientation="vertical" style={{ width: '100%' }}>
              <Typography.Paragraph style={{ marginBottom: 0 }}>
                Monatlicher Kontrollbeleg. Pflicht für jeden Kalendermonat.
              </Typography.Paragraph>
              <Typography.Text type="secondary">
                Empfohlen: Monatsbeleg direkt nach Monatsende erstellen.
              </Typography.Text>
              <MonatsbelegInfoCard />
              <DatePicker
                picker="month"
                value={monatPeriod}
                onChange={(v) => v && setMonatPeriod(v)}
                disabledDate={(current) =>
                  !current || current.isAfter(maxMonatsbelegMonth, 'month')
                }
                style={{ width: '100%' }}
              />
              <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                Nur abgeschlossene (vergangene) Kalendermonate (Europe/Vienna). Der aktuelle Monat
                ist erst nach Monatsende wählbar. Vergangene Monate erfordern eine Bestätigung.
              </Typography.Text>
              <Button
                type="primary"
                onClick={openMonatsbelegModal}
                disabled={
                  actionDisabledBase ||
                  hasMonatsbelegForPeriod ||
                  !canMonat ||
                  monatIsCurrentOrFutureMonth
                }
                block
              >
                {`Monatsbeleg für ${formatMonthYearDe(monatYear, monatMonth)} erstellen`}
              </Button>
              {hasMonatsbelegForPeriod ? (
                <Alert
                  type="success"
                  showIcon
                  title="Monatsbeleg für den gewählten Zeitraum ist bereits vorhanden."
                />
              ) : null}
            </Space>
          </Card>
        </Col>

        <Col xs={24} md={12}>
          <Card
            title={titleWithTooltip(
              'Jahresbeleg',
              'Ein Jahresbeleg ist für jedes Kalenderjahr zu erstellen, spätestens bis zum 31. Jänner des Folgejahres. Der Monatsbeleg Dezember kann als Jahresbeleg verwendet werden.'
            )}
          >
            <Space orientation="vertical" style={{ width: '100%' }}>
              <Typography.Paragraph style={{ marginBottom: 0 }}>
                Jährlicher Kontrollbeleg. Dezember Monatsbeleg kann als Jahresbeleg dienen.
              </Typography.Paragraph>
              <Typography.Text type="secondary">
                Frist beachten: Erstellung bis spätestens 31. Jänner des Folgejahres.
              </Typography.Text>
              <DatePicker
                picker="year"
                value={jahrPeriod}
                onChange={(v) => v && setJahrPeriod(v)}
                style={{ width: '100%' }}
              />
              <Input
                placeholder="Optional: Grund bei vorzeitiger Erstellung"
                value={jbEarly}
                onChange={(e) => setJbEarly(e.target.value)}
                maxLength={450}
              />
              <Button
                type="primary"
                onClick={confirmJahresbeleg}
                disabled={actionDisabledBase || hasJahresbelegForYear || !canJahr}
                loading={busy === 'jahr'}
                block
              >
                {`Jahresbeleg für ${jahrYear} erstellen`}
              </Button>
              {hasJahresbelegForYear ? (
                <Alert
                  type="success"
                  showIcon
                  title="Jahresbeleg für das gewählte Jahr ist bereits vorhanden."
                />
              ) : null}
            </Space>
          </Card>
        </Col>

        <Col xs={24} md={12}>
          <Card
            title={titleWithTooltip(
              'Nullbeleg',
              'Der Nullbeleg ist ein Beleg mit Null-Betrag. Er kann zu Kontrollzwecken oder als Ersatz für den Monatsbeleg in bestimmten Ausnahmefällen (z.B. bei Umsatzsteuerbefreiung) verwendet werden.'
            )}
          >
            <Space orientation="vertical" style={{ width: '100%' }}>
              <Typography.Paragraph style={{ marginBottom: 0 }}>
                Der Nullbeleg wird nur bei einer Kassennachschau auf amtliche Aufforderung benötigt.
              </Typography.Paragraph>
              <Typography.Text type="secondary">
                Keine Planung oder Erinnerung erforderlich. Nur für Prüfzwecke.
              </Typography.Text>
              <Button
                type="primary"
                onClick={() => void onNullbeleg()}
                disabled={actionDisabledBase || !canNull}
                loading={busy === 'null'}
                block
              >
                Nullbeleg für Prüfzwecke erstellen
              </Button>
              {hasNullbelegForRegister ? (
                <Alert
                  type="info"
                  showIcon
                  title="Für diese Kasse existiert bereits mindestens ein Nullbeleg."
                />
              ) : null}
            </Space>
          </Card>
        </Col>

        <Col xs={24}>
          <Card
            id="rksv-focus-schlussbeleg"
            title={titleWithTooltip(
              'Schlussbeleg / Endbeleg',
              'Der Schlussbeleg wird bei endgültiger Stilllegung der Kasse erstellt. Nach Erstellung kann die Kasse keine weiteren Zahlungen mehr annehmen. Dies kann nicht rückgängig gemacht werden.'
            )}
            styles={{
              body: { border: '1px solid #ffccc7', borderRadius: 8, background: '#fff1f0' },
            }}
          >
            <Space orientation="vertical" style={{ width: '100%' }}>
              <Typography.Paragraph strong style={{ color: '#a8071a', marginBottom: 0 }}>
                Endgültige Stilllegung der Kasse. Nach Erstellung kann die Kasse keine Zahlungen
                mehr annehmen.
              </Typography.Paragraph>
              <Alert
                type="warning"
                showIcon
                title="Endbeleg wird NUR bei endgültiger Außerbetriebnahme verwendet (keine Saisonpause!)."
              />
              <Alert
                type="error"
                showIcon
                title="Achtung: Dieser Vorgang ist dauerhaft und kann nicht rückgängig gemacht werden."
              />
              <Typography.Text type="secondary">
                Nur verwenden, wenn die Kasse endgültig außer Betrieb genommen wird.
              </Typography.Text>
              <Typography.Text type="secondary">
                {
                  'Nach Erstellung wird der Status auf "Decommissioned" gesetzt. Neue Zahlungen sind danach nicht mehr erlaubt.'
                }
              </Typography.Text>
              {!canCreateSchlussbelegNow ? (
                <Alert
                  type="warning"
                  showIcon
                  title={
                    selectedRegisterHasOpenSession
                      ? 'Nicht verfügbar: Es besteht eine offene Sitzung. Bitte Sitzung schließen.'
                      : 'Nicht verfügbar: Endbeleg nur bei Kassenstatus „Geschlossen".'
                  }
                />
              ) : null}
              <Button
                danger
                type="primary"
                onClick={openSchlussbelegDialog}
                disabled={
                  actionDisabledBase ||
                  hasSchlussbelegForRegister ||
                  !canSchluss ||
                  !canCreateSchlussbelegNow
                }
                loading={busy === 'schluss'}
                block
              >
                Kasse stilllegen (Endbeleg)
              </Button>
              {hasSchlussbelegForRegister ? (
                <Alert
                  type="warning"
                  showIcon
                  title="Für diese Kasse existiert bereits ein Schlussbeleg."
                />
              ) : null}
            </Space>
          </Card>
        </Col>
      </Row>

      <Card
        title="Zuletzt erstellte Sonderbelege (mit Zweck)"
        style={{ marginBottom: 16 }}
        loading={scanLoading}
      >
        {recentSpecialReceipts.length === 0 ? (
          <Alert type="info" showIcon title="Noch keine Sonderbelege vorhanden." />
        ) : (
          <Row gutter={[12, 12]}>
            {recentSpecialReceipts.map((row) => {
              const kind = normalizeSpecialKind(row.rksvSpecialReceiptKind);
              const badge = specialReceiptBadge(kind);
              const y = Number(row.rksvSpecialReceiptYear ?? 0);
              const m = Number(row.rksvSpecialReceiptMonth ?? 0);
              const periodText =
                kind === 'monatsbeleg' && y > 0 && m > 0
                  ? `Abgedeckter Monat: ${formatMonthYearDe(y, m)}`
                  : kind === 'jahresbeleg' && y > 0
                    ? `Abgedecktes Jahr: ${y}`
                    : kind === 'nullbeleg' && y > 0 && m > 0
                      ? `Bezogen auf: ${formatMonthYearDe(y, m)}`
                      : 'Periode: —';
              return (
                <Col
                  xs={24}
                  md={12}
                  lg={8}
                  key={row.receiptId ?? `${row.receiptNumber}-${row.issuedAt}`}
                >
                  <Card size="small">
                    <Space orientation="vertical" size={6} style={{ width: '100%' }}>
                      <Space>
                        <Tag color={badge.color}>{badge.text}</Tag>
                        {receiptIsLateCreated(row) ? (
                          <Tag color="orange">Verspätet erstellt</Tag>
                        ) : (
                          <Tag color="green">Erfolgreich erstellt</Tag>
                        )}
                      </Space>
                      <Typography.Text strong>
                        {row.receiptNumber || 'Ohne Belegnummer'}
                      </Typography.Text>
                      <Typography.Text type="secondary">
                        Erstellt am: {row.issuedAt ? formatDateTime(row.issuedAt, '') : '—'}
                      </Typography.Text>
                      <Typography.Text>{periodText}</Typography.Text>
                      <Typography.Text type="secondary">
                        {specialReceiptPurposeDe(kind)}
                      </Typography.Text>
                      <Space wrap>
                        {row.receiptId ? (
                          <Link href={`/receipts/${row.receiptId}`}>
                            <Button size="small">Details öffnen</Button>
                          </Link>
                        ) : null}
                        {row.paymentId ? (
                          <>
                            <StoredReportPdfButton
                              reportType={reportPdfTypeFromSpecialReceiptKind(
                                row.rksvSpecialReceiptKind
                              )}
                              targetId={row.paymentId}
                              fileNameBase={row.receiptNumber}
                              size="small"
                            />
                            <ReprintButton
                              paymentId={row.paymentId}
                              receiptNumber={row.receiptNumber}
                              size="small"
                            />
                          </>
                        ) : null}
                      </Space>
                    </Space>
                  </Card>
                </Col>
              );
            })}
          </Row>
        )}
      </Card>

      <Card
        id="rksv-missing-monatsbelege"
        title="Fehlende Monatsbelege"
        style={{ marginBottom: 16 }}
        loading={Boolean(registerId) && monatsbelegStatusQuery.isLoading}
      >
        {!registerId ? (
          <Alert type="info" showIcon title="Bitte zuerst eine Kasse auswählen." />
        ) : (
          <Space orientation="vertical" style={{ width: '100%' }} size="middle">
            <Alert
              type="info"
              showIcon
              title="Hinweise zu vergangenen Monatsbelegen"
              description={
                <ul style={{ marginBottom: 0, paddingLeft: 20 }}>
                  <li>Aktueller Monat: direkt erstellbar</li>
                  <li>1 Monat zurück: Info-Hinweis</li>
                  <li>2–6 Monate zurück: Warnung, Bestätigung erforderlich</li>
                  <li>Über 6 Monate: erhöhtes Risiko, Audit-Log</li>
                </ul>
              }
            />
            <Table<MissingMonatsbelegTableRow>
              rowKey="key"
              size="small"
              pagination={missingMonatsbelegRows.length > 12 ? { pageSize: 12 } : false}
              columns={missingMonatsbelegColumns}
              dataSource={missingMonatsbelegRows}
              locale={{ emptyText: 'Keine fehlenden Monatsbelege für diese Kasse.' }}
            />
          </Space>
        )}
      </Card>

      <Card
        id="rksv-monatsbeleg-timeline"
        title={t('rksvHub.monatsbelegTimeline.cardTitle', { year: monatYear })}
        style={{ marginBottom: 16 }}
        extra={
          <DatePicker
            picker="year"
            value={dayjs(`${monatYear}-01-01`)}
            onChange={(v) => v && setMonatPeriod((prev) => prev.year(v.year()))}
          />
        }
      >
        {!registerId ? (
          <Alert type="info" showIcon title={t('rksvHub.monatsbelegTimeline.needRegister')} />
        ) : (
          <MonatsbelegTimeline
            year={monatYear}
            months={monthlyTimelineRows}
            cashRegisterId={registerId}
            canRecreate={isSuperAdmin}
            onCreateLate={openMissingMonatsbelegModal}
          />
        )}
      </Card>

      <Card title="Bestehende Sonderbelege" loading={scanLoading}>
        <Table<OrvalReceiptRow>
          rowKey={(r) => r.receiptId ?? ''}
          dataSource={registerId ? registerScopedReceipts : (receiptScan ?? [])}
          columns={specialColumns}
          pagination={{ pageSize: 12 }}
          size="small"
          locale={{
            emptyText: registerId
              ? 'Für die ausgewählte Kasse wurden keine Sonderbelege gefunden.'
              : 'Keine Sonderbelege in den letzten 300 Belegen.',
          }}
        />
      </Card>

      {registerId && !tenantBlocksCreate ? (
        <CreateMonatsbelegModal
          open={monatsbelegModalOpen}
          cashRegisterId={registerId}
          cashRegisterLabel={
            selectedRegister
              ? formatSonderbelegeRegisterLabel(selectedRegister, { includeTenant: true })
              : undefined
          }
          year={selectedMonatsbelegYear}
          month={selectedMonatsbelegMonth}
          reason={reasonShort.trim() || 'Admin Monatsbeleg'}
          onClose={() => setMonatsbelegModalOpen(false)}
          onSuccess={() => {
            void refetchMonatsbelegData();
            setMonatsbelegModalOpen(false);
          }}
        />
      ) : null}

      <Modal
        title="Kasse stilllegen (Endbeleg)"
        open={schlussModalOpen}
        onCancel={() => {
          setSchlussModalOpen(false);
          setSchlussConfirmText('');
        }}
        okText="Endbeleg endgültig erstellen"
        okButtonProps={{ danger: true, loading: busy === 'schluss' }}
        onOk={() => void submitSchlussModal()}
      >
        <Typography.Paragraph strong>
          Diese Aktion deaktiviert die Kasse dauerhaft. Nach dem Endbeleg sind keine neuen Zahlungen
          oder Kassiervorgänge mehr möglich.
        </Typography.Paragraph>
        <Typography.Paragraph type="warning">
          Nicht für Feiertage, Betriebsferien oder saisonale Pausen verwenden.
        </Typography.Paragraph>
        <Alert
          type="error"
          showIcon
          style={{ marginBottom: 12 }}
          title="Starke Bestätigung erforderlich"
          description='Gib zur Bestätigung exakt «ENDBELEG» ein. Status wird auf "Decommissioned" gesetzt.'
        />
        <Input
          placeholder="ENDBELEG"
          value={schlussConfirmText}
          onChange={(e) => setSchlussConfirmText(e.target.value)}
          autoComplete="off"
        />
      </Modal>
    </>
  );
}
