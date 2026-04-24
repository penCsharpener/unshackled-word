FROM python:3.12-slim

# Install system dependencies for SFST and DWDSmor
RUN apt-get update && apt-get install -y default-jdk libsaxonhe-java sfst && rm -rf /var/lib/apt/lists/*

# Install the Open Edition of DWDSmor
RUN pip install dwdsmor

WORKDIR /app
# COPY data/process.py .

# cat elberfelder_raw.tsv | docker run -i elberfelder-morph > elberfelder_analyzed.tsv
