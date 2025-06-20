export type Mapping = {
  fromId: number;
  toId: number;
};

export type Word = {
  id: number;
  text: string;
};

export type SourceWord = {
  id: number;
  word: string;
  strongs: string;
};

export type Verse = {
  Translation1: Word[];
  Translation2: Word[];
  SourceLanguage: SourceWord[];
};

export type BibleRef = {
    BookId: number;
    ChapterId: number;
    VerseId: number;
}