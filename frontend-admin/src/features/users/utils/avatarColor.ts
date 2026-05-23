/** Deterministic avatar background from display name (stable per user). */
export function getColorFromName(name: string): string {
    const trimmed = name.trim();
    if (!trimmed) return '#8c8c8c';
    let hash = 0;
    for (let i = 0; i < trimmed.length; i += 1) {
        hash = trimmed.charCodeAt(i) + ((hash << 5) - hash);
    }
    const hue = Math.abs(hash) % 360;
    return `hsl(${hue}, 55%, 45%)`;
}
