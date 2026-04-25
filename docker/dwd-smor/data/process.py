import csv
import sys
import spacy

def get_linguistic_data(token):
    """Extracts data from a spaCy token into a dictionary."""
    morph = token.morph.to_dict()
    return {
        "lemma": token.lemma_,
        "part_of_speech": token.pos_,
        "morphology": str(token.morph),
        "degree": morph.get("Degree", ""),
        "nonfinite": morph.get("VerbForm", ""),
        "category": token.tag_,
        "tense": morph.get("Tense", ""),
        "person": morph.get("Person", ""),
        "number": morph.get("Number", ""),
        "mood": morph.get("Mood", ""),
        "case": morph.get("Case", ""),
        "gender": morph.get("Gender", "")
    }

def process_stream():
    # Load model and disable components not needed for lemmatization/morphology
    try:
        nlp = spacy.load("de_core_news_lg", disable=["parser", "ner"])
    except OSError:
        sys.stderr.write("ERROR: Model not found. Run: python -m spacy download de_core_news_lg\n")
        return

    reader = csv.DictReader(sys.stdin, delimiter="\t")
    fieldnames = (reader.fieldnames or []) + [
        "lemma", "part_of_speech", "morphology", "degree", "nonfinite", 
        "category", "tense", "person", "number", 
        "mood", "case", "gender"
    ]
    
    writer = csv.DictWriter(sys.stdout, delimiter="\t", fieldnames=fieldnames, quoting=csv.QUOTE_MINIMAL)
    writer.writeheader()

    # Batching parameters
    BATCH_SIZE = 20000
    rows_buffer = []

    def flush_buffer(buffer):
        """Processes a batch of rows and writes them."""
        # Extract only the words to process
        words = [row.get("PlainWord", "").strip() or " " for row in buffer]
        
        # nlp.pipe returns a generator of Docs
        # n_process=-1 uses all available CPU cores (Linux/macOS)
        for doc, row in zip(nlp.pipe(words, batch_size=BATCH_SIZE), buffer):
            if len(doc) > 0:
                row.update(get_linguistic_data(doc[0]))
            writer.writerow(row)

    for row in reader:
        rows_buffer.append(row)
        
        if len(rows_buffer) >= BATCH_SIZE:
            flush_buffer(rows_buffer)
            rows_buffer = []

    # Process remaining rows
    if rows_buffer:
        flush_buffer(rows_buffer)

if __name__ == "__main__":
    try:
        process_stream()
    except BrokenPipeError:
        sys.stderr.close()
