export type BackupSettings = {
  enabled: boolean;
  hour: string;
  keep: number;
  targetDirectory: string;
  lastAutoBackupDate?: string | null;
};

export type BackupFile = {
  fileName: string;
  sizeBytes: number;
  createdAt: string;
  productVersion: string;
  databaseSha256: string;
  archiveSha256: string;
  appliedMigrations: string[];
};

async function readError(response: Response, fallback: string) {
  const payload = await response.json().catch(() => null) as { message?: string } | null;
  return payload?.message || fallback;
}

export async function getBackupSettings(): Promise<BackupSettings> {
  const response = await fetch('/api/backups/settings');
  if (!response.ok) throw new Error(await readError(response, 'دریافت تنظیمات Backup انجام نشد'));
  return await response.json() as BackupSettings;
}

export async function saveBackupSettings(settings: BackupSettings): Promise<BackupSettings> {
  const response = await fetch('/api/backups/settings', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(settings),
  });
  if (!response.ok) throw new Error(await readError(response, 'ذخیره تنظیمات Backup انجام نشد'));
  return await response.json() as BackupSettings;
}

export async function listBackups(): Promise<BackupFile[]> {
  const response = await fetch('/api/backups');
  if (!response.ok) throw new Error(await readError(response, 'دریافت فهرست Backupها انجام نشد'));
  return await response.json() as BackupFile[];
}

export async function createBackup(): Promise<BackupFile> {
  const response = await fetch('/api/backups', { method: 'POST' });
  if (!response.ok) throw new Error(await readError(response, 'ساخت Backup انجام نشد'));
  return await response.json() as BackupFile;
}

export async function verifyBackup(fileName: string): Promise<BackupFile> {
  const response = await fetch('/api/backups/' + encodeURIComponent(fileName) + '/verify', { method: 'POST' });
  if (!response.ok) throw new Error(await readError(response, 'اعتبارسنجی Backup انجام نشد'));
  return await response.json() as BackupFile;
}

export async function prepareRestore(fileName: string) {
  const response = await fetch('/api/backups/' + encodeURIComponent(fileName) + '/restore', { method: 'POST' });
  if (!response.ok) throw new Error(await readError(response, 'آماده‌سازی بازیابی انجام نشد'));
  return await response.json() as { fileName: string; preparedAt: string; requiresRestart: boolean; message: string };
}

export function downloadBackupUrl(fileName: string) {
  return '/api/backups/' + encodeURIComponent(fileName) + '/download';
}
