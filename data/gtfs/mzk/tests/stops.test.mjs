import assert from 'node:assert/strict';
import path from 'node:path';
import test from 'node:test';
import { DIRS } from '../pipeline/config.mjs';
import { MAX_SPREAD_METERS, buildStops, indexOsmStops, matchKey, metersBetween } from '../pipeline/stops.mjs';
import { readJson } from '../pipeline/util.mjs';

const departures = readJson(path.join(DIRS.parsed, 'departures.json'));
const stops = readJson(path.join(DIRS.parsed, 'stops.json'));

const osmNode = (id, name, latitude, longitude) => ({ id, lat: latitude, lon: longitude, tags: { name } });

test('matchKey odcina sufiks "na zadanie" i prefiks miasta, bo wystepuja tylko po jednej stronie', () => {
  assert.equal(matchKey('Kwiatkowskiego Grażyńskiego NŻ'), matchKey('Kwiatkowskiego Grażyńskiego'));
  assert.equal(matchKey('Bielsko-Biała, Plac Żwirki i Wigury'), matchKey('Plac Żwirki i Wigury'));
});

test('matchKey normalizuje diakrytyki i wielkosc liter', () => {
  assert.equal(matchKey('Hałcnów Kościół'), 'halcnow-kosciol');
  assert.equal(matchKey('ŻYWIECKA Stadion Miejski'), matchKey('Żywiecka Stadion Miejski'));
});

test('indexOsmStops grupuje wezly po kluczu i pomija wezly bez nazwy albo bez wspolrzednych', () => {
  const index = indexOsmStops([
    osmNode(1, 'Browarna', 49.81, 19.03),
    osmNode(2, 'Browarna', 49.8101, 19.0301),
    { id: 3, lat: 49.8, lon: 19.0, tags: {} },
    { id: 4, tags: { name: 'Bez wspolrzednych' } },
  ]);
  assert.deepEqual([...index.keys()], ['browarna']);
  assert.equal(index.get('browarna').length, 2);
});

test('buildStops usrednia slupki tego samego przystanku i zapisuje ich identyfikatory OSM', () => {
  const index = indexOsmStops([osmNode(2, 'Browarna', 49.8102, 19.0302), osmNode(1, 'Browarna', 49.8100, 19.0300)]);
  const { records, unmatched } = buildStops(['Browarna'], index);

  assert.deepEqual(unmatched, []);
  assert.equal(records.length, 1);
  assert.equal(records[0].match, 'auto');
  assert.deepEqual(records[0].osm_ids, [1, 2], 'identyfikatory posortowane, zeby diff byl stabilny');
  assert.ok(Math.abs(records[0].latitude - 49.8101) < 1e-6);
});

test('buildStops raportuje nazwy bez odpowiednika w OSM, a nie zapisuje ich ze zerowa wspolrzedna', () => {
  const { records, unmatched } = buildStops(['Nie ma takiego przystanku'], indexOsmStops([]));
  assert.deepEqual(records, []);
  assert.deepEqual(unmatched, ['Nie ma takiego przystanku']);
});

test('buildStops przyjmuje reczna korekte zamiast dopasowania automatycznego', () => {
  const index = indexOsmStops([osmNode(1, 'Browarna', 49.0, 19.0)]);
  const { records } = buildStops(['Browarna'], index, { Browarna: { latitude: 49.5, longitude: 19.5 } });
  assert.equal(records[0].match, 'override');
  assert.equal(records[0].latitude, 49.5);
});

test('buildStops konczy sie bledem, gdy jedna nazwa trafia w dwa rozne przystanki', () => {
  const index = indexOsmStops([osmNode(1, 'Dworzec', 49.80, 19.00), osmNode(2, 'Dworzec', 49.83, 19.05)]);
  assert.throws(() => buildStops(['Dworzec'], index), /rozrzucie \d+ m/);
});

test('metersBetween liczy odleglosci w metrach z dokladnoscia wystarczajaca dla promienia przystanku', () => {
  assert.ok(Math.abs(metersBetween({ latitude: 49.8, longitude: 19.0 }, { latitude: 49.809, longitude: 19.0 }) - 1000) < 20);
});

test('parsed/stops.json pokrywa kazda nazwe przystanku wystepujaca w parsed/departures.json', () => {
  const needed = [...new Set(departures.map((page) => page.stop_name))].sort();
  const covered = stops.map((stop) => stop.stop_name).sort();
  assert.deepEqual(covered, needed, 'planer trasy pomija przystanki bez wspolrzednych');
});

test('parsed/stops.json ma wspolrzedne w granicach Bielska-Bialej i maly rozrzut slupkow', () => {
  for (const stop of stops) {
    assert.ok(stop.latitude > 49.6 && stop.latitude < 50.0, `${stop.stop_name}: szerokosc ${stop.latitude}`);
    assert.ok(stop.longitude > 18.8 && stop.longitude < 19.4, `${stop.stop_name}: dlugosc ${stop.longitude}`);
    assert.ok(stop.spread_m <= MAX_SPREAD_METERS, `${stop.stop_name}: rozrzut ${stop.spread_m} m`);
  }
});
