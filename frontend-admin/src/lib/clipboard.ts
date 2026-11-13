'use client';

function copyViaExecCommand(value: string): boolean {
  if (typeof document === 'undefined') {
    return false;
  }

  const textarea = document.createElement('textarea');
  textarea.value = value;
  textarea.setAttribute('readonly', '');
  // Keep the node in-viewport; off-screen / opacity-only nodes fail in some browsers.
  textarea.style.position = 'fixed';
  textarea.style.top = '0';
  textarea.style.left = '0';
  textarea.style.width = '1px';
  textarea.style.height = '1px';
  textarea.style.padding = '0';
  textarea.style.border = 'none';
  textarea.style.outline = 'none';
  textarea.style.boxShadow = 'none';
  textarea.style.background = 'transparent';
  textarea.style.opacity = '0';

  document.body.appendChild(textarea);
  textarea.focus({ preventScroll: true });
  textarea.select();
  textarea.setSelectionRange(0, value.length);

  try {
    return document.execCommand('copy');
  } catch {
    return false;
  } finally {
    textarea.remove();
  }
}

/**
 * Copy text to the clipboard. Prefers the async Clipboard API in secure contexts,
 * then falls back to `document.execCommand('copy')` for older / insecure contexts.
 */
export async function copyTextToClipboard(value: string): Promise<boolean> {
  if (!value) {
    return false;
  }

  const canUseClipboardApi =
    typeof window !== 'undefined' &&
    window.isSecureContext &&
    typeof navigator !== 'undefined' &&
    typeof navigator.clipboard?.writeText === 'function';

  if (canUseClipboardApi) {
    try {
      await navigator.clipboard.writeText(value);
      return true;
    } catch {
      // Fall through to the legacy path (permission denied, iframe policy, etc.).
    }
  }

  return copyViaExecCommand(value);
}
