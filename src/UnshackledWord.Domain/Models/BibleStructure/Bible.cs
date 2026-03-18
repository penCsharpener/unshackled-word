namespace UnshackledWord.Domain.Models.BibleStructure;

public static class Bible
{
    public static ICollection<BibleBook> GetEntireBible()
    {
        var books = new List<BibleBook>();
        foreach (var book in BibleBook.AllBooks.Values)
        {
            var chapters = BibleChapter.GetChapters().Where(x => x.BibleBookId == book.Id).ToList();

            foreach (var chapter in chapters)
            {
                if (chapter.ChapterHeb is not null)
                {
                    foreach (var v in Enumerable.Range(1, chapter.VersesHeb))
                    {
                        chapter.HebVerses.Add(new BibleVerse { Book = book, Chapter = chapter, Verse = v });
                    }
                    book.HebChapters.Add(chapter);
                }

                if (chapter.ChapterLxx is not null)
                {
                    foreach (var v in Enumerable.Range(1, chapter.VersesLxx))
                    {
                        chapter.LxxVerses.Add(new BibleVerse { Book = book, Chapter = chapter, Verse = v });
                    }
                    book.LxxChapters.Add(chapter);
                }
            }

            books.Add(book);
        }

        return books;
    }
}
