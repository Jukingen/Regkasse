type ConfirmableModal = {
  confirm: (config: {
    title: string;
    content: string;
    okText: string;
    cancelText: string;
    onOk: () => void | Promise<void>;
  }) => void;
};

/** One confirm before closing a till that another user currently holds. */
export function confirmForceCloseHeldByOther(
  modal: ConfirmableModal,
  t: (key: string, options?: Record<string, string | number>) => string,
  holderName: string,
  onOk: () => void | Promise<void>
): void {
  modal.confirm({
    title: t('cashRegisters.shift.forceCloseTitle'),
    content: t('cashRegisters.shift.forceCloseContent', { holder: holderName }),
    okText: t('cashRegisters.actions.closeRegister'),
    cancelText: t('common.buttons.cancel'),
    onOk,
  });
}
