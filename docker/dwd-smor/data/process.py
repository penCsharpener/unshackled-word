import csv
import sys
import dwdsmor

def process_stream():
    # Initialize the lemmatizer
    lemmatizer = dwdsmor.lemmatizer()

    # Process stdin/stdout as TSV
    reader = csv.DictReader(sys.stdin, delimiter="\t")

    # Prepare the output with the requested columns
    fieldnames = reader.fieldnames + ["lemma", "part_of_speech", "morphology"]
    writer = csv.DictWriter(
        sys.stdout, delimiter="\t", fieldnames=fieldnames, quoting=csv.QUOTE_MINIMAL
    )

    writer.writeheader()

    for row in reader:
        word = row.get("PlainWord", "")

        try:
            # lemmatizer returns a Traversal object
            traversal = lemmatizer(str(word))

            # Use getattr to safely retrieve attributes from the analysis result
            row["lemma"] = getattr(traversal.analysis, "lemma", "")
            row["part_of_speech"] = getattr(traversal.analysis, "pos", "")
            row["morphology"] = getattr(traversal.analysis, "analysis", "")
            
        except StopIteration:
            # This is common for names/punctuation; logging as info
            sys.stderr.write(f"INFO: No analysis found for '{word}'\n")
            row["lemma"], row["part_of_speech"], row["morphology"] = "", "", ""
            
        except Exception as e:
            # This catches unexpected crashes
            sys.stderr.write(
                f"ERROR: Failed to process '{word}'. Reason: {type(e).__name__}: {e}\n"
            )
            row["lemma"], row["part_of_speech"], row["morphology"] = "", "", ""

        writer.writerow(row)

if __name__ == "__main__":
    try:
        process_stream()
    except BrokenPipeError:
        # Prevents error messages when piping output to tools like 'head'
        sys.stderr.close()
