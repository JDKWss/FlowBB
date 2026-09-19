import path from 'node:path';
import { DEFAULT_LINES, DIRS } from './config.mjs';
import { writeCalendar } from './calendar.mjs';
import { extractManifest } from './extract.mjs';
import { fetchLines, readManifest } from './fetch.mjs';
import { loadDb, runChecks } from './load-db.mjs';
import { ensureDir, writeJsonLines } from './util.mjs';

const HELP = `Uzycie: node pipeline/cli.mjs <komenda...> [--lines 7,N1,N2]

Komendy (wykonywane po kolei):
  fetch     pobiera PDF-y wybranych linii do raw/ i zapisuje parsed/manifest.json
  extract   czyta PDF-y i zapisuje parsed/departures.json
  calendar  generuje parsed/calendar_days.json z calendar_config.json
  load-db   laduje dane do PostgreSQL (wymaga PGHOST/PGUSER/PGDATABASE)
  checks    uruchamia sql/checks.sql (konczy sie bledem przy zlych liczbach)
  all       fetch, extract, calendar, load-db, checks

Domyslne linie: ${DEFAULT_LINES.join(', ')}`;

function extract() {
  const records = extractManifest(readManifest());
  ensureDir(DIRS.parsed);
  writeJsonLines(path.join(DIRS.parsed, 'departures.json'), records);
  const total = records.reduce((n, r) => n + Object.values(r.departures).reduce((m, d) => m + d.length, 0), 0);
  console.log(`extract: ${records.length} stron, ${total} odjazdow -> parsed/departures.json`);
}

function parseArgs(argv) {
  const commands = [];
  let lines = DEFAULT_LINES;
  for (let i = 0; i < argv.length; i++) {
    if (argv[i] === '--lines') lines = argv[++i].split(',').map((s) => s.trim()).filter(Boolean);
    else if (argv[i].startsWith('--lines=')) lines = argv[i].slice(8).split(',').map((s) => s.trim()).filter(Boolean);
    else if (!argv[i].startsWith('--')) commands.push(argv[i]);
  }
  return { commands, lines };
}

const { commands, lines } = parseArgs(process.argv.slice(2));
const steps = {
  fetch: () => fetchLines(lines),
  extract,
  calendar: writeCalendar,
  'load-db': loadDb,
  checks: runChecks,
};
steps.all = () => ['fetch', 'extract', 'calendar', 'load-db', 'checks'].forEach((s) => steps[s]());

if (commands.length === 0 || process.argv.includes('--help')) {
  console.log(HELP);
} else {
  for (const name of commands) {
    if (!steps[name]) {
      console.error(`Nieznana komenda: ${name}\n\n${HELP}`);
      process.exit(2);
    }
    steps[name]();
  }
}
