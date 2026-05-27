import { AXIOS_INSTANCE } from '@/lib/axios';

export type DevOrphanedUserCleanupResult = {
    message: string;
    deletedMemberships: number;
    deletedUsers: number;
};

export async function cleanupOrphanedDevUsers(): Promise<DevOrphanedUserCleanupResult> {
    const { data } = await AXIOS_INSTANCE.post<DevOrphanedUserCleanupResult>(
        '/api/dev/cleanup/orphaned-users',
    );
    return data;
}
