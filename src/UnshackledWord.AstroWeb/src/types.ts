export interface SearchResult {
  id: string;
  title: string;
  description: string;
}

export interface WordResponse {
  id: number;
  positionInVerse: number;
  verse: number;
  wordInContext: string;
  plainWord: string;
  lemma: string;
  strongs: string;
}