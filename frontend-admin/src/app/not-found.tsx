import Link from 'next/link';
import { Button, Result } from 'antd';

export default function NotFound() {
    return (
        <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100vh' }}>
            <Result
                status="404"
                title="404"
                subTitle="Die angeforderte Seite existiert nicht."
                extra={
                    <Link href="/dashboard">
                        <Button type="primary">Zur Übersicht</Button>
                    </Link>
                }
            />
        </div>
    );
}
