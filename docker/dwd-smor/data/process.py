import csv
import sys
import dwdsmor

def clear_linguistic_data(data_row):
    keys = ["lemma", "part_of_speech", "morphology", "degree", "nonfinite", 
            "function", "category", "tense", "person", "number", 
            "mood", "case", "gender"]
    data_row.update(dict.fromkeys(keys, ""))
    return data_row


def process_stream():
    # Initialize the lemmatizer
    lemmatizer = dwdsmor.lemmatizer()

    # Process stdin/stdout as TSV
    reader = csv.DictReader(sys.stdin, delimiter="\t")

    # Prepare the output with the requested columns
    fieldnames = reader.fieldnames + ["lemma", "part_of_speech", "morphology", "degree", "nonfinite", "function" ,"category", "tense", "person", "number", "mood", "case", "gender"]
    writer = csv.DictWriter(
        sys.stdout, delimiter="\t", fieldnames=fieldnames, quoting=csv.QUOTE_MINIMAL
    )

    writer.writeheader()

    # enumerate starts at 1 to count rows easily
    for i, row in enumerate(reader, 1):
        # ABORT after 10 lines for testing
        if i > 100:
            sys.stderr.write("DEBUG: reached 10 line limit. Aborting.\n")
            break

        word = row.get("PlainWord", "")

        try:
            # lemmatizer returns a Traversal object
            trav = lemmatizer(str(word))

            if trav is None:
                trav = lemmatizer(str(word), pos={"N"})

            if trav:
                # Use getattr to safely retrieve attributes from the analysis result
                row["lemma"] = trav.analysis
                row["part_of_speech"] = trav.pos
                row["degree"] = trav.degree
                row["nonfinite"] = trav.nonfinite
                row["function"] = trav.function
                row["category"] = trav.category
                row["tense"] = trav.tense
                row["person"] = trav.person
                row["number"] = trav.number
                row["mood"] = trav.mood
                row["case"] = trav.case
                row["gender"] = trav.gender
            else:
                clear_linguistic_data(row)
            
        except StopIteration:
            sys.stderr.write(f"INFO: No analysis found for '{word}'\n")
            clear_linguistic_data(row)
            
        except Exception as e:
            sys.stderr.write(
                f"ERROR: Failed to process '{word}'. Reason: {type(e).__name__}: {e}\n"
            )
            clear_linguistic_data(row)

        writer.writerow(row)

if __name__ == "__main__":
    try:
        process_stream()
    except BrokenPipeError:
        sys.stderr.close()
