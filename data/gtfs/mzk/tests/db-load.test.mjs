import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import path from 'node:path';
import { before, describe, it } from 'node:test';
import { DIRS, ROOT, SERVICES } from '../pipeline/config.mjs';
import { loadDb } from '../pipeline/load-db.mjs';
import { readJson, stopId } from '../pipeline/util.mjs';

// Test integracyjny: laduje parsed/ do PostGIS i sprawdza wynik SQL-em.
// Wlaczany zmienna MZK_DB_TEST=1 (serwis `test-db` w compose.yaml); bez niej jest pomijany.
const enabled = process.env.MZK_DB_TEST === '1';
const skip = enabled ? false : 'baza wylaczona (uruchom: docker compose run --rm test-db)';

const records = readJson(path.join(DIRS.parsed, 'departures.json'));
const { directions: baseline } = readJson(path.join(ROOT, 'tests/fixtures/line-baseline.json'));

function query(sql) {
  const out = execFileSync('psql', ['-X', '-At', '-F', '|', '-v', 'ON_ERROR_STOP=1', '-c', sql], { encoding: 'utf8' });
  return out.split('\n').filter(Boolean).map((row) => row.split('|'));
}

const scalar = (sql) => Number(query(sql)[0][0]);
const TABLES = ['transit_line_direction', 'transit_source', 'transit_stop', 'transit_departure', 'transit_calendar_day'];
const snapshot = () => Object.fromEntries(TABLES.map((t) => [t, scalar(`SELECT count(*) FROM ${t}`)]));

describe('ladowanie linii 4, 7, 16, N1, N2 do PostGIS', { skip }, () => {
  before(() => loadDb());

  it('kazdy kierunek ma w bazie tyle stron i odjazdow, ile wartosci bazowe', () => {
    const rows = query(`
      SELECT ld.line, ld.headsign, count(DISTINCT d.stop_seq), count(*)
      FROM transit_departure d JOIN transit_line_direction ld USING (line, direction_id)
      GROUP BY ld.line, ld.headsign`);
    const actual = new Map(rows.map(([line, headsign, pages, deps]) => [`${line}|${headsign}`, { pages: +pages, departures: +deps }]));
    assert.equal(actual.size, baseline.length);
    for (const b of baseline) {
      assert.deepEqual(actual.get(`${b.line}|${b.direction}`), { pages: b.pages, departures: b.departures }, `${b.line} -> ${b.direction}`);
    }
  });

  it('liczby wierszy: kierunki i zrodla po 10, przystanki i odjazdy zgodne z JSON-em', () => {
    const expectedStops = new Set(records.map((r) => stopId(r.stop_name, r.plate))).size;
    const expectedDepartures = records.reduce((n, r) => n + SERVICES.reduce((m, s) => m + r.departures[s].length, 0), 0);
    const counts = snapshot();
    assert.equal(counts.transit_line_direction, baseline.length);
    assert.equal(counts.transit_source, baseline.length);
    assert.equal(counts.transit_stop, expectedStops);
    assert.equal(counts.transit_departure, expectedDepartures);
  });

  it('flagi w bazie (tablica text[]) zgadzaja sie z flagami w JSON-ie', () => {
    const expected = {};
    for (const r of records) {
      for (const s of SERVICES) for (const d of r.departures[s]) for (const f of d.flags) expected[f] = (expected[f] ?? 0) + 1;
    }
    const actual = Object.fromEntries(query('SELECT f, count(*) FROM transit_departure, unnest(flags) AS f GROUP BY f').map(([f, n]) => [f, +n]));
    assert.deepEqual(actual, expected);
  });

  it('linie nocne maja w bazie tylko odjazdy po polnocy (dep_sec < 14400), dzienne przed 03:00 tylko zjazdy', () => {
    assert.equal(scalar("SELECT count(*) FROM transit_departure WHERE line LIKE 'N%' AND dep_sec >= 14400"), 0);
    assert.equal(scalar("SELECT count(*) FROM transit_departure WHERE line NOT LIKE 'N%' AND dep_sec < 10800 AND NOT depot_run"), 0);
  });

  it('kazdy odjazd wskazuje istniejacy przystanek i zrodlo, a zjazdy do zajezdni maja flage #', () => {
    assert.equal(scalar('SELECT count(*) FROM transit_departure d LEFT JOIN transit_stop s USING (stop_id) WHERE s.stop_id IS NULL'), 0);
    assert.equal(scalar('SELECT count(*) FROM transit_departure d LEFT JOIN transit_source s USING (source_id) WHERE s.source_id IS NULL'), 0);
    assert.equal(scalar("SELECT count(*) FROM transit_departure WHERE depot_run <> ('#' = ANY(flags))"), 0);
  });

  it('ponowne zaladowanie jest idempotentne: te same liczby wierszy w kazdej tabeli', () => {
    const afterFirstLoad = snapshot();
    loadDb();
    assert.deepEqual(snapshot(), afterFirstLoad);
  });
});
