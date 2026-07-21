/**
 * POS camera barcode presets for expo-camera `CameraView`.
 * Prefer narrow type lists — fewer symbologies = less CPU and fewer false positives.
 *
 * Native scanning uses the platform detector inside expo-camera (not a separate
 * `barcode-detector` npm package). Web uses expo-camera’s web barcode path.
 */

/** Customer QR + Gutschein QR payloads. */
export const POS_QR_BARCODE_TYPES = ['qr'] as const;

/** Generic POS scanner (product EAN etc. when needed). */
export const POS_PRODUCT_BARCODE_TYPES = ['qr', 'ean13', 'ean8', 'code128', 'code39'] as const;
