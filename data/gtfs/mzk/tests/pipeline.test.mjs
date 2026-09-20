import assert from 'node:assert/strict';
import test from 'node:test';
import { buildCalendarDays, easterSunday, publicHolidays } from '../pipeline/calendar.mjs';
import { dropSuperseded, parseListing } from '../pipeline/fetch.mjs';
import { assignDirectionIds, buildSql, copyEscape, pgArray } from '../pipeline/load-db.mjs';
import { slug, stopId } from '../pipeline/util.mjs';

test('stop_id: nazwa + tabliczka, brak tabliczki to "x"', () => {
  assert.equal(stopId('Szyndzielnia', '13'), 'szyndzielnia_13');
  assert.equal(stopId('Słowackiego BCK', null), 'slowackiego-bck_x');
  assert.equal(slug('Hałcnów Kościół'), 'halcnow-kosciol');
});

test('Wielkanoc 2026 i swieta ruchome', () => {
  assert.equal(easterSunday(2026).toISOString().slice(0, 10), '2026-04-05');
  const h = publicHolidays(2026);
  for (const day of ['2026-04-06', '2026-05-24', '2026-06-04', '2026-11-11', '2026-12-25']) assert.ok(h.has(day), day);
});

test('kalendarz: swieto = rozklad niedzielny, wakacyjne dni tylko po skonfigurowaniu', () => {
  const base = { valid_from: '2026-09-28', valid_to: '2026-11-15', school_holiday_ranges: [], extra_holidays: [] };
  const byDay = new Map(buildCalendarDays(base).map((d) => [d.day, d]));
  assert.equal(byDay.get('2026-10-03').service, 'saturday');
  assert.equal(byDay.get('2026-10-04').service, 'sunday');
  assert.equal(byDay.get('2026-10-05').service, 'weekday');
  assert.equal(byDay.get('2026-11-11').service, 'sunday');
  assert.equal(byDay.get('2026-11-11').public_holiday, true);
  assert.ok([...byDay.values()].every((d) => d.service !== 'weekday_holiday'), 'puste wakacje: brak weekday_holiday');

  const withHoliday = buildCalendarDays({ ...base, school_holiday_ranges: [['2026-10-05', '2026-10-06']] });
  assert.equal(withHoliday.find((d) => d.day === '2026-10-05').service, 'weekday_holiday');
  assert.equal(withHoliday.find((d) => d.day === '2026-10-07').service, 'weekday');
});

const SAMPLE_HTML = `
<div class="wp-block-media-text alignwide"><figure><img alt="7o"></figure><div>
<p><a href="https://x.pl/wp-content/uploads/2026/08/7-kier.-A.pdf">Kie­ru­nek: <strong>Wapie­ni­ca</strong></a></p>
<p><a href="https://x.pl/wp-content/uploads/2026/06/7-pozostale.pdf">pozo­sta­łe przystanki</a></p></div></div>
<div class="wp-block-media-text alignwide"><figure><img alt="N1o"></figure><div>
<p><a href="https://x.pl/wp-content/uploads/2025/10/N1-stary.pdf">Kierunek: <strong>Zajezdnia MZK</strong></a></p>
<p><a href="https://x.pl/wp-content/uploads/2026/07/N1-nowy.pdf">Kierunek: <strong>Zajezdnia MZK</strong></a></p></div></div>`;

test('lista PDF-ow: linia z alt obrazka, kierunek z kotwicy, bez miekkich lacznikow', () => {
  const entries = parseListing(SAMPLE_HTML);
  assert.equal(entries.length, 4);
  assert.deepEqual(
    entries.filter((e) => e.kind === 'kier' && e.line === '7').map((e) => e.label),
    ['Wapienica'],
  );
  assert.equal(entries.find((e) => e.kind === 'pozostale').label, null);
  assert.equal(entries.find((e) => e.line === 'N1').uploads_date, '2025/10');
});

test('duplikaty (linia, cel): wygrywa pozniejsza data w sciezce uploads', () => {
  const kept = dropSuperseded(parseListing(SAMPLE_HTML).filter((e) => e.line === 'N1'));
  assert.equal(kept.length, 1);
  assert.match(kept[0].url, /N1-nowy\.pdf$/);
});

test('COPY: NULL i znaki specjalne, tablica flag', () => {
  assert.equal(copyEscape(null), '\\N');
  assert.equal(copyEscape('a\tb\nc\\d'), 'a\\tb\\nc\\\\d');
  assert.equal(pgArray(['#', 'N']), '{"#","N"}');
  assert.equal(pgArray([]), '{}');
});

const record = (line, direction, seq, stop, plate, deps) => ({
  line, direction, seq, source: `${line}-${direction}.pdf`, stop_name: stop, plate, valid_from: '2026-09-01',
  departures: { weekday: deps, weekday_holiday: [], saturday: [], sunday: [] },
});
const dep = (t, sec, flags = []) => ({ t, sec, flags, depot_run: flags.includes('#') });

test('direction_id: alfabetycznie w obrebie linii, niezaleznie od kolejnosci plikow', () => {
  const ids = assignDirectionIds([record('7', 'Wapienica Dzwonkowa', 1, 'A', '1', []), record('7', 'Szyndzielnia', 1, 'B', '2', [])]);
  assert.equal(ids.get('7|Szyndzielnia'), 0);
  assert.equal(ids.get('7|Wapienica Dzwonkowa'), 1);
});

test('SQL: petla (ten sam przystanek dwa razy w kierunku) daje dwie pozycje odjazdow, jeden stop', () => {
  const records = [
    record('7', 'Szyndzielnia', 1, 'Karbowa Hala Sportowa', '01', [dep('05:00', 18000)]),
    record('7', 'Szyndzielnia', 2, 'Karbowa Hala Sportowa', '01', [dep('05:03', 18180, ['#'])]),
  ];
  const manifest = [{ file: '7-Szyndzielnia.pdf', url: 'https://x/7.pdf', sha256: 'a'.repeat(64), last_modified: 'Mon, 31 Aug 2026 11:22:45 GMT', uploads_date: '2026/08' }];
  const { sql, counts } = buildSql({ records, manifest, calendarDays: [], schemaSql: '-- schema' });
  assert.equal(counts.departures, 2);
  assert.equal((sql.match(/karbowa-hala-sportowa_01\t/g) ?? []).length, 1 + 2, 'jeden wiersz stopu i dwa odjazdy');
  assert.match(sql, /COPY transit_departure[\s\S]*\{"#"\}\ttrue/);
});
