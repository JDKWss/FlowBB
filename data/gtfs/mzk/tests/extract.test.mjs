import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { parseBbox, parseCell, parsePage } from '../pipeline/extract.mjs';

const dir = path.join(path.dirname(fileURLToPath(import.meta.url)), 'fixtures');
const expected = JSON.parse(fs.readFileSync(path.join(dir, 'expected.json'), 'utf8'));
const loadPage = (name) => parseBbox(fs.readFileSync(path.join(dir, `${name}.bbox.xml`), 'utf8'))[0];
const fmt = (d) => `${d.t}${d.flags.join('')}`;

test('podpis fixture: ostrzezenie, gdy brak potwierdzenia czlowieka', (t) => {
  if (!expected.verified.human_signoff) t.diagnostic('UWAGA: expected.json nie ma potwierdzenia czlowieka (etap A0)');
  assert.ok(expected.verified.by.length > 0);
});

for (const name of ['l7_wapienica_p1', 'l7_wapienica_p4']) {
  test(`zlota strona ${name}: naglowek, kolumny, legenda i odjazdy`, () => {
    const want = expected[name];
    const got = parsePage(loadPage(name), 1);
    assert.equal(got.stop_name, want.stop_name);
    assert.equal(got.plate, want.plate);
    assert.equal(got.direction, want.direction);
    assert.equal(got.valid_from, want.valid_from);
    assert.equal(got.page_line, want.page_line);
    assert.deepEqual(got.columns_raw, want.columns_raw);
    assert.deepEqual(Object.keys(got.legend).sort(), [...want.legend_keys].sort());
    for (const service of Object.keys(want.departures)) {
      assert.deepEqual(got.departures[service].map(fmt), want.departures[service], `service ${service}`);
    }
  });
}

test('nierozpoznany naglowek kolumny konczy sie bledem (a nie cichym gubieniem danych)', () => {
  const words = loadPage('l7_wapienica_p1').map((w) => (w.t === 'Soboty' ? { ...w, t: 'noc z pt. na sob.' } : w));
  assert.throws(() => parsePage(words, 3), /strona 3: nierozpoznany naglowek kolumny "noc z pt\. na sob\."/);
});

test('flagi to kazdy znak po minutach, takze polskie litery', () => {
  const deps = parseCell('10#, 38#Ś, 50K', 17, 1);
  assert.deepEqual(deps.map(fmt), ['17:10#', '17:38#Ś', '17:50K']);
  assert.deepEqual(deps.map((d) => d.depot_run), [true, true, false]);
  assert.equal(deps[0].sec, 17 * 3600 + 10 * 60);
});

test('godziny po polnocy zostaja jak w PDF (linie nocne)', () => {
  const [d] = parseCell('35', 0, 1);
  assert.equal(d.t, '00:35');
  assert.equal(d.sec, 35 * 60);
});

test('niepoprawny zapis odjazdu to blad z numerem strony', () => {
  assert.throws(() => parseCell('7x', 5, 9), /strona 9: nieznany zapis odjazdu "7x"/);
  assert.throws(() => parseCell('75', 5, 9), /nieznany zapis odjazdu "75"/);
});
