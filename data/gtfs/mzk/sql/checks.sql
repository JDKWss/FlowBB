-- Kontrole po zaladowaniu. Blok DO konczy sie bledem, gdy zlote liczby sie nie zgadzaja.

DO $$
DECLARE actual jsonb;
BEGIN
  SELECT jsonb_object_agg(service, n) INTO actual FROM (
    SELECT d.service, count(*) AS n
    FROM transit_departure d
    JOIN transit_line_direction ld USING (line, direction_id)
    WHERE d.line = '7' AND ld.headsign = 'Wapienica Dzwonkowa'
      AND d.stop_id = 'szyndzielnia_13' AND d.stop_seq = 1
    GROUP BY d.service
  ) t;
  IF actual IS DISTINCT FROM '{"weekday":17,"weekday_holiday":17,"saturday":26,"sunday":18}'::jsonb THEN
    RAISE EXCEPTION 'Zlote liczby odjazdow (linia 7, Szyndzielnia 13) sie nie zgadzaja: %', actual;
  END IF;
  RAISE NOTICE 'OK: linia 7, Szyndzielnia 13 -> %', actual;
END $$;

\echo '--- 1. Liczba stron rozkladu i odjazdow per linia / kierunek'
SELECT ld.line, ld.headsign, ld.valid_from,
       count(DISTINCT d.stop_seq) AS przystankow,
       count(*) AS odjazdow,
       count(*) FILTER (WHERE d.depot_run) AS zjazdy_do_zajezdni
FROM transit_departure d
JOIN transit_line_direction ld USING (line, direction_id)
GROUP BY ld.line, ld.direction_id, ld.headsign, ld.valid_from
ORDER BY ld.line, ld.direction_id;

\echo '--- 2. Linie nocne: odjazdy w godzinach 0-3 (czas jak w PDF, po polnocy)'
SELECT d.line, ld.headsign, d.service, count(*) AS odjazdow_po_polnocy,
       to_char(min(d.dep_sec) * interval '1 second', 'HH24:MI') AS najwczesniej,
       to_char(max(d.dep_sec) * interval '1 second', 'HH24:MI') AS najpozniej
FROM transit_departure d
JOIN transit_line_direction ld USING (line, direction_id)
WHERE d.line LIKE 'N%' AND d.dep_sec < 4 * 3600
GROUP BY d.line, ld.headsign, d.service
ORDER BY d.line, ld.headsign, d.service;

\echo '--- 3. Powrot bez wspolrzednych: odjazdy z przystanku 2026-10-03 (sobota) po 21:30, bez zjazdow do zajezdni'
SELECT d.line, ld.headsign, to_char(d.dep_sec * interval '1 second', 'HH24:MI') AS odjazd, d.flags
FROM transit_calendar_day c
JOIN transit_departure d ON d.service = c.service
JOIN transit_line_direction ld USING (line, direction_id)
WHERE c.day = DATE '2026-10-03' AND d.stop_id = 'szyndzielnia_13' AND NOT d.depot_run
  AND d.dep_sec >= 21 * 3600 + 30 * 60
ORDER BY d.dep_sec;

\echo '--- 4. Kalendarz: typy dni i swieta'
SELECT service, count(*) AS dni, count(*) FILTER (WHERE public_holiday) AS swieta
FROM transit_calendar_day GROUP BY service ORDER BY service;

\echo '--- 5. Przystanki bez wspolrzednych (PoC: dopasowanie do OSM to osobny krok)'
SELECT match, count(*) FROM transit_stop GROUP BY match;
