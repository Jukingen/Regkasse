import React, { useMemo, useRef, useState, useEffect } from 'react';
import { Select, Spin } from 'antd';
import type { SelectProps } from 'antd/es/select';

export interface DebounceSelectProps<ValueType = any>
    extends Omit<SelectProps<ValueType | ValueType[]>, 'options' | 'children'> {
    fetchOptions: (search: string) => Promise<ValueType[]>;
    debounceTimeout?: number;
    defaultOptions?: ValueType[];
}

// Simple debounce implementation
function debounce<T extends (...args: any[]) => any>(func: T, wait: number) {
    let timeout: NodeJS.Timeout | null = null;
    return (...args: Parameters<T>) => {
        if (timeout) clearTimeout(timeout);
        timeout = setTimeout(() => {
            func(...args);
        }, wait);
    };
}

function DebounceSelect<
    ValueType extends { key?: string; label: React.ReactNode; value: string | number } = any,
>({ fetchOptions, debounceTimeout = 800, defaultOptions = [], ...props }: DebounceSelectProps<ValueType>) {
    const [fetching, setFetching] = useState(false);
    const [options, setOptions] = useState<ValueType[]>([]);

    // We track if a search is active to decide whether to show search results or default options
    // Initial state logic: if we have defaults, we are not searching.
    const [isSearching, setIsSearching] = useState(false);
    const fetchRef = useRef(0);

    const debounceFetcher = useMemo(() => {
        const loadOptions = (value: string) => {
            fetchRef.current += 1;
            const fetchId = fetchRef.current;

            // If empty value, stop searching and revert to defaults
            if (!value) {
                setOptions([]);
                setIsSearching(false);
                setFetching(false);
                return;
            }

            setOptions([]);
            setFetching(true);
            setIsSearching(true);

            fetchOptions(value).then((newOptions) => {
                if (fetchId !== fetchRef.current) {
                    return;
                }
                setOptions(newOptions);
                setFetching(false);
            });
        };

        return debounce(loadOptions, debounceTimeout);
    }, [fetchOptions, debounceTimeout]);

    // Derived options to display
    const displayOptions = isSearching ? options : defaultOptions;

    return (
        <Select
            labelInValue
            filterOption={false}
            onSearch={debounceFetcher}
            notFoundContent={fetching ? <Spin size="small" /> : null}
            {...props}
            options={displayOptions}
            // Ensure we clear search state on blur/deselect if needed, 
            // but usually onSearch with empty string handles the "clear" case during typing.
            onClear={() => {
                setIsSearching(false);
                setOptions([]);
            }}
        />
    );
}

export default DebounceSelect;
