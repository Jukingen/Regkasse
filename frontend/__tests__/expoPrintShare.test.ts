import { escapeHtml } from '../services/receiptFormatter';
import {
  PrintCancelledError,
  ShareUnavailableError,
  isPrintCancelled,
  isPrintDialogCancelled,
  isShareUnavailable,
} from '../utils/expoPrintShare';

describe('expoPrintShare helpers', () => {
  it('detects iOS print dialog cancel messages', () => {
    expect(isPrintDialogCancelled(new Error('Printing did not complete'))).toBe(true);
    expect(isPrintDialogCancelled(new Error('User cancelled print'))).toBe(true);
    expect(isPrintDialogCancelled(new PrintCancelledError())).toBe(true);
    expect(isPrintDialogCancelled(new Error('Printer not available'))).toBe(false);
    expect(isPrintCancelled(new Error('Network offline'))).toBe(false);
  });

  it('identifies share unavailable', () => {
    expect(isShareUnavailable(new ShareUnavailableError())).toBe(true);
    expect(isShareUnavailable(new Error('boom'))).toBe(false);
  });
});

describe('receipt HTML escape', () => {
  it('escapes markup in product names', () => {
    expect(escapeHtml(`Cafe <script>alert(1)</script> & "Bar"`)).toBe(
      'Cafe &lt;script&gt;alert(1)&lt;/script&gt; &amp; &quot;Bar&quot;'
    );
  });
});
