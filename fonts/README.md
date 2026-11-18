# Optional fonts

Not distributed with this repo (licence). To use the SBL fonts, download them from
<https://www.sbl-site.org/resources/fonts/> (after accepting their licence) and put the TTF files here:

    fonts/sbl/SBL_BibLit.ttf

This folder is bind-mounted into the web container (dev: `/app/public/fonts`, prod nginx: `/srv/fonts`) and served
at `/fonts/...`. Without the files the app falls back to Noto Serif Hebrew / system serif. Rename the file to match,
or adjust the `@font-face` in `src/UnshackledWord.Web/src/styles/index.css`.
