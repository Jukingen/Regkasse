'use client';

import Image, { type ImageProps } from 'next/image';

const DEFAULT_BLUR =
    'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==';

export type OptimizedImageProps = Omit<ImageProps, 'alt'> & {
    alt: string;
};

function isDataOrBlobSrc(src: ImageProps['src']): boolean {
    return typeof src === 'string' && (src.startsWith('data:') || src.startsWith('blob:'));
}

function isSvgSrc(src: ImageProps['src']): boolean {
    if (typeof src === 'string') {
        return src.toLowerCase().includes('.svg');
    }
    return false;
}

/**
 * Shared next/image wrapper: lazy load + blur placeholder by default.
 * Pass `priority` / `loading="eager"` for above-the-fold assets (e.g. header logo).
 */
export function OptimizedImage({
    src,
    alt,
    width,
    height,
    className,
    loading = 'lazy',
    placeholder = 'blur',
    blurDataURL = DEFAULT_BLUR,
    unoptimized,
    ...rest
}: OptimizedImageProps) {
    const skipBlur = isDataOrBlobSrc(src) || isSvgSrc(src);
    const forceUnoptimized = isDataOrBlobSrc(src) || isSvgSrc(src);

    return (
        <Image
            src={src}
            alt={alt}
            width={width}
            height={height}
            className={className}
            loading={loading}
            placeholder={skipBlur ? 'empty' : placeholder}
            blurDataURL={skipBlur ? undefined : blurDataURL}
            unoptimized={forceUnoptimized || unoptimized}
            {...rest}
        />
    );
}
