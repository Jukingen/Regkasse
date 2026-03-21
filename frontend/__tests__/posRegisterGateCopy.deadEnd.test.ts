import {
  buildPosRegisterGateContext,
  registerGateBannerDetail,
  registerGateBannerTitle,
  registerGateFooterHint,
} from '../utils/posRegisterGateCopy';

describe('posRegisterGateCopy dead-end avoidance', () => {
  it('select_register with empty picklist: Einstellungen, not "unten" chip instruction', () => {
    const ctx = buildPosRegisterGateContext({
      settingsLoadFailed: false,
      registerListFailureKind: null,
      registerListLoading: false,
      registerPicklistCount: 0,
      readiness: {
        loading: false,
        error: false,
        nextAction: 'select_register',
        messageCode: 'CASH_REGISTER_REQUIRED',
      },
    });
    const detail = registerGateBannerDetail(ctx);
    expect(detail).toMatch(/Einstellungen/i);
    expect(detail).not.toMatch(/unten eine Kasse antippen/i);
    expect(registerGateBannerTitle(ctx)).toContain('auswählen');
    expect(registerGateFooterHint(ctx)).toMatch(/Einstellungen/i);
  });

  it('select_register with forbidden list: no false "tap below" promise', () => {
    const ctx = buildPosRegisterGateContext({
      settingsLoadFailed: false,
      registerListFailureKind: 'forbidden',
      registerListLoading: false,
      registerPicklistCount: 0,
      readiness: {
        loading: false,
        error: false,
        nextAction: 'select_register',
        messageCode: 'CASH_REGISTER_REQUIRED',
      },
    });
    const detail = registerGateBannerDetail(ctx);
    expect(detail).toMatch(/Administrator/i);
    expect(detail).not.toMatch(/unten eine Kasse antippen/i);
  });
});
