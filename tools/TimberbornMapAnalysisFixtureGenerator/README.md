# Timberborn Map Analysis Fixture Generator

Creates compressed, focused regression fixtures from local `.timber` archives. The fixtures retain only the decoded
inputs needed by the exact water and forest classifiers; complete Workshop payloads are not copied into the test
corpus.

Run from the repository root:

```powershell
dotnet run --project tools/TimberbornMapAnalysisFixtureGenerator/TimberbornMapAnalysisFixtureGenerator.csproj -- `
  --water MAP.timber OUTPUT.json.gz WORKSHOP_ID

dotnet run --project tools/TimberbornMapAnalysisFixtureGenerator/TimberbornMapAnalysisFixtureGenerator.csproj -- `
  --forest MAP.timber OUTPUT.json.gz WORKSHOP_ID
```
