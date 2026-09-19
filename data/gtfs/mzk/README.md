# MZK Bielsko-Biała — rozkłady PDF → odjazdy → PostGIS (PoC)

Proof of concept: pokazuje, że publiczne rozkłady przystankowe MZK (PDF) da się pobrać, sparsować i załadować do PostgreSQL/PostGIS. Plan i uzasadnienia: [docs/mzk-pipeline-implementation.md](../../../docs/mzk-pipeline-implementation.md) (sekcja 13 opisuje odstępstwa PoC od planu).

**Zakres:** linie 4, 7 i 16 oraz linie nocne N1 i N2 (po dwa kierunki każda, 10 PDF-ów). Bez GTFS/OTP, bez składania kursów i bez współrzędnych przystanków. Dane są wtórne, pochodzą z publicznych rozkładów MZK i są nieoficjalne.

## Uruchomienie (wszystko w kontenerach)

Wymagany tylko Docker. Z katalogu `data/gtfs/mzk/`:

```bash
export MZK_UID=$(id -u) MZK_GID=$(id -g)   # pliki w raw/ i parsed/ maja nalezec do Ciebie

docker compose run --rm pipeline node pipeline/cli.mjs fetch      # PDF-y -> raw/, parsed/manifest.json
docker compose run --rm pipeline node pipeline/cli.mjs extract    # -> parsed/departures.json
docker compose run --rm pipeline node pipeline/cli.mjs calendar   # -> parsed/calendar_days.json
docker compose run --rm load                                       # PostGIS + load-db + checks
docker compose run --rm pipeline node --test "tests/*.test.mjs"   # testy jednostkowe i testy parsed/ (bez bazy)
docker compose run --rm test-db                                    # test integracyjny: laduje 4, 7, 16, N1, N2 do PostGIS
docker compose down -v                                             # sprzatanie (baza jest efemeryczna)
```

- `--lines 7,12` wybiera inne linie (domyślnie `4,7,16,N1,N2`). Na stronie MZK jest 112 PDF-ów (ok. 353 MB); pobieramy tylko wybrane.
- Baza jest efemeryczna (tmpfs, bez hasła, bez opublikowanych portów), więc nie koliduje z niczym na hoście. `load-db` zawsze zaczyna od `DROP TABLE`, więc ponowne uruchomienie jest idempotentne.
- Weryfikacja TLS jest włączona. Serwer MZK wysyła niekompletny łańcuch certyfikatów, więc `fetch` dokłada brakujący certyfikat pośredni Sectigo do `cache/bundle.pem`.

## Układ katalogu

| Ścieżka | Zawartość |
|---|---|
| `pipeline/` | `fetch`, `extract`, `calendar`, `load-db`, `cli` (Node, bez zależności npm) |
| `sql/schema.sql`, `sql/checks.sql` | schemat `transit_*` (poziom 1 z planu) i kontrole po załadowaniu |
| `tests/` | testy `node:test`: `lines.test.mjs` (parsed/, bez bazy), `db-load.test.mjs` (PostGIS, włączany przez `MZK_DB_TEST=1`), fixtures stron (bbox), `expected.json` i `line-baseline.json` |
| `calendar_config.json` | okres ważności, wakacje szkolne (puste), dodatkowe święta |
| `parsed/` | wynik, do commitowania: `manifest.json`, `departures.json`, `calendar_days.json` |
| `raw/`, `cache/` | PDF-y i certyfikaty (w `.gitignore`) |

## Co jest sprawdzone

- Wynik obecnego przebiegu: 10 PDF-ów → 209 stron → 16826 odjazdów → PostGIS (151 przystanków, 122 dni kalendarza).
- `tests/line-baseline.json` trzyma liczbę stron i odjazdów każdego z 10 kierunków. To wartości **regresyjne z parsera**, a nie niezależna weryfikacja z PDF-ami. Test `db-load.test.mjs` sprawdza je po załadowaniu do bazy, a ponowne ładowanie nie zmienia liczb wierszy.
- Niezmienniki `lines.test.mjs`: każda flaga ma opis w legendzie swojej strony, `depot_run` to dokładnie flaga `#`, linie nocne mają tylko godziny 0–3, a linie dzienne przed 03:00 tylko zjazdy do zajezdni.
- Ręcznie porównane z obrazem PDF-a (wartości wszystkich kolumn) zostały strony: linia 7 (Wapienica Dzwonkowa 03, str. 1; Dzwonkowa Cieszyńska 02, str. 28), N1 (Osiedle Kopernika 01) i N2 (Olimpijska 01). Linie 4 i 16 nie były porównywane stronami.
- Test złoty: strona 1 linii 7 (Szyndzielnia 13) = 17 / 17 / 26 / 18 odjazdów (dzień roboczy / wakacyjny dzień roboczy / sobota / niedziela). Zawartość przepisana ręcznie z obrazu strony, niezależnie od parsera. **Brakuje potwierdzenia człowieka** (`tests/fixtures/expected.json`, pole `human_signoff`).
- Strona 4 tej samej linii ma tylko dwie kolumny (przystanek obsługiwany w dni robocze); parser radzi sobie z tym poprawnie.
- Parser jest ścisły: nieznany nagłówek kolumny, nieznany zapis odjazdu lub niezgodny numer linii kończą się błędem z numerem strony.

## Znane ograniczenia i następne kroki

1. **Współrzędne przystanków:** `transit_stop.geom` jest puste (`match = 'missing'`). Dopasowanie do OSM to osobny krok z planu (sekcja 5.4).
2. **Kursy:** tabele mają odjazdy z przystanków, a nie kursy. Składanie kursów (`transit_trip`, `transit_stop_time`) i GTFS dla OTP nie są zrobione. Flaga `K` oznacza kurs skrócony do Karbowej Hali Sportowej, a `#` zjazd do zajezdni, co wpływa na składanie kursów.
3. **Godziny po północy:** linie nocne mają w PDF godziny 0–3, zapisane bez przesunięcia (`dep_sec` 0–14399), czyli jak wydrukowano. To samo dotyczy ostatniego wiersza „0” w PDF-ach linii dziennych (np. linia 4, Łagodna Szkoła 01: `00#`); to wyłącznie zjazdy do zajezdni, zapisane jako `dep_sec` 0, a nie 86400.
4. **Święta:** dzień świąteczny (także w sobotę) dostaje rozkład niedzielny. To założenie, nie potwierdzona praktyka MZK. Wakacje szkolne nie są wpisane, więc `weekday_holiday` nie występuje w kalendarzu.
5. **Flagi:** poza `K`, `#`, `N`, `R`, `Ś`, `W` linie 4 i 16 mają flagę `D` (kurs skrócony do Warszawskiej Dworca, 882 wystąpienia). Legenda `#` bywa różna (np. „po trasie do: ŁAGODNA SZKOŁA”). Semantyka flag w kursach nie jest jeszcze użyta.
6. **Zmiany tras:** strona linii 4 ma czerwony napis „ZMIANA TRASY” i czerwoną datę obowiązywania (17.08.2026). Parser tego nie odczytuje, więc nie wiadomo, które strony mają tymczasową trasę.
7. **Docelowa integracja z aplikacją** to migracja EF Core (Data Lead + Backend/Core Lead). Ten schemat SQL jest tylko dowodem, że dane się ładują.
