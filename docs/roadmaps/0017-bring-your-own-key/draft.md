Roadmap 0016 implemented the AI usage using extra tooling. 
This roamap aims at allowing a user to add their own API key to be used in the UI. 
In the web UI like mapping editor or lemmatizer get a button: "Let AI do it" (just with a better name)

The api-key needs to be encrypted securely in the database to prevent missuse. 
It should be encrypted with hardware identifiers which don't change but change per machine so that we don't have a password in the config that could be leaked with the database. 

With the AI button the user can get the mapping done already and only serves as a reviewer. 
Code from the AI Worker should be reused. 