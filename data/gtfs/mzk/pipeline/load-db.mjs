import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { DIRS } from './config.mjs';
import { readJson, stopId } from './util.mjs';

// Format tekstowy COPY: \N = NULL, znaki specjalne zapisane sekwencjami.
export function copyEscape(value) {
  if (value === null || value === undefined) return '\\N';
  return String(value).replace(/\\/g, '\\\\').replace(/\t/g, '\\t').replace(/\n/g, '\\n').replace(/\r/g, '\\r');
}

export const pgArray = (items) => `{${items.map((i) => `"${String(i).replace(/(["\\])/g, '\\$1')}"`).join(',')}}`;

const copyBlock = (table, columns, rows) =>
  `COPY ${table} (${columns.join(', ')}) FROM STDIN;\n${rows.map((r) => r.map(copyEscape).join('\t')).join('\n')}${rows.length ? '\n' : ''}\\.\n`;

// direction_id: w obrebie linii kierunki posortowane alfabetycznie (nie zalezy od nazw plikow).
export function assignDirectionIds(records) {
  const byLine = new Map();
  for (const r of records) {
    if (!byLine.has(r.line)) byLine.set(r.line, new Set());
    byLine.get(r.line).add(r.direction);
  }
  const ids = new Map();
  for (const [line, directions] of byLine) {
    [...directions].sort((a, b) => a.localeCompare(b, 'pl')).forEach((d, i) => ids.set(`${line}|${d}`, i));
  }
  return ids;
}

function buildLineDirections(records, ids) {
  const rows = new Map();
  for (const r of records) {
    const key = `${r.line}|${r.direction}`;
    if (!rows.has(key)) rows.set(key, [r.line, ids.get(key), r.direction, r.valid_from]);
  }
  return [...rows.values()];
}

function buildStops(records) {
  const stops = new Map();
  for (const r of records) {
    const id = stopId(r.stop_name, r.plate);
    if (!stops.has(id)) stops.set(id, [id, r.stop_name, r.plate, null, null, null, 'missing', null]);
  }
  return [...stops.values()];
}

function buildSources(manifest, records, ids) {
  return manifest.map((entry, i) => {
    const source = path.basename(entry.file);
    const first = records.find((r) => r.source === source);
    if (!first) throw new Error(`Manifest wskazuje ${source}, ale brak rekordow z tego PDF-a (uruchom extract)`);
    const modified = entry.last_modified ? new Date(entry.last_modified).toISOString() : null;
    return [i + 1, entry.url, entry.sha256, modified, entry.uploads_date, first.valid_from, first.line, ids.get(`${first.line}|${first.direction}`), false];
  });
}

function buildDepartures(records, manifest, ids) {
  const sourceIds = new Map(manifest.map((m, i) => [path.basename(m.file), i + 1]));
  const rows = [];
  for (const r of records) {
    const sid = stopId(r.stop_name, r.plate);
    for (const [service, deps] of Object.entries(r.departures)) {
      for (const d of deps) {
        rows.push([sourceIds.get(r.source), r.line, ids.get(`${r.line}|${r.direction}`), r.seq, sid, service, d.sec, pgArray(d.flags), d.depot_run]);
      }
    }
  }
  return rows;
}

export function buildSql({ records, manifest, calendarDays, schemaSql }) {
  const ids = assignDirectionIds(records);
  const departures = buildDepartures(records, manifest, ids);
  const parts = [
    'BEGIN;',
    schemaSql,
    copyBlock('transit_line_direction', ['line', 'direction_id', 'headsign', 'valid_from'], buildLineDirections(records, ids)),
    copyBlock('transit_stop', ['stop_id', 'name', 'plate', 'geom', 'osm_id', 'match_score', 'match', 'transfer_group'], buildStops(records)),
    copyBlock('transit_source', ['source_id', 'url', 'sha256', 'last_modified', 'uploads_date', 'valid_from', 'line', 'direction_id', 'superseded'], buildSources(manifest, records, ids)),
    copyBlock('transit_calendar_day', ['day', 'service', 'public_holiday', 'note'], calendarDays.map((d) => [d.day, d.service, d.public_holiday, d.note])),
    copyBlock('transit_departure', ['source_id', 'line', 'direction_id', 'stop_seq', 'stop_id', 'service', 'dep_sec', 'flags', 'depot_run'], departures),
    'COMMIT;',
  ];
  return { sql: parts.join('\n'), counts: { departures: departures.length } };
}

function psql(args, input) {
  return execFileSync('psql', ['-X', '-v', 'ON_ERROR_STOP=1', ...args], {
    input,
    encoding: 'utf8',
    stdio: input === undefined ? 'inherit' : ['pipe', 'pipe', 'inherit'],
    maxBuffer: 256 * 1024 * 1024,
  });
}

export function loadDb() {
  const records = readJson(path.join(DIRS.parsed, 'departures.json'));
  const manifest = readJson(path.join(DIRS.parsed, 'manifest.json'));
  const calendarDays = readJson(path.join(DIRS.parsed, 'calendar_days.json'));
  const schemaSql = fs.readFileSync(path.join(DIRS.sql, 'schema.sql'), 'utf8');
  const { sql, counts } = buildSql({ records, manifest, calendarDays, schemaSql });
  psql(['-q'], sql);
  console.log(`load-db: zaladowano ${counts.departures} odjazdow, ${records.length} stron rozkladu, ${calendarDays.length} dni kalendarza`);
}

export function runChecks() {
  psql(['-f', path.join(DIRS.sql, 'checks.sql')]);
}
