FROM python:3.12-slim

# Install system dependencies for SFST and DWDSmor
RUN apt-get update && apt-get install -y default-jdk libsaxonhe-java sfst && rm -rf /var/lib/apt/lists/*

# Install spaCy with large model
RUN pip install --upgrade pip
RUN pip install spacy
RUN python -m spacy download de_core_news_lg

WORKDIR /app
# COPY data/process.py .

# cat elberfelder_raw.tsv | docker run -i elberfelder-morph > elberfelder_analyzed.tsv
