/**
 * Narrow unknown caught errors without widening types to `any`.
 * Does not assume all errors are Axios-shaped.
 */

export function getAxiosResponseStatus(err: unknown): number | undefined {
    if (typeof err !== 'object' || err === null) {
        return undefined;
    }
    if (!Object.prototype.hasOwnProperty.call(err, 'response')) {
        return undefined;
    }
    const resp = Reflect.get(err, 'response');
    if (typeof resp !== 'object' || resp === null) {
        return undefined;
    }
    if (!Object.prototype.hasOwnProperty.call(resp, 'status')) {
        return undefined;
    }
    const status = Reflect.get(resp, 'status');
    return typeof status === 'number' ? status : undefined;
}

export function getAxiosResponseDataString(err: unknown): string | undefined {
    if (typeof err !== 'object' || err === null) {
        return undefined;
    }
    if (!Object.prototype.hasOwnProperty.call(err, 'response')) {
        return undefined;
    }
    const resp = Reflect.get(err, 'response');
    if (typeof resp !== 'object' || resp === null) {
        return undefined;
    }
    if (!Object.prototype.hasOwnProperty.call(resp, 'data')) {
        return undefined;
    }
    const data = Reflect.get(resp, 'data');
    if (typeof data === 'string') {
        return data;
    }
    if (typeof data === 'object' && data !== null && Object.prototype.hasOwnProperty.call(data, 'message')) {
        const message = Reflect.get(data, 'message');
        if (typeof message === 'string') {
            return message;
        }
    }
    return undefined;
}

/** Ant Design Form `validateFields` rejection shape. */
export function isAntdFormValidateError(err: unknown): boolean {
    return typeof err === 'object' && err !== null && 'errorFields' in err;
}
