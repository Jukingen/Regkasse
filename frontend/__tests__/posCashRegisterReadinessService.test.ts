import { parsePosCashRegisterContextDto } from '../utils/posCashRegisterReadinessParse';

describe('parsePosCashRegisterContextDto', () => {
  it('reads camelCase fields', () => {
    const dto = parsePosCashRegisterContextDto({
      effectiveRegisterId: 'a1b2c3d4-e5f6-7890-abcd-ef1234567890',
      resolution: 'sole_register',
      registerStatus: 'Open',
      autoOpened: true,
      nextAction: 'ready',
      messageCode: 'CASH_REGISTER_AUTO_OPENED',
    });
    expect(dto.effectiveRegisterId).toContain('a1b2');
    expect(dto.resolution).toBe('sole_register');
    expect(dto.registerStatus).toBe('Open');
    expect(dto.autoOpened).toBe(true);
    expect(dto.nextAction).toBe('ready');
    expect(dto.messageCode).toBe('CASH_REGISTER_AUTO_OPENED');
  });

  it('reads PascalCase fields', () => {
    const dto = parsePosCashRegisterContextDto({
      EffectiveRegisterId: 'b2c3d4e5-f6a7-8901-bcde-f12345678901',
      Resolution: 'settings',
      RegisterStatus: 'Closed',
      AutoOpened: false,
      NextAction: 'open_register',
      MessageCode: 'CASH_REGISTER_CLOSED',
    });
    expect(dto.effectiveRegisterId).toContain('b2c3');
    expect(dto.resolution).toBe('settings');
    expect(dto.nextAction).toBe('open_register');
    expect(dto.messageCode).toBe('CASH_REGISTER_CLOSED');
  });
});
