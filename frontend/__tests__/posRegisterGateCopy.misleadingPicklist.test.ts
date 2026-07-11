import {
  POS_READINESS_MESSAGE_CODES,
  buildPosRegisterGateContext,
  registerGateAlertMessage,
  registerGateBannerDetail,
  registerGateBannerTitle,
  registerGateFooterHint,
} from '../utils/posRegisterGateCopy';

describe('posRegisterGateCopy: non-empty misleading picklist vs readiness', () => {
  it('open_register + CLOSED: does not promise chip/list selection despite non-empty picklist count', () => {
    const ctx = buildPosRegisterGateContext({
      settingsLoadFailed: false,
      registerListFailureKind: null,
      registerListLoading: false,
      registerPicklistCount: 4,
      registerListEmptyReason: null,
      readiness: {
        loading: false,
        error: false,
        nextAction: 'open_register',
        messageCode: POS_READINESS_MESSAGE_CODES.CLOSED,
      },
    });
    expect(registerGateBannerTitle(ctx)).toMatch(/geschlossen/i);
    const detail = registerGateBannerDetail(ctx);
    expect(detail).toMatch(/geschlossen|Schicht|Kassenverwaltung/i);
    expect(detail).not.toMatch(/unten eine Kasse antippen/i);
    expect(registerGateFooterHint(ctx)).toMatch(/Schicht/i);
    expect(registerGateFooterHint(ctx)).not.toMatch(/Berechtigung/i);
    expect(registerGateFooterHint(ctx)).not.toMatch(/Kasse wählen — dann wird „Zahlen“ aktiv/i);
    expect(registerGateAlertMessage(ctx)).toMatch(/geschlossen|Schicht/i);
    expect(registerGateAlertMessage(ctx)).not.toMatch(/Liste.*bezahlen/i);
  });
});
