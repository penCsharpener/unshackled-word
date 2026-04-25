import csv
import sys
import spacy

def clear_linguistic_data(data_row):
    keys = ["lemma", "part_of_speech", "morphology", "degree", "nonfinite", 
            "function", "category", "tense", "person", "number", 
            "mood", "case", "gender"]
    data_row.update(dict.fromkeys(keys, ""))
    return data_row

def process_stream():
    # Load German model
    # Note: Ensure you have run 'python -m spacy download de_core_news_sm'
    try:
        nlp = spacy.load("de_core_news_md")
    except OSError:
        sys.stderr.write("ERROR: German model not found. Run: python -m spacy download de_core_news_sm\n")
        return

    reader = csv.DictReader(sys.stdin, delimiter="\t")
    
    fieldnames = (reader.fieldnames or []) + [
        "lemma", "part_of_speech", "morphology", "degree", "nonfinite", 
        "function", "category", "tense", "person", "number", 
        "mood", "case", "gender"
    ]
    
    writer = csv.DictWriter(
        sys.stdout, delimiter="\t", fieldnames=fieldnames, quoting=csv.QUOTE_MINIMAL
    )
    writer.writeheader()

    for i, row in enumerate(reader, 1):
        if i > 10000:
            sys.stderr.write("DEBUG: reached 10000 line limit. Aborting.\n")
            break

        word = row.get("PlainWord", "").strip()

        if not word:
            writer.writerow(clear_linguistic_data(row))
            continue

        try:
            # Process the single word
            doc = nlp(word)
            
            if len(doc) > 0:
                token = doc[0]
                morph = token.morph.to_dict()

                row["lemma"] = token.lemma_
                row["part_of_speech"] = token.pos_
                row["morphology"] = str(token.morph)
                
                # Map spaCy morphology keys to your columns
                row["degree"] = morph.get("Degree", "")
                row["nonfinite"] = morph.get("VerbForm", "") # SpaCy uses VerbForm for non-finite info
                row["function"] = "" # SpaCy doesn't provide 'function' in the same way as dwdsmor
                row["category"] = token.tag_
                row["tense"] = morph.get("Tense", "")
                row["person"] = morph.get("Person", "")
                row["number"] = morph.get("Number", "")
                row["mood"] = morph.get("Mood", "")
                row["case"] = morph.get("Case", "")
                row["gender"] = morph.get("Gender", "")
            else:
                clear_linguistic_data(row)
            
        except Exception as e:
            sys.stderr.write(f"ERROR: Failed to process '{word}'. {type(e).__name__}: {e}\n")
            clear_linguistic_data(row)

        writer.writerow(row)

if __name__ == "__main__":
    try:
        process_stream()
    except BrokenPipeError:
        sys.stderr.close()
