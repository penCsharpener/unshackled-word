"use client";

import { useDraggable } from "@dnd-kit/core";

export function DragDropWord({ word }: { word: { id: number; text: string } }) {
  const { attributes, listeners, setNodeRef, transform } = useDraggable({
    id: word.id,
  });

  return (
    <div
      ref={setNodeRef}
      {...listeners}
      {...attributes}
      className="cursor-move p-2 bg-white border rounded shadow"
      style={{
        transform: transform
          ? `translate(${transform.x}px, ${transform.y}px)`
          : undefined,
      }}
    >
      {word.text}
    </div>
  );
}
