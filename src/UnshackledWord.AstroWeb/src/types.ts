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

export enum PartOfSpeech {
  Adjective = 0,
  Adverb = 1,
  Article = 2,
  Conjunction = 3,
  Indeclinable = 4,
  Interjection = 5,
  Noun = 6,
  NounProperPerson = 7,
  NounProperPlace = 8,
  Particle = 9,
  Preposition = 10,
  Pronoun = 11,
  Verb = 12,
}
