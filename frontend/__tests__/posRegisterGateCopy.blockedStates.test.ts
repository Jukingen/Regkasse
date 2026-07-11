import {
  POS_READINESS_MESSAGE_CODES,
  buildPosRegisterGateContext,
  registerGateAlertMessage,
  registerGateBannerDetail,
  registerGateBannerTitle,
  registerGateFooterHint,
} from '../utils/posRegisterGateCopy';

describe('posRegisterGateCopy blocked-state differentiation', () => {
  it('empty selectable + no_registers: says no register in system, not generic pick', () => {
    const ctx = buildPosRegisterGateContext({
      settingsLoadFailed: false,
      registerListFailureKind: null,
      registerListLoading: false,
      registerPicklistCount: 0,
      registerListEmptyReason: 'no_registers',
      readiness: {
        loading: false,
        error: false,
        nextAction: 'select_register',
        messageCode: POS_READINESS_MESSAGE_CODES.REQUIRED,
      },
    });
    expect(registerGateBannerTitle(ctx)).toContain('Keine Kasse im System');
    expect(registerGateBannerDetail(ctx)).toMatch(/angelegt/i);
    expect(registerGateFooterHint(ctx)).toMatch(/Administrator/i);
  });

  it('empty selectable + none_open: stresses Schicht / closed registers', () => {
    const ctx = buildPosRegisterGateContext({
      settingsLoadFailed: false,
      registerListFailureKind: null,
      registerListLoading: false,
      registerPicklistCount: 0,
      registerListEmptyReason: 'none_open',
      readiness: {
        loading: false,
        error: false,
        nextAction: 'select_register',
        messageCode: POS_READINESS_MESSAGE_CODES.REQUIRED,
      },
    });
    expect(registerGateBannerTitle(ctx)).toMatch(/geöffnet/i);
    expect(registerGateBannerDetail(ctx)).toMatch(/Schicht/i);
    expect(registerGateAlertMessage(ctx)).toMatch(/geschlossen|Schicht/i);
  });

  it('empty selectable + none_selectable_for_user: conflict with other shifts / visibility', () => {
    const ctx = buildPosRegisterGateContext({
      settingsLoadFailed: false,
      registerListFailureKind: null,
      registerListLoading: false,
      registerPicklistCount: 0,
      registerListEmptyReason: 'none_selectable_for_user',
      readiness: {
        loading: false,
        error: false,
        nextAction: 'select_register',
        messageCode: POS_READINESS_MESSAGE_CODES.REQUIRED,
      },
    });
    expect(registerGateBannerTitle(ctx)).toMatch(/freie Kasse/i);
    expect(registerGateBannerDetail(ctx)).toMatch(/anderen Benutzern|Kassenansicht/i);
  });

  it('ensure-ready CONFLICT: dedicated banner detail (not generic empty list)', () => {
    const ctx = buildPosRegisterGateContext({
      settingsLoadFailed: false,
      registerListFailureKind: null,
      registerListLoading: false,
      registerPicklistCount: 0,
      registerListEmptyReason: null,
      readiness: {
        loading: false,
        error: false,
        nextAction: 'forbidden',
        messageCode: POS_READINESS_MESSAGE_CODES.CONFLICT,
      },
    });
    expect(registerGateBannerTitle(ctx)).toMatch(/Verwendung|Kasse/i);
    expect(registerGateBannerDetail(ctx)).toMatch(/Schicht|anderen Person/i);
  });

  it('open_register + FORBIDDEN: permission to open, not conflict wording', () => {
    const ctx = buildPosRegisterGateContext({
      settingsLoadFailed: false,
      registerListFailureKind: null,
      registerListLoading: false,
      registerPicklistCount: 0,
      readiness: {
        loading: false,
        error: false,
        nextAction: 'open_register',
        messageCode: POS_READINESS_MESSAGE_CODES.FORBIDDEN,
      },
    });
    expect(registerGateBannerTitle(ctx)).toMatch(/nicht erlaubt/i);
    expect(registerGateBannerDetail(ctx)).toMatch(/Rolle|Administrator/i);
    expect(registerGateBannerDetail(ctx)).not.toMatch(/anderen Benutzern zugeordnet/i);
    expect(registerGateFooterHint(ctx)).toMatch(/nicht erlaubt|Berechtigung/i);
    expect(registerGateFooterHint(ctx)).not.toMatch(/Schicht starten/i);
  });

  it('RKSV Monatsbeleg missing: dedicated title/detail/footer/alert', () => {
    const ctx = buildPosRegisterGateContext({
      settingsLoadFailed: false,
      registerListFailureKind: null,
      registerListLoading: false,
      registerPicklistCount: 0,
      registerListEmptyReason: null,
      readiness: {
        loading: false,
        error: false,
        nextAction: 'monatsbeleg_required',
        messageCode: POS_READINESS_MESSAGE_CODES.MONATSBELEG_REQUIRED,
      },
    });
    expect(registerGateBannerTitle(ctx)).toMatch(/Monatsbeleg/i);
    expect(registerGateBannerDetail(ctx)).toMatch(/Monatsbeleg|Monat/i);
    expect(registerGateFooterHint(ctx)).toMatch(/Monatsbeleg/i);
    expect(registerGateAlertMessage(ctx)).toMatch(/Monatsbeleg/i);
  });

  it('RKSV Startbeleg missing: dedicated title/detail/footer/alert', () => {
    const ctx = buildPosRegisterGateContext({
      settingsLoadFailed: false,
      registerListFailureKind: null,
      registerListLoading: false,
      registerPicklistCount: 0,
      registerListEmptyReason: null,
      readiness: {
        loading: false,
        error: false,
        nextAction: 'startbeleg_required',
        messageCode: POS_READINESS_MESSAGE_CODES.STARTBELEG_REQUIRED,
      },
    });
    expect(registerGateBannerTitle(ctx)).toMatch(/Startbeleg/i);
    expect(registerGateBannerDetail(ctx)).toMatch(/Startbeleg|RKSV/i);
    expect(registerGateFooterHint(ctx)).toMatch(/Startbeleg/i);
    expect(registerGateAlertMessage(ctx)).toMatch(/Startbeleg/i);
  });

  it('GET selectable forbidden: permission copy, independent of emptyReason', () => {
    const ctx = buildPosRegisterGateContext({
      settingsLoadFailed: false,
      registerListFailureKind: 'forbidden',
      registerListLoading: false,
      registerPicklistCount: 0,
      registerListEmptyReason: 'none_open',
      readiness: {
        loading: false,
        error: false,
        nextAction: 'select_register',
        messageCode: POS_READINESS_MESSAGE_CODES.REQUIRED,
      },
    });
    expect(registerGateBannerDetail(ctx)).toMatch(/Berechtigung|Kassenansicht/i);
    expect(registerGateBannerDetail(ctx)).not.toMatch(/Alle Kassen sind geschlossen/);
  });
});
