import fs from 'node:fs';

const ENTITIES = { amp: '&', lt: '<', gt: '>', quot: '"', apos: "'", nbsp: ' ' };

export function decodeEntities(text) {
  return text
    .replace(/&#x([0-9a-f]+);/gi, (_, h) => String.fromCodePoint(parseInt(h, 16)))
    .replace(/&#(\d+);/g, (_, d) => String.fromCodePoint(Number(d)))
    .replace(/&([a-z]+);/gi, (m, n) => ENTITIES[n.toLowerCase()] ?? m);
}

export function median(values) {
  const sorted = [...values].sort((a, b) => a - b);
  const mid = Math.floor(sorted.length / 2);
  return sorted.length % 2 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
}

export function slug(text) {
  return text
    .replace(/ł/g, 'l')
    .replace(/Ł/g, 'L')
    .normalize('NFD')
    .replace(/\p{M}/gu, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '');
}

export function stopId(name, plate) {
  return `${slug(name)}_${plate ?? 'x'}`;
}

export function pad2(n) {
  return String(n).padStart(2, '0');
}

export function ensureDir(dir) {
  fs.mkdirSync(dir, { recursive: true });
}

// Jeden rekord w wierszu: diffy w repo sa czytelne, a plik nie puchnie.
export function writeJsonLines(file, records) {
  const body = records.map((r) => JSON.stringify(r)).join(',\n');
  fs.writeFileSync(file, `[\n${body}\n]\n`);
}

export function readJson(file) {
  return JSON.parse(fs.readFileSync(file, 'utf8'));
}
