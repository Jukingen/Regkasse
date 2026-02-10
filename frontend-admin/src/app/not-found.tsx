import Link from 'next/link';
import { Button, Result } from 'antd';

export default function NotFound() {
    return (
        <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100vh' }}>
            <Result
                status="404"
                title="404"
                subTitle="Sorry, the page you visited does not exist."
                extra={
                    <Link href="/dashboard">
                        <Button type="primary">Back to Dashboard</Button>
                    </Link>
                }
            />
        </div>
    );
}
