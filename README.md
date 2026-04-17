# unshackled-word

This project pursue the goal to provide all the necessary data to study the Bible in the original languages - primarily in German.
It was born out of the need to have a completely free data without any restrictions to copyright and licencing.
Of course there are already great Bible study tools and translations out there but most of the time there are strings attached.
In the spirit of thedoreanprinciple.org this project wants to go a different way.

## What does this repo provide?

1. A source for Bible data in German, Greek and Hebrew
   * the project `UnshackledWord.Tooling.SeedDb` downloads a variety of data sources that use open licenses and imports them into a postgres database, ie.:
     * [x] Step Bible Data (extensive Greek and Hebrew data)
     * [x] Treasury Of Scripture Knowledge
     * [x] Statistical Restoration Greek New Testament (SR)
     * [x] SBL Greek New Testament and Apparatus
     * [x] Elberfelder 1871 translation exported from TheWord BibleSoftware
     * [ ] LXX data
   * the `UnshackledWord.Tooling.WebApi` provides a way to query data imported data for studying and further enhancements
2. A fully AI tagged Elberfelder 1871
   * [x] with the help of Google Gemini AI the Elb 1871 was fully tagged with the Step Bible Data
   * this does not mean this tagged Bible is done but it's a start that must be improved upon
   * [ ] Hebrew OT tagged with the Greek LXX OT
3. Web frontend to study the Elberfelder 1871
   * [ ] for correcting and improving the tagging initially done with AI
   * [ ] for enhancing the data
   * [ ] studying the Scriptures (the baseline goal is something like the csv-bible.de)
   * [ ] creating a new, modern, more precise, copyright free Elberfelder translation for the 21. century
4. Vocab trainers for Greek and Hebrew
   * flash card vocab training of the original languages (baseline is cerego.com-like UX)
   * unlike Anki, the user should not need to grade his own learning success but an algorithm is doing that automatically
5. Enhancements to the Bible data akin to Logos Bible software
   * [ ] tagging for which person spoke which words of the biblical text
   * [ ] tagging for theologically themes
   * [ ] tagging for biblical concepts, metaphors and symbols

## Docker Setup

If you want to give this project a go and take a look at the data create a `.env` file in the root of the solution.
Add these lines if you leave everything else as default:

```dotenv
POSTGRES_CONNECTIONSTRING="Host=unshackledword.tooling.database;Port=5432;Database=postgres;Username=postgres;Password=test;"
ENVIRONMENT=Development
```

Then run `docker compose up -d` in the solution root.
If this doesn't work for you please create an issue with the details.

### BibleTagger

A basic .NET Blazor frontend.

```sh
dotnet tool install --global dotnet-ef
```
### Migrations

Create new EF migration in the right folder. Run this in `src\UnshackledWord.Tooling.BibleTagger`

```sh
dotnet ef migrations add InitialCreate --output-dir Data/Migrations
```

Optional flags can be used to only output specific migrations

```sh
--from NameOfBeginngingMigration --to NameOfAnotherMigration
```

Output EF Migration as output script so add manually to migration worker

```sh
dotnet ef migrations script --idempotent -o ..\UnshackledWord.Tooling.Postgres.Migration\scripts\OutputScript.ignore.sql
```
To migrate database directly within BibleTagger

```sh
dotnet ef database update
```

### Managing JS/CSS libraries

```sh
dotnet tool install -g Microsoft.Web.LibraryManager.Cli
```

```sh
cd src\UnshackledWord.Tooling.BibleTagger\wwwroot
# libman init
libman update bootstrap
```
