import { describe, expect, it } from '@jest/globals';

import { resolvePosPermissions } from '../utils/posPermissions';

const cashierClaims = [
  'payment.take',
  'shift.open',
  'shift.close',
  'order.view',
  'order.create',
  'rksv.startbeleg.create',
  'rksv.monatsbeleg.create',
  'rksv.jahresbeleg.create',
];

const waiterClaims = [
  'payment.take',
  'payment.view',
  'shift.view',
  'shift.close',
  'order.view',
  'order.create',
];

const kitchenClaims = ['order.view', 'order.update', 'kitchen.view', 'kitchen.update'];

describe('resolvePosPermissions', () => {
  it('denies every flag when there is no user', () => {
    expect(resolvePosPermissions(null)).toEqual({
      isCashier: false,
      isWaiter: false,
      canMakePayment: false,
      canOpenShift: false,
      canCloseShift: false,
      canViewOrders: false,
      canTakeOrders: false,
      canCreateSonderbeleg: false,
    });
  });

  it('grants every operation flag for SuperAdmin even with an empty compact JWT', () => {
    expect(resolvePosPermissions({ role: 'SuperAdmin', permissions: [] })).toEqual({
      isCashier: false,
      isWaiter: false,
      canMakePayment: true,
      canOpenShift: true,
      canCloseShift: true,
      canViewOrders: true,
      canTakeOrders: true,
      canCreateSonderbeleg: true,
    });
  });

  it('grants every flag when SuperAdmin is only in roles[]', () => {
    const flags = resolvePosPermissions({
      role: 'Cashier',
      roles: ['SuperAdmin'],
      permissions: [],
    });
    expect(flags.isCashier).toBe(true);
    expect(flags.canMakePayment).toBe(true);
    expect(flags.canCreateSonderbeleg).toBe(true);
  });

  it('grants every flag for compact SuperAdmin JWT (system.critical only)', () => {
    const flags = resolvePosPermissions({
      role: 'Manager',
      permissions: ['system.critical'],
    });
    expect(flags.canMakePayment).toBe(true);
    expect(flags.canOpenShift).toBe(true);
    expect(flags.canCreateSonderbeleg).toBe(true);
  });

  it('maps typical Cashier claims to payment, shift, orders, and Sonderbeleg', () => {
    expect(resolvePosPermissions({ role: 'Cashier', permissions: cashierClaims })).toEqual({
      isCashier: true,
      isWaiter: false,
      canMakePayment: true,
      canOpenShift: true,
      canCloseShift: true,
      canViewOrders: true,
      canTakeOrders: true,
      canCreateSonderbeleg: true,
    });
  });

  it('Waiter may take/view orders but cannot pay or open/close shift even with payment.take', () => {
    expect(resolvePosPermissions({ role: 'Waiter', permissions: waiterClaims })).toEqual({
      isCashier: false,
      isWaiter: true,
      canMakePayment: false,
      canOpenShift: false,
      canCloseShift: false,
      canViewOrders: true,
      canTakeOrders: true,
      canCreateSonderbeleg: false,
    });
  });

  it('does not grant POS floor flags to Kitchen even with order.view', () => {
    expect(resolvePosPermissions({ role: 'Kitchen', permissions: kitchenClaims })).toEqual({
      isCashier: false,
      isWaiter: false,
      canMakePayment: false,
      canOpenShift: false,
      canCloseShift: false,
      canViewOrders: false,
      canTakeOrders: false,
      canCreateSonderbeleg: false,
    });
  });

  it('denies payment when payment.take is missing even if the role is Cashier', () => {
    const flags = resolvePosPermissions({
      role: 'Cashier',
      permissions: ['order.view', 'shift.open'],
    });
    expect(flags.canMakePayment).toBe(false);
    expect(flags.canOpenShift).toBe(true);
  });

  it('matches permission claims case-insensitively', () => {
    const flags = resolvePosPermissions({
      role: 'Cashier',
      permissions: ['Payment.Take', 'RKSV.Monatsbeleg.Create'],
    });
    expect(flags.canMakePayment).toBe(true);
    expect(flags.canCreateSonderbeleg).toBe(true);
  });

  it('treats any rksv.*.create claim as Sonderbeleg create', () => {
    expect(
      resolvePosPermissions({
        permissions: ['rksv.nullbeleg.create'],
      }).canCreateSonderbeleg
    ).toBe(true);
    expect(
      resolvePosPermissions({
        permissions: ['rksv.schlussbeleg.create'],
      }).canCreateSonderbeleg
    ).toBe(true);
    expect(
      resolvePosPermissions({
        permissions: ['rksv.monatsbeleg.view'],
      }).canCreateSonderbeleg
    ).toBe(false);
  });
});
