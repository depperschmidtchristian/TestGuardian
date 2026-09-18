# Entscheidungen: feature/multi-file-input

Ergänzt `docs/plans/multi-file-input.md` um Details, die erst bei der Umsetzung konkret wurden.

## `System.Progress<T>` bewusst NICHT in den Tests verwendet

`System.Progress<T>` marshalt `Report`-Aufrufe über den zum Konstruktionszeitpunkt aktiven `SynchronizationContext` — ist keiner gesetzt (der Normalfall in einem Testlauf ohne UI/ASP.NET-Kontext), landet der Callback stattdessen asynchron auf dem ThreadPool. Für Tests, die direkt nach `ReadAll(...)` die gesammelten Reports prüfen wollen, ist das ein Race Condition: der Callback könnte noch gar nicht gelaufen sein. Die Tests verwenden deshalb eine eigene, minimale `SynchronousProgress<T>`-Testklasse, die `IProgress<T>` direkt implementiert und synchron auf dem aufrufenden Thread berichtet. Die Produktionslogik selbst (`TrxBatchReader.ReadAll`) bleibt unverändert kompatibel mit echtem `System.Progress<T>` — das betrifft nur, wie die Tests den Fortschritt beobachten.

## `StringComparer.OrdinalIgnoreCase` für Pfad-Deduplizierung und -Sortierung

Anders als Assembly-Namen (siehe `feature/trx-aggregation-summary`, dort `Ordinal`) sind Dateipfade auf Windows faktisch case-insensitive — derselbe Pfad in unterschiedlicher Groß-/Kleinschreibung (`C:\Test.trx` vs. `c:\test.trx`) soll als dieselbe Datei erkannt werden, nicht als zwei verschiedene. Deshalb hier bewusst `OrdinalIgnoreCase` statt `Ordinal`.

## Suchmuster-Auflösung ohne Verzeichnisteil

Ein Suchmuster wie `lauf-*.trx` ohne Verzeichnisangabe wird gegen `"."` (aktuelles Arbeitsverzeichnis) aufgelöst — `Path.GetDirectoryName("lauf-*.trx")` liefert einen leeren String, kein `null`, das musste explizit abgefangen werden.

## `Directory.GetFiles` mit ungültigem Suchmuster

Enthält ein Suchmuster ungültige Zeichen für `Directory.GetFiles`, wirft die Methode eine `ArgumentException`. Das wird abgefangen und als `UnresolvedInput` mit verständlicher Meldung ausgewiesen, statt das ganze Programm mit einer rohen Exception abstürzen zu lassen — konsistent mit dem Grundsatz aus Feature 1 (Teil A, Pflicht 5), unerwartete Eingaben sichtbar statt destruktiv zu behandeln.

## Build vs. Testlauf

Wie bei den vorherigen Features: `dotnet build` lief erfolgreich (0 Fehler, 0 Warnungen). `dotnet test` wurde bewusst nicht ausgeführt.
