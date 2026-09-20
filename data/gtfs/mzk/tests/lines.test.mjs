import assert from 'node:assert/strict';
import path from 'node:path';
import test from 'node:test';
import { DEFAULT_LINES, DIRS, ROOT, SERVICES } from '../pipeline/config.mjs';
import { readJson } from '../pipeline/util.mjs';

// Testy commitowanego parsed/ dla linii 4, 7, 16, N1 i N2: bez sieci, PDF-ow i bazy.
const records = readJson(path.join(DIRS.parsed, 'departures.json'));
const manifest = readJson(path.join(DIRS.parsed, 'manifest.json'));
const { directions: baseline } = readJson(path.join(ROOT, 'tests/fixtures/line-baseline.json'));

const NIGHT_LIMIT_SEC = 4 * 3600;
const DAY_START_SEC = 3 * 3600;
const countDepartures = (page) => SERVICES.reduce((n, s) => n + page.departures[s].length, 0);
const allDepartures = () => records.flatMap((page) => SERVICES.flatMap((s) => page.departures[s]));

test('domyslne linie to 4, 7, 16, N1, N2, a parsed/ zawiera dokladnie te linie', () => {
  assert.deepEqual([...DEFAULT_LINES].sort(), ['16', '4', '7', 'N1', 'N2']);
  assert.deepEqual([...new Set(records.map((r) => r.line))].sort(), [...DEFAULT_LINES].sort());
});

test('kazda linia ma dwa kierunki, a liczba stron i odjazdow zgadza sie z wartosciami bazowymi', () => {
  const actual = new Map();
  for (const page of records) {
    const key = `${page.line}|${page.direction}`;
    const entry = actual.get(key) ?? { pages: 0, departures: 0 };
    actual.set(key, { pages: entry.pages + 1, departures: entry.departures + countDepartures(page) });
  }
  assert.equal(actual.size, baseline.length, 'liczba (linia, kierunek)');
  for (const b of baseline) {
    assert.deepEqual(actual.get(`${b.line}|${b.direction}`), { pages: b.pages, departures: b.departures }, `${b.line} -> ${b.direction}`);
  }
  for (const line of DEFAULT_LINES) assert.equal(baseline.filter((b) => b.line === line).length, 2, `linia ${line}`);
});

test('kazda strona ma odjazdy, wszystkie cztery typy dnia i ciagly numer stop_seq w obrebie PDF-a', () => {
  const bySource = Object.groupBy(records, (r) => r.source);
  for (const [source, pages] of Object.entries(bySource)) {
    pages.forEach((page, i) => assert.equal(page.seq, i + 1, `${source}: kolejnosc stron`));
  }
  for (const page of records) {
    assert.deepEqual(Object.keys(page.departures).sort(), [...SERVICES].sort(), `${page.source} str. ${page.seq}`);
    assert.ok(countDepartures(page) > 0, `${page.source} str. ${page.seq} (${page.stop_name}): brak odjazdow`);
  }
});

test('kazda flaga odjazdu ma opis w legendzie swojej strony', () => {
  for (const page of records) {
    for (const dep of SERVICES.flatMap((s) => page.departures[s])) {
      for (const flag of dep.flags) assert.ok(flag in page.legend, `${page.source} str. ${page.seq}: flaga "${flag}" bez legendy`);
    }
  }
});

test('depot_run oznacza dokladnie odjazdy z flaga #', () => {
  assert.ok(allDepartures().every((d) => d.depot_run === d.flags.includes('#')));
});

test('linie nocne: odjazdy tylko w godzinach 0-3 (dep_sec < 14400), bez flag', () => {
  const night = records.filter((r) => r.line.startsWith('N'));
  assert.ok(night.length > 0);
  for (const page of night) {
    for (const dep of SERVICES.flatMap((s) => page.departures[s])) {
      assert.ok(dep.sec >= 0 && dep.sec < NIGHT_LIMIT_SEC, `${page.line} ${page.stop_name}: ${dep.t}`);
      assert.deepEqual(dep.flags, [], `${page.line} ${page.stop_name}: ${dep.t}`);
    }
  }
});

// Wyjatek: PDF-y linii dziennych maja na koncu tabeli wiersz godziny "0" (np. linia 4, Lagodna Szkola 01:
// "00#"), zapisany jako 00:00 (dep_sec 0). Sa to wylacznie zjazdy do zajezdni.
test('linie dzienne: odjazdy od 03:00 do 24:00, a przed 03:00 tylko zjazdy do zajezdni', () => {
  const day = records.filter((r) => !r.line.startsWith('N'));
  assert.ok(day.length > 0);
  for (const page of day) {
    for (const dep of SERVICES.flatMap((s) => page.departures[s])) {
      const where = `${page.line} ${page.stop_name}: ${dep.t}`;
      assert.ok(dep.sec < 24 * 3600, where);
      assert.ok(dep.sec >= DAY_START_SEC || dep.depot_run, `${where} przed 03:00 bez flagi #`);
    }
  }
});

test('manifest: jeden PDF na kierunek, kazdy ma rekordy w departures.json', () => {
  assert.equal(manifest.length, baseline.length);
  const sources = new Set(records.map((r) => r.source));
  for (const entry of manifest) {
    assert.match(entry.sha256, /^[0-9a-f]{64}$/, entry.file);
    assert.match(entry.url, /^https:\/\/.+\.pdf$/, entry.file);
    assert.ok(sources.has(path.basename(entry.file)), `${entry.file}: brak rekordow w departures.json`);
  }
  assert.deepEqual([...new Set(manifest.map((m) => m.line))].sort(), [...DEFAULT_LINES].sort());
});
