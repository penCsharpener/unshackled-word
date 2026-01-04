# unshackled-word

## BibleTagger

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
