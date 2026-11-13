'use client';

import { useParams } from 'next/navigation';

import { AdminPageHeader } from '@/components/admin-layout/AdminPageHeader';
import { AdminPageShell } from '@/components/admin-layout/AdminPageShell';
import { RoomDetail } from '@/features/lodging/RoomDetail';
import { useI18n } from '@/i18n';

export default function AdminRoomDetailPage() {
  const { t } = useI18n();
  const params = useParams<{ id: string }>();
  const roomId = typeof params.id === 'string' ? params.id : '';
  return (
    <AdminPageShell>
      <AdminPageHeader title={t('admin.rooms.detailTitle')} subtitle={t('admin.rooms.subtitle')} />
      <RoomDetail roomId={roomId} />
    </AdminPageShell>
  );
}
