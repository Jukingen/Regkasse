/**
 * Pipeline stepper: sunucu projeksiyonu tercih edilir; istemci türetimi yalnızca açıkça açıldığında (legacy).
 * NEXT_PUBLIC_* değişkenleri build zamanında gömülür.
 */
export function isBackupPipelineClientFallbackEnabled(): boolean {
  return process.env.NEXT_PUBLIC_BACKUP_PIPELINE_CLIENT_FALLBACK === 'true';
}
