import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { DIRS, OVERPASS_BBOX, OVERPASS_URL, ROOT, USER_AGENT } from './config.mjs';
import { ensureDir, readJson, slug, writeJsonLines } from './util.mjs';

const cachePath = () => path.join(DIRS.cache, 'overpass-stops.json');
const overridesPath = () => path.join(ROOT, 'stops_overrides.json');

// Maksymalny rozrzut wezlow OSM o tej samej nazwie. Wiecej oznacza, ze nazwa trafila w dwa rozne
// przystanki i wspolrzedna bylaby zmyslona, wiec krok konczy sie bledem zamiast zapisac srednia.
export const MAX_SPREAD_METERS = 400;

const QUERY = `[out:json][timeout:180];
(
  node["highway"="bus_stop"](${OVERPASS_BBOX});
  node["public_transport"="platform"](${OVERPASS_BBOX});
);
out tags center;`;

// Nazwa przystanku -> klucz dopasowania. "NZ" (na zadanie) i prefiks miasta wystepuja tylko po
// jednej stronie, wiec sa odcinane przed porownaniem. Reszte normalizuje wspolne slug().
export function matchKey(name) {
  return slug(
    name
      .trim()
      .replace(/\s+N[ŻZ]$/u, '')
      .replace(/^Bielsko-Biała,\s*/u, ''),
  );
}

export function metersBetween(a, b) {
  const meanLatitude = ((a.latitude + b.latitude) / 2) * (Math.PI / 180);
  const dy = (a.latitude - b.latitude) * 111_200;
  const dx = (a.longitude - b.longitude) * 111_200 * Math.cos(meanLatitude);
  return Math.hypot(dx, dy);
}

function centroid(points) {
  const latitude = points.reduce((sum, p) => sum + p.latitude, 0) / points.length;
  const longitude = points.reduce((sum, p) => sum + p.longitude, 0) / points.length;
  return { latitude, longitude };
}

function spreadMeters(points) {
  if (points.length < 2) return 0;
  const center = centroid(points);
  return Math.max(...points.map((p) => metersBetween(p, center)));
}

export function indexOsmStops(elements) {
  const index = new Map();
  for (const element of elements) {
    const name = element.tags?.name;
    const latitude = element.lat ?? element.center?.lat;
    const longitude = element.lon ?? element.center?.lon;
    if (!name || latitude === undefined || longitude === undefined) continue;
    const key = matchKey(name);
    if (!index.has(key)) index.set(key, []);
    index.get(key).push({ latitude, longitude, osm_id: element.id });
  }
  return index;
}

export function buildStops(stopNames, osmIndex, overrides = {}) {
  const records = [];
  const unmatched = [];
  for (const stopName of stopNames) {
    const override = overrides[stopName];
    if (override) {
      records.push({ stop_name: stopName, latitude: override.latitude, longitude: override.longitude,
        match: 'override', osm_ids: [], spread_m: 0 });
      continue;
    }
    const candidates = osmIndex.get(matchKey(stopName));
    if (!candidates) {
      unmatched.push(stopName);
      continue;
    }
    const spread = spreadMeters(candidates);
    if (spread > MAX_SPREAD_METERS) {
      throw new Error(
        `Przystanek "${stopName}": ${candidates.length} wezlow OSM o rozrzucie ${Math.round(spread)} m ` +
          `(limit ${MAX_SPREAD_METERS} m). Nazwa trafila w rozne przystanki - dopisz wspolrzedna do stops_overrides.json.`,
      );
    }
    const { latitude, longitude } = centroid(candidates);
    records.push({ stop_name: stopName, latitude: Number(latitude.toFixed(7)),
      longitude: Number(longitude.toFixed(7)), match: 'auto',
      osm_ids: candidates.map((c) => c.osm_id).sort((a, b) => a - b), spread_m: Math.round(spread) });
  }
  records.sort((a, b) => a.stop_name.localeCompare(b.stop_name, 'pl'));
  return { records, unmatched };
}

// Odpowiedz Overpass jest cache'owana: krok mozna powtarzac bez ponownego obciazania publicznego API.
export function ensureOverpassCache() {
  const target = cachePath();
  if (fs.existsSync(target)) return readJson(target);
  ensureDir(DIRS.cache);
  execFileSync('curl', ['-sSfL', '-A', USER_AGENT, '-o', target, OVERPASS_URL,
    '--data-urlencode', `data=${QUERY}`], { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });
  return readJson(target);
}

function readOverrides() {
  const file = overridesPath();
  return fs.existsSync(file) ? readJson(file) : {};
}

export function writeStops() {
  const departures = readJson(path.join(DIRS.parsed, 'departures.json'));
  const stopNames = [...new Set(departures.map((r) => r.stop_name))];
  const overpass = ensureOverpassCache();
  const { records, unmatched } = buildStops(stopNames, indexOsmStops(overpass.elements ?? []), readOverrides());

  ensureDir(DIRS.parsed);
  writeJsonLines(path.join(DIRS.parsed, 'stops.json'), records);
  console.log(`stops: dopasowano ${records.length}/${stopNames.length} przystankow -> parsed/stops.json`);
  if (unmatched.length > 0) {
    console.log(`stops: BRAK wspolrzednych dla ${unmatched.length}: ${unmatched.join(', ')}`);
    console.log('stops: dopisz je do stops_overrides.json ({"Nazwa": {"latitude": .., "longitude": ..}}).');
  }
}
