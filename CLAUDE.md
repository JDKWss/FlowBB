@AGENTS.md

# Claude Code - instrukcje dodatkowe

- `AGENTS.md` jest jedynym zrodlem prawdy o projekcie, stacku, rolach i workflow. Baza runtime to Neo4j ([docs/adr/001-runtime-persistence.md](docs/adr/001-runtime-persistence.md)); plan pracy: [docs/MVP_WORK_PLAN.md](docs/MVP_WORK_PLAN.md).
- Nie duplikuj ani nie zmieniaj jego zasad w tym pliku.
- Na poczatku zadania podaj: wlasciciela obszaru, pliki do zmiany, test akceptacyjny i ewentualna potrzebe zmiany kontraktu.
- Uzywaj planu dla zadania wieloplikowego lub przekraczajacego jeden modul; dla malej poprawki dzialaj bez rozbudowanego planowania.
- Nie uruchamiaj autonomicznie kilku piszacych agentow w tym samym worktree. Zadania implementacyjne wykonuj w osobnym worktree z `origin/develop`.
- Przed zadaniem zalezacym od innego modulu (np. Attendance od Events) sprawdz, czy `origin/develop` go zawiera; jesli nie, zglos blocker zamiast implementowac cudzy modul.
- Subagentom deleguj analize lub niezalezne, niekolidujace obszary. Jeden agent odpowiada za koncowa integracje i weryfikacje.
- Nie wykonuj `git commit`, `git push`, merge ani rebase bez wyraznego polecenia czlowieka.
- Po zmianie pokaz diff w skrocie, uruchom wlasciwe buildy/testy i jasno wskaz nieweryfikowane elementy.
