"use client";

import { useDroppable } from "@dnd-kit/core";
import clsx from "clsx";

export function DropZone({
  id,
  children,
  isOver,
}: {
  id: number;
  children: React.ReactNode;
  isOver: boolean;
}) {
  const { setNodeRef, isOver: internalOver } = useDroppable({ id });

  return (
    <div
      ref={setNodeRef}
      className={clsx(
        "min-h-[3rem] p-2 border rounded transition",
        (isOver || internalOver) && "bg-blue-100 border-blue-400"
      )}
    >
      {children}
    </div>
  );
}
