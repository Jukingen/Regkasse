import {
  getLicenseRenewalFunnelStepPercent,
  getLicenseRenewalFunnelStrokeColor,
} from '../licenseRenewalFunnel';

describe('licenseRenewalFunnel', () => {
  it('computes step percent safely', () => {
    expect(getLicenseRenewalFunnelStepPercent(0, 0)).toBe(0);
    expect(getLicenseRenewalFunnelStepPercent(1, 2)).toBe(50);
    expect(getLicenseRenewalFunnelStepPercent(3, 2)).toBe(100);
  });

  it('picks stroke colors by step and threshold', () => {
    expect(getLicenseRenewalFunnelStrokeColor('pageView', 10)).toBe('#1890ff');
    expect(getLicenseRenewalFunnelStrokeColor('renewed', 60)).toBe('#52c41a');
    expect(getLicenseRenewalFunnelStrokeColor('renewed', 40)).toBe('#faad14');
    expect(getLicenseRenewalFunnelStrokeColor('activated', 10)).toBe('#cf1322');
  });
});
