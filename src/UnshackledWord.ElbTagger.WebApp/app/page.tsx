"use client";

import { DndContext } from "@dnd-kit/core";
import { useEffect, useState } from "react";
import { DragDropWord } from "@/components/DragDropWord";
import { DropZone } from "@/components/DropZone";
import { fetchVerse, saveMappings } from "@/lib/api";
import { Mapping, Verse } from "@/types/verse";

export default function Home() {
  const [verse, setVerse] = useState<Verse | null>(null);
  const [mappings, setMappings] = useState<Mapping[]>([]);

  useEffect(() => {
    fetchVerse(40, 1, 1).then(setVerse);
  }, []);

  const handleDragEnd = (event: any) => {
    const { active, over } = event;
    if (over && active.id !== over.id) {
      setMappings((prev) => [
        ...prev,
        { fromId: active.id as number, toId: over.id as number },
      ]);
    }
  };

  const handleSave = () => {
    if (verse) saveMappings(40, 1, 1, mappings);
  };

  if (!verse) return <div>Loading...</div>;

  return (
    <DndContext onDragEnd={handleDragEnd}>
      <div className="p-6 space-y-6">
        <h1 className="text-2xl font-bold">Verse Mapping</h1>

        <div>
          <h2 className="text-xl">Translation 1</h2>
          <div className="flex flex-wrap gap-2">
            {verse.Translation1.map((word) => (
              <DragDropWord key={word.id} word={word} />
            ))}
          </div>
        </div>

        <div>
          <h2 className="text-xl">Translation 2</h2>
          <div className="flex flex-wrap gap-2">
            {verse.Translation2.map((word) => (
              <DropZone key={word.id} id={word.id} isOver={false}>
                <DragDropWord word={word} />
              </DropZone>
            ))}
          </div>
        </div>

        <div>
          <h2 className="text-xl">Original</h2>
          <div className="flex flex-wrap gap-2">
            {verse.SourceLanguage.map((word) => (
              <DropZone key={word.id} id={word.id} isOver={false}>
                {word.word}
              </DropZone>
            ))}
          </div>
        </div>

        <button
          onClick={handleSave}
          className="mt-4 px-4 py-2 bg-blue-600 text-white rounded"
        >
          Save Mappings
        </button>
      </div>
    </DndContext>
  );
}
