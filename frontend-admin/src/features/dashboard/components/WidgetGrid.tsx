'use client';

import React from 'react';
import {
    DndContext,
    closestCenter,
    KeyboardSensor,
    PointerSensor,
    useSensor,
    useSensors,
    type DragEndEvent,
} from '@dnd-kit/core';
import {
    SortableContext,
    sortableKeyboardCoordinates,
    useSortable,
    rectSortingStrategy,
} from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import type { DashboardWidgetPreference } from '@/features/dashboard/types';
import { renderDashboardWidget } from '@/features/dashboard/widgets/widgetRegistry';

type WidgetGridProps = {
    widgets: DashboardWidgetPreference[];
    titleById: Map<string, string>;
    onReorder: (widgets: DashboardWidgetPreference[]) => void;
    onWidgetSettingsChange: (widgetId: string, settings: Record<string, unknown>) => void;
};

function SortableWidgetItem({
    item,
    title,
    onSettingsChange,
}: {
    item: DashboardWidgetPreference;
    title: string;
    onSettingsChange: (settings: Record<string, unknown>) => void;
}) {
    const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({
        id: item.widgetId,
    });

    const style: React.CSSProperties = {
        transform: CSS.Transform.toString(transform),
        transition,
        opacity: isDragging ? 0.85 : 1,
        zIndex: isDragging ? 1 : 0,
    };

    return (
        <div ref={setNodeRef} style={style} data-widget-id={item.widgetId}>
            {renderDashboardWidget(item.widgetId, {
                title,
                dragHandleProps: { ...attributes, ...listeners },
                settings: item.settings,
                onSettingsChange,
            })}
        </div>
    );
}

/** Drag-and-drop grid of visible dashboard widgets. */
export function WidgetGrid({
    widgets,
    titleById,
    onReorder,
    onWidgetSettingsChange,
}: WidgetGridProps) {
    const visible = widgets.filter((w) => w.isVisible).sort((a, b) => a.order - b.order);
    const ids = visible.map((w) => w.widgetId);

    const sensors = useSensors(
        useSensor(PointerSensor, { activationConstraint: { distance: 6 } }),
        useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
    );

    const handleDragEnd = (event: DragEndEvent) => {
        const { active, over } = event;
        if (!over || active.id === over.id) return;

        const oldIndex = ids.indexOf(String(active.id));
        const newIndex = ids.indexOf(String(over.id));
        if (oldIndex < 0 || newIndex < 0) return;

        const reorderedVisible = [...visible];
        const [moved] = reorderedVisible.splice(oldIndex, 1);
        reorderedVisible.splice(newIndex, 0, moved);

        const hidden = widgets.filter((w) => !w.isVisible).sort((a, b) => a.order - b.order);
        const merged = [...reorderedVisible, ...hidden].map((w, index) => ({ ...w, order: index }));
        onReorder(merged);
    };

    if (visible.length === 0) {
        return null;
    }

    return (
        <DndContext sensors={sensors} collisionDetection={closestCenter} onDragEnd={handleDragEnd}>
            <SortableContext items={ids} strategy={rectSortingStrategy}>
                <div
                    style={{
                        display: 'grid',
                        gridTemplateColumns: 'repeat(auto-fill, minmax(min(100%, 420px), 1fr))',
                        gap: 16,
                        marginBottom: 24,
                    }}
                >
                    {visible.map((item) => (
                        <SortableWidgetItem
                            key={item.widgetId}
                            item={item}
                            title={titleById.get(item.widgetId) ?? item.widgetId}
                            onSettingsChange={(settings) => onWidgetSettingsChange(item.widgetId, settings)}
                        />
                    ))}
                </div>
            </SortableContext>
        </DndContext>
    );
}
