import axios from "axios";
import { Verse, Mapping } from "@/types/verse";

const API_BASE = "http://localhost:7780/api/unshackled";

export const fetchVerse = async (bookId: number, chapterId: number, verseId: number): Promise<Verse> => {
  const res = await axios.get(`${API_BASE}/book/${bookId}/chapter/${chapterId}/verse/${verseId}`);
  return res.data;
};

export const saveMappings = async (
  bookId: number,
  chapterId: number,
  verseId: number,
  mappings: Mapping[]
): Promise<void> => {
  await axios.post(`${API_BASE}/book/${bookId}/chapter/${chapterId}/verse/${verseId}/mappings`, { mappings });
};
