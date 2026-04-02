import React from 'react';
import '@testing-library/jest-dom';
import axios from 'axios';
import { Modal } from 'antd';
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';

beforeAll(() => {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    configurable: true,
    value: vi.fn().mockImplementation((query: string) => ({
      matches: false,
      media: query,
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  });
});
import { BackupExecutionModeCard } from '@/features/backup-dr/components/BackupExecutionModeCard';

const getMock = vi.fn();
const putMock = vi.fn();

vi.mock('@/features/backup-dr/logic/backupExecutionModeApi', async () => {
  const q = await import('@/features/backup-dr/logic/backupExecutionModeQueryKeys');
  return {
    getGetApiAdminBackupExecutionModeQueryKey: q.getGetApiAdminBackupExecutionModeQueryKey,
    BACKUP_EXECUTION_MODE_API_PATH: q.BACKUP_EXECUTION_MODE_API_PATH,
    getBackupExecutionMode: (...a: unknown[]) => getMock(...a),
    putBackupExecutionMode: (...a: unknown[]) => putMock(...a),
  };
});

const baseDto = {
  storedMode: 'InheritFromConfiguration',
  requestedUserFacingMode: 'UseConfigurationDefault',
  configurationDefaultUserFacingMode: 'Fake',
  effectiveUserFacingMode: 'Fake',
  recommendedFallbackUserFacingMode: null,
  adapterKindIfConfigurationDefaultOnly: 'Fake',
  effectiveModeResolutionSummaryEnglish: 'Requested=UseConfigurationDefault, effective=Fake, runnable=true.',
  configurationExecutionAdapterKind: 'Fake',
  effectiveExecutionAdapterKind: 'Fake',
  effectiveModeRunnable: true,
  hypotheticalPgDumpHealthLevel: 'Healthy',
  blockers: [],
  realModeBlockingDiagnostics: [],
  selectableModes: [
    { userFacingMode: 'UseConfigurationDefault', internalMode: 'InheritFromConfiguration', selectable: true },
    { userFacingMode: 'Fake', internalMode: 'SimulatedFake', selectable: true, blockReason: null },
    { userFacingMode: 'RealPgDump', internalMode: 'PostgreSqlPgDump', selectable: true },
  ],
  effectiveConfigurationHealth: {},
};

function t(k: string, options?: Record<string, string | number>): string {
  const map: Record<string, string> = {
    'backupDr.executionMode.title': 'Execution mode',
    'backupDr.executionMode.meaningTitle': 'Meanings',
    'backupDr.executionMode.meaningFake': 'Fake meaning',
    'backupDr.executionMode.meaningReal': 'Real meaning',
    'backupDr.executionMode.intro': 'Intro',
    'backupDr.executionMode.labelRequested': 'Requested',
    'backupDr.executionMode.labelEffective': 'Effective',
    'backupDr.executionMode.labelConfigDefault': 'Default',
    'backupDr.executionMode.adapterInternal': 'Adapter',
    'backupDr.executionMode.userFacing.fake': 'Fake',
    'backupDr.executionMode.userFacing.realPgDump': 'Real',
    'backupDr.executionMode.userFacing.useConfig': 'Follow config',
    'backupDr.executionMode.userFacing.productionStub': 'Stub',
    'backupDr.executionMode.optionInherit': 'Inherit',
    'backupDr.executionMode.optionFake': 'Fake opt',
    'backupDr.executionMode.optionPgDump': 'PgDump opt',
    'backupDr.executionMode.helpInherit': 'help inherit',
    'backupDr.executionMode.helpFake': 'help fake',
    'backupDr.executionMode.helpPgDump': 'help pgdump',
    'backupDr.executionMode.save': 'Save',
    'backupDr.executionMode.saveConfirmTitleInherit': 'Confirm inherit?',
    'backupDr.executionMode.saveConfirmBodyInherit': 'Body inherit',
    'backupDr.executionMode.saveConfirmTitleFake': 'Confirm fake?',
    'backupDr.executionMode.saveConfirmBodyFake': 'Body fake',
    'backupDr.executionMode.saveConfirmBodyFakeDevHint': 'Dev hint',
    'backupDr.executionMode.saveConfirmOk': 'OK',
    'backupDr.executionMode.saveRejectedHint': 'Rejected hint',
    'backupDr.executionMode.viewOnlyHint': 'View only',
    'backupDr.executionMode.realRunnableYes': 'Runnable yes',
    'backupDr.executionMode.realRunnableNo': 'Runnable no',
    'backupDr.executionMode.resolutionSummaryLabel': 'Resolution:',
    'backupDr.executionMode.realReadiness.summary.readyTitle': 'Real ready',
    'backupDr.executionMode.realReadiness.summary.readyBody': 'Real ready body',
    'backupDr.executionMode.realReadiness.summary.blockedTitle': 'Real blocked',
    'backupDr.executionMode.realReadiness.summary.blockedBody': 'Real blocked body',
    'backupDr.executionMode.realReadiness.summary.degradedSelectableTitle': 'Degraded',
    'backupDr.executionMode.realReadiness.summary.degradedSelectableBody': 'Degraded body',
    'backupDr.executionMode.realReadiness.summary.noLevelTitle': 'No level',
    'backupDr.executionMode.realReadiness.summary.noLevelBody': 'No level body',
    'backupDr.executionMode.realReadiness.panelTitle': 'Real readiness',
    'backupDr.executionMode.realReadiness.footnoteApi': 'Footnote',
    'backupDr.executionMode.realReadiness.configKeysLabel': 'Keys:',
    'backupDr.executionMode.realReadiness.serverDetailLabel': 'Detail:',
    'backupDr.executionMode.realReadiness.noDiagnosticsListed': 'No diagnostics',
    'backupDr.executionMode.realReadiness.realRowSelectableYes': 'Selectable yes',
    'backupDr.executionMode.realReadiness.realRowSelectableNo': 'Selectable no',
    'backupDr.executionMode.realReadiness.healthLevels.unknown': 'Unknown',
    'backupDr.executionMode.realReadiness.healthLevels.Healthy': 'Healthy',
    'backupDr.executionMode.realReadiness.healthLevels.Degraded': 'Degraded',
    'backupDr.executionMode.realReadiness.healthLevels.Unhealthy': 'Unhealthy',
    'backupDr.executionMode.realReadiness.tier.blocking': 'Blocking',
    'backupDr.executionMode.realReadiness.tier.advisory': 'Advisory',
    'backupDr.executionMode.realReadiness.category.staging': 'Staging',
    'backupDr.executionMode.realReadiness.category.connection': 'Connection',
    'backupDr.executionMode.realReadiness.category.pgDump': 'PgDump',
    'backupDr.executionMode.realReadiness.category.pgRestore': 'PgRestore',
    'backupDr.executionMode.realReadiness.category.archive': 'Archive',
    'backupDr.executionMode.realReadiness.category.policy': 'Policy',
    'backupDr.executionMode.realReadiness.category.environment': 'Environment',
    'backupDr.executionMode.realReadiness.category.schedule': 'Schedule',
    'backupDr.executionMode.realReadiness.category.orchestrator': 'Orchestrator',
    'backupDr.executionMode.realReadiness.category.other': 'Other',
    'common.buttons.cancel': 'Cancel',
    'backupDr.executionMode.saveConfirmTitleReal': 'Confirm real?',
    'backupDr.executionMode.saveConfirmBodyReal': 'Body real',
    'backupDr.executionMode.realSavedButBlockedTitle': 'Real blocked title',
    'backupDr.executionMode.realSavedButBlockedBody': 'Real blocked body',
    'backupDr.executionMode.noFakeFallbackExplanation': 'No silent fake',
    'backupDr.executionMode.recommendedFallbackTitle': 'Fallback title',
    'backupDr.executionMode.recommendedFallbackBody': 'Fallback desc',
    'backupDr.executionMode.errors.forbidden': 'Load forbidden',
    'backupDr.executionMode.errors.pgDumpUnhealthy': 'Pg unhealthy save',
    'backupDr.executionMode.errors.confirmFakeRequired': 'Confirm fake API',
    'backupDr.executionMode.saveConfirmTitleFakeStrong': 'Confirm fake strong?',
    'backupDr.executionMode.saveConfirmBodyFakeStrong': 'Body fake strong',
    'backupDr.executionMode.notRunnableTitle': 'Not runnable title',
  };
  const mapped = map[k];
  if (mapped !== undefined) return mapped;
  if (options && Object.keys(options).length > 0) return `${k}:${JSON.stringify(options)}`;
  return k;
}

describe('BackupExecutionModeCard', () => {
  beforeEach(() => {
    Modal.destroyAll();
    getMock.mockReset();
    putMock.mockReset();
    getMock.mockResolvedValue({ ...baseDto });
  });

  afterEach(() => {
    Modal.destroyAll();
    cleanup();
    vi.restoreAllMocks();
  });

  it('loads and shows requested / effective / default rows', async () => {
    render(<BackupExecutionModeCard canManage t={t} />);
    await waitFor(() => expect(screen.getByText('Execution mode')).toBeInTheDocument());
    expect(screen.getByText('Requested')).toBeInTheDocument();
    expect(screen.getByText('Effective')).toBeInTheDocument();
    expect(screen.getByText('Default')).toBeInTheDocument();
  });

  it('disables save when canManage is false', async () => {
    render(<BackupExecutionModeCard canManage={false} t={t} />);
    await waitFor(() => expect(screen.getByRole('button', { name: 'Save' })).toBeDisabled());
    expect(screen.getByText('View only')).toBeInTheDocument();
  });

  it('opens confirm modal before save', async () => {
    putMock.mockResolvedValue({ ...baseDto });

    render(<BackupExecutionModeCard canManage t={t} />);
    await waitFor(() => expect(screen.getByRole('radio', { name: /Inherit/i })).toBeInTheDocument());

    fireEvent.click(screen.getByRole('radio', { name: /Fake opt/i }));
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));

    await waitFor(() => {
      expect(screen.getAllByText('Confirm fake?').length).toBeGreaterThanOrEqual(1);
    });
    const dialog = screen.getAllByRole('dialog').pop()!;
    fireEvent.click(within(dialog).getByRole('button', { name: 'OK' }));

    await waitFor(() => {
      expect(putMock).toHaveBeenCalledWith({
        mode: 'Fake',
        confirmSimulatedOnlyOperationalRiskInProduction: false,
      });
    });
  });

  it('authorized user can select Real and save uses confirm copy for Real (effective target PgDump)', async () => {
    putMock.mockResolvedValue({ ...baseDto, requestedUserFacingMode: 'RealPgDump', storedMode: 'PostgreSqlPgDump' });

    render(<BackupExecutionModeCard canManage t={t} />);
    await waitFor(() => expect(screen.getByRole('radio', { name: /PgDump opt/i })).toBeInTheDocument());

    fireEvent.click(screen.getByRole('radio', { name: /PgDump opt/i }));
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));

    await waitFor(() => {
      expect(screen.getAllByText('Confirm real?').length).toBeGreaterThanOrEqual(1);
    });
    const dialog = screen.getAllByRole('dialog').pop()!;
    expect(within(dialog).getByText('Body real')).toBeInTheDocument();
    fireEvent.click(within(dialog).getByRole('button', { name: 'OK' }));

    await waitFor(() => {
      expect(putMock).toHaveBeenCalledWith({
        mode: 'RealPgDump',
        confirmSimulatedOnlyOperationalRiskInProduction: false,
      });
    });
  });

  it('disables radios when canManage is false (cannot change requested mode)', async () => {
    render(<BackupExecutionModeCard canManage={false} t={t} />);
    const card = await waitFor(() => {
      const title = screen.getByText('Execution mode');
      const el = title.closest('.ant-card');
      expect(el).toBeTruthy();
      return el as HTMLElement;
    });
    await waitFor(() => expect(within(card).getAllByRole('radio')).toHaveLength(3));
    const wrappers = card.querySelectorAll('.ant-radio-wrapper-disabled');
    expect(wrappers.length).toBe(3);
  });

  it('shows Real prerequisite block reason when Real row is not selectable', async () => {
    getMock.mockResolvedValue({
      ...baseDto,
      selectableModes: [
        { userFacingMode: 'UseConfigurationDefault', internalMode: 'InheritFromConfiguration', selectable: true },
        { userFacingMode: 'Fake', internalMode: 'SimulatedFake', selectable: true },
        {
          userFacingMode: 'RealPgDump',
          internalMode: 'PostgreSqlPgDump',
          selectable: false,
          blockReason: 'BLOCK_REAL_ROW',
        },
      ],
      hypotheticalPgDumpHealthLevel: 'Unhealthy',
    });

    render(<BackupExecutionModeCard canManage t={t} />);
    await waitFor(() => expect(screen.getByText('BLOCK_REAL_ROW')).toBeInTheDocument());
    expect(screen.getByRole('radio', { name: /PgDump opt/i })).toBeDisabled();
  });

  it('shows saved-Real-but-blocked alert when API reports Real requested with PgDump adapter but not runnable', async () => {
    getMock.mockResolvedValue({
      ...baseDto,
      storedMode: 'PostgreSqlPgDump',
      requestedUserFacingMode: 'RealPgDump',
      effectiveUserFacingMode: 'RealPgDump',
      effectiveExecutionAdapterKind: 'PgDump',
      effectiveModeRunnable: false,
      recommendedFallbackUserFacingMode: 'UseConfigurationDefault',
    });

    render(<BackupExecutionModeCard canManage t={t} />);
    await waitFor(() => expect(screen.getByText('Real blocked title')).toBeInTheDocument());
    expect(screen.getByText('No silent fake')).toBeInTheDocument();
    expect(screen.getByText('Fallback title')).toBeInTheDocument();
    expect(screen.getByText('Fallback desc')).toBeInTheDocument();
  });

  it('surfaces requested vs effective mismatch honestly in description rows', async () => {
    getMock.mockResolvedValue({
      ...baseDto,
      requestedUserFacingMode: 'RealPgDump',
      storedMode: 'PostgreSqlPgDump',
      effectiveUserFacingMode: 'Fake',
      effectiveExecutionAdapterKind: 'Fake',
      effectiveModeResolutionSummaryEnglish: 'Requested=RealPgDump, effective=Fake.',
    });

    render(<BackupExecutionModeCard canManage t={t} />);
    await waitFor(() => {
      const realTags = screen.getAllByText('Real');
      expect(realTags.length).toBeGreaterThanOrEqual(1);
    });
    expect(screen.getAllByText('Fake').length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText(/Requested=RealPgDump, effective=Fake/)).toBeInTheDocument();
  });

  it('maps BACKUP_PG_DUMP_PREREQUISITES_UNHEALTHY to operator-facing save error', async () => {
    vi.spyOn(axios, 'isAxiosError').mockReturnValue(true);
    putMock.mockRejectedValue({
      response: { status: 422, data: { code: 'BACKUP_PG_DUMP_PREREQUISITES_UNHEALTHY' } },
    });

    render(<BackupExecutionModeCard canManage t={t} />);
    await waitFor(() => expect(screen.getByRole('radio', { name: /Fake opt/i })).toBeInTheDocument());

    fireEvent.click(screen.getByRole('radio', { name: /Fake opt/i }));
    fireEvent.click(screen.getByRole('button', { name: 'Save' }));
    await waitFor(() => {
      expect(screen.getAllByText('Confirm fake?').length).toBeGreaterThanOrEqual(1);
    });
    const dialog = screen.getAllByRole('dialog').pop()!;
    fireEvent.click(within(dialog).getByRole('button', { name: 'OK' }));

    await waitFor(() => expect(screen.getByText('Pg unhealthy save')).toBeInTheDocument());
  });

  it('maps load 403 to forbidden copy', async () => {
    vi.spyOn(axios, 'isAxiosError').mockReturnValue(true);
    getMock.mockRejectedValue({ response: { status: 403 } });

    render(<BackupExecutionModeCard canManage t={t} />);
    await waitFor(() => expect(screen.getByText('Load forbidden')).toBeInTheDocument());
  });
});
