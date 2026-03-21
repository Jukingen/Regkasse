import {
  registerGateBannerTitle,
  registerGateBannerDetail,
  registerGateFooterHint,
  registerGateAlertMessage,
} from '../utils/registerGateUserMessages';

describe('registerGateUserMessages (settings load vs empty register)', () => {
  it('shows settings error title when settings failed, not generic no-register', () => {
    expect(registerGateBannerTitle(null, false, 0, true)).toContain('nicht ladbar');
    expect(registerGateBannerDetail(null, false, 0, true)).toContain('Kasseneinstellungen');
    expect(registerGateFooterHint(null, false, 0, true)).toContain('Kasseneinstellungen');
    expect(registerGateAlertMessage(null, 0, true)).toContain('Kasseneinstellungen');
  });

  it('keeps selection title when multiple registers without settings failure', () => {
    expect(registerGateBannerTitle(null, false, 3, false)).toBe('Kasse wählen');
  });
});
