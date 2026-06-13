'use client';

/**
 * Drop-in replacement for deprecated antd `List` (v6 deprecation, v7 removal).
 * Reuses antd list styles and Item/Meta while avoiding the deprecation warning.
 */
import React, { useContext, useMemo } from 'react';
import { clsx } from 'clsx';
import { ConfigContext } from 'antd/es/config-provider';
import { useComponentConfig } from 'antd/es/config-provider/context';
import DefaultRenderEmpty from 'antd/es/config-provider/defaultRenderEmpty';
import useSize from 'antd/es/config-provider/hooks/useSize';
import Spin from 'antd/es/spin';
import Item from 'antd/es/list/Item';
import { ListContext } from 'antd/es/list/context';
import useStyle from 'antd/es/list/style';

type ListSize = 'small' | 'default' | 'large';

export type SimpleListLocale = {
    emptyText?: React.ReactNode;
};

export type SimpleListProps<T = unknown> = Omit<
    React.HTMLAttributes<HTMLDivElement>,
    'children' | 'header'
> & {
    bordered?: boolean;
    split?: boolean;
    rootClassName?: string;
    children?: React.ReactNode;
    itemLayout?: 'horizontal' | 'vertical';
    dataSource?: readonly T[];
    size?: ListSize;
    header?: React.ReactNode;
    footer?: React.ReactNode;
    loading?: boolean | SpinProps;
    rowKey?: keyof T | ((item: T) => React.Key);
    renderItem?: (item: T, index: number) => React.ReactNode;
    locale?: SimpleListLocale;
};

type SpinProps = React.ComponentProps<typeof Spin>;

function isFunction(value: unknown): value is (...args: never[]) => unknown {
    return typeof value === 'function';
}

function SimpleListInner<T>({
    bordered = false,
    split = true,
    prefixCls: customizePrefixCls,
    className,
    rootClassName,
    style,
    children,
    itemLayout,
    dataSource = [],
    size: customizeSize,
    header,
    footer,
    loading = false,
    rowKey,
    renderItem,
    locale,
    ...rest
}: SimpleListProps<T> & { prefixCls?: string }) {
    const {
        getPrefixCls,
        direction,
        className: contextClassName,
        style: contextStyle,
    } = useComponentConfig('list');
    const { renderEmpty } = useContext(ConfigContext);

    const prefixCls = getPrefixCls('list', customizePrefixCls);
    const [hashId, cssVarCls] = useStyle(prefixCls);

    let loadingProp: SpinProps | undefined;
    if (typeof loading === 'boolean') {
        loadingProp = { spinning: loading };
    } else if (loading) {
        loadingProp = loading;
    }
    const isLoading = !!loadingProp?.spinning;

    const mergedSize = useSize(customizeSize);
    let sizeCls = '';
    switch (mergedSize) {
        case 'large':
            sizeCls = 'lg';
            break;
        case 'small':
            sizeCls = 'sm';
            break;
        default:
            break;
    }

    const classString = clsx(
        prefixCls,
        {
            [`${prefixCls}-vertical`]: itemLayout === 'vertical',
            [`${prefixCls}-${sizeCls}`]: sizeCls,
            [`${prefixCls}-split`]: split,
            [`${prefixCls}-bordered`]: bordered,
            [`${prefixCls}-loading`]: isLoading,
        },
        contextClassName,
        className,
        rootClassName,
        hashId,
        cssVarCls,
    );

    const renderInternalItem = (item: T, index: number) => {
        if (!renderItem) {
            return null;
        }

        let key: React.Key | undefined;
        if (isFunction(rowKey)) {
            key = rowKey(item);
        } else if (rowKey) {
            key = (item as Record<string, React.Key | undefined>)[rowKey as string];
        } else if (item && typeof item === 'object' && 'key' in item) {
            key = (item as { key?: React.Key }).key;
        }
        if (key == null) {
            key = `list-item-${index}`;
        }

        return <React.Fragment key={key}>{renderItem(item, index)}</React.Fragment>;
    };

    let childrenContent: React.ReactNode =
        isLoading ? (
            <div style={{ minHeight: 53 }} />
        ) : null;

    if (dataSource.length > 0) {
        childrenContent = (
            <ul className={clsx(`${prefixCls}-items`, `${prefixCls}-container`, cssVarCls)}>
                {dataSource.map(renderInternalItem)}
            </ul>
        );
    } else if (!children && !isLoading) {
        childrenContent = (
            <div className={`${prefixCls}-empty-text`}>
                {locale?.emptyText ?? renderEmpty?.('List') ?? (
                    <DefaultRenderEmpty componentName="List" />
                )}
            </div>
        );
    }

    const contextValue = useMemo(
        () => ({
            grid: undefined,
            itemLayout,
        }),
        [itemLayout],
    );

    return (
        <ListContext.Provider value={contextValue}>
            <div
                style={{ ...contextStyle, ...style }}
                className={classString}
                {...rest}
            >
                {header ? <div className={`${prefixCls}-header`}>{header}</div> : null}
                <Spin {...loadingProp}>{childrenContent}</Spin>
                {children}
                {footer ? <div className={`${prefixCls}-footer`}>{footer}</div> : null}
            </div>
        </ListContext.Provider>
    );
}

export function SimpleList<T = unknown>(props: SimpleListProps<T>) {
    return <SimpleListInner {...props} />;
}

SimpleList.Item = Item;
