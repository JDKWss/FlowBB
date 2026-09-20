import { execFileSync } from 'node:child_process';
import path from 'node:path';
import { DIRS, SERVICE_BY_HEADER, SERVICES } from './config.mjs';
import { decodeEntities, median, pad2 } from './util.mjs';

const ROW_TOL = 3; // pt: slowa w jednym wierszu
const CELL_GAP = 15; // pt: przerwa oddzielajaca komorki tabeli
const HEADER_GAP = 8; // pt: przerwa oddzielajaca kolumny naglowka
const DEFAULT_COLUMN_WIDTH = 120;

class PageError extends Error {
  constructor(page, message) {
    super(`strona ${page}: ${message}`);
  }
}

export function pdftotextBbox(pdfPath) {
  return execFileSync('pdftotext', ['-bbox-layout', pdfPath, '-'], {
    encoding: 'utf8',
    maxBuffer: 512 * 1024 * 1024,
  });
}

export function parseBbox(xml) {
  const wordRe = /<word xMin="([\d.]+)" yMin="([\d.]+)" xMax="([\d.]+)" yMax="([\d.]+)">([\s\S]*?)<\/word>/g;
  return xml.split('<page ').slice(1).map((chunk) =>
    [...chunk.matchAll(wordRe)].map((m) => ({ x0: +m[1], y0: +m[2], x1: +m[3], y1: +m[4], t: decodeEntities(m[5]) })),
  );
}

const sameRow = (a, b) => Math.abs(a.y0 - b.y0) < ROW_TOL;
const byPosition = (a, b) => a.y0 - b.y0 || a.x0 - b.x0;
const joinWords = (words) => [...words].sort((a, b) => a.x0 - b.x0).map((w) => w.t).join(' ');

function parseHeader(words, pageNo) {
  const anchor = words.find((w) => w.t === 'PRZYSTANEK:');
  if (!anchor) throw new PageError(pageNo, 'brak naglowka "PRZYSTANEK:"');
  const validity = words.find((w) => w.t === 'Obowiązuje' && sameRow(w, anchor));
  const stopCut = validity ? validity.x0 - 1 : Infinity;
  const stopRaw = joinWords(words.filter((w) => sameRow(w, anchor) && w.x0 > anchor.x0 && w.x0 < stopCut));
  const stop = stopRaw.match(/^(.*\S)\s+(\d{1,2})$/);

  const dirAnchor = words.find((w) => w.t === 'KIERUNEK:');
  if (!dirAnchor) throw new PageError(pageNo, 'brak naglowka "KIERUNEK:"');
  const info = words.find((w) => w.t === 'Informacje' && Math.abs(w.y0 - dirAnchor.y0) < 15);
  const dirCut = info ? info.x0 - 1 : 400;
  const directionRaw = joinWords(words.filter((w) => sameRow(w, dirAnchor) && w.x0 > dirAnchor.x0 && w.x0 < dirCut));

  const date = words.find((w) => /^\d{2}\.\d{2}\.\d{4}$/.test(w.t) && sameRow(w, anchor));
  if (!stopRaw || !directionRaw || !date) throw new PageError(pageNo, 'niepelny naglowek (przystanek, kierunek lub data)');
  const [d, m, y] = date.t.split('.');

  return {
    stop_name: stop ? stop[1] : stopRaw,
    plate: stop ? stop[2] : null,
    direction: directionRaw.replace(/\.$/, '').trim(),
    valid_from: `${y}-${m}-${d}`,
  };
}

// Duzy numer linii w zoltym polu (np. "7", "N1"): slowo o wysokosci > 35 pt po lewej.
function detectLine(words) {
  const big = words.filter((w) => w.y1 - w.y0 > 35 && w.x1 < 115 && w.y0 > 40 && w.y0 < 200);
  return big.sort((a, b) => b.x1 - b.x0 - (a.x1 - a.x0))[0]?.t ?? null;
}

function clusterHeader(headerWords) {
  const clusters = [];
  for (const w of [...headerWords].sort((a, b) => a.x0 - b.x0)) {
    const last = clusters.at(-1);
    if (last && w.x0 <= last.x1 + HEADER_GAP) {
      last.x1 = Math.max(last.x1, w.x1);
      last.words.push(w);
    } else {
      clusters.push({ x0: w.x0, x1: w.x1, words: [w] });
    }
  }
  return clusters;
}

function buildColumns(headerWords, pageNo) {
  const columns = clusterHeader(headerWords).map((c) => {
    const label = [...c.words].sort(byPosition).map((w) => w.t).join(' ');
    const service = SERVICE_BY_HEADER[label];
    if (!service) {
      const known = Object.keys(SERVICE_BY_HEADER).join(' | ');
      throw new PageError(pageNo, `nierozpoznany naglowek kolumny "${label}" (znane: ${known})`);
    }
    return { label, service, center: (c.x0 + c.x1) / 2 };
  });
  if (new Set(columns.map((c) => c.service)).size !== columns.length) {
    throw new PageError(pageNo, 'powtorzony naglowek kolumny');
  }
  const gaps = columns.slice(1).map((c, i) => c.center - columns[i].center);
  const width = gaps.length ? median(gaps) : DEFAULT_COLUMN_WIDTH;
  // Komorki sa wyrownane do lewej krawedzi kolumny, a naglowki wysrodkowane.
  return columns.map((c) => ({ ...c, left: c.center - width / 2 }));
}

function groupCells(rowWords) {
  const cells = [];
  for (const w of [...rowWords].sort((a, b) => a.x0 - b.x0)) {
    const last = cells.at(-1);
    if (last && w.x0 - last.x1 <= CELL_GAP) {
      last.x1 = w.x1;
      last.words.push(w);
    } else {
      cells.push({ x0: w.x0, x1: w.x1, words: [w] });
    }
  }
  return cells.map((c) => ({ x0: c.x0, text: c.words.map((w) => w.t).join(' ') }));
}

export function parseCell(text, hour, pageNo) {
  return text.split(',').map((raw) => {
    const token = raw.trim();
    const m = token.match(/^(\d{2})([#*\p{L}]*)$/u);
    if (!m || Number(m[1]) > 59) throw new PageError(pageNo, `nieznany zapis odjazdu "${token}" w wierszu godziny ${hour}`);
    const flags = [...m[2]];
    return { t: `${pad2(hour)}:${m[1]}`, sec: hour * 3600 + Number(m[1]) * 60, flags, depot_run: flags.includes('#') };
  });
}

function nearestColumn(columns, x) {
  return columns.reduce((best, c) => (Math.abs(x - c.left) < Math.abs(x - best.left) ? c : best));
}

function parseLegend(words, lastRow) {
  const legend = {};
  const below = words.filter((w) => w.y0 > lastRow.y1 + 2).sort(byPosition);
  const rows = [];
  for (const w of below) {
    const row = rows.find((r) => sameRow(r[0], w));
    if (row) row.push(w);
    else rows.push([w]);
  }
  for (const row of rows) {
    const m = joinWords(row).match(/^(\S)\s*-\s*(.+)$/);
    if (m) legend[m[1]] = m[2];
  }
  return legend;
}

function parseTable(words, pageNo) {
  const godzina = words.find((w) => w.t === 'Godzina');
  if (!godzina) throw new PageError(pageNo, 'brak naglowka tabeli "Godzina"');
  const hourWords = words
    .filter((w) => /^\d{1,2}$/.test(w.t) && w.x0 >= godzina.x0 - 2 && w.x1 <= godzina.x1 && w.y0 > godzina.y1 - 1)
    .sort(byPosition);
  if (hourWords.length === 0) throw new PageError(pageNo, 'tabela bez wierszy godzin');
  // Prawa krawedz kolumny godzin; dane pierwszej kolumny zaczynaja sie tuz za nia.
  const hourRight = Math.max(...hourWords.map((w) => w.x1)) + 3;

  const headerWords = words.filter(
    (w) => w !== godzina && w.x0 > godzina.x1 && w.y0 >= godzina.y0 - ROW_TOL && w.y0 < hourWords[0].y0 - 2,
  );
  const columns = buildColumns(headerWords, pageNo);
  const departures = Object.fromEntries(SERVICES.map((s) => [s, []]));

  for (const hourWord of hourWords) {
    const yc = (hourWord.y0 + hourWord.y1) / 2;
    const rowWords = words.filter((w) => w.x0 > hourRight && Math.abs((w.y0 + w.y1) / 2 - yc) < 5);
    const seen = new Set();
    for (const cell of groupCells(rowWords)) {
      const column = nearestColumn(columns, cell.x0);
      if (seen.has(column.service)) throw new PageError(pageNo, `dwie komorki w kolumnie "${column.label}", godzina ${hourWord.t}`);
      seen.add(column.service);
      departures[column.service].push(...parseCell(cell.text, Number(hourWord.t), pageNo));
    }
  }
  const lastRow = hourWords.at(-1);
  return { columns_raw: columns.map((c) => c.label), departures, legend: parseLegend(words, lastRow) };
}

export function parsePage(words, pageNo) {
  return { page_line: detectLine(words), ...parseHeader(words, pageNo), ...parseTable(words, pageNo) };
}

function validateDocument(records, meta) {
  const fail = (msg) => {
    throw new Error(`${meta.source}: ${msg}`);
  };
  if (records.length === 0) fail('PDF bez stron');
  for (const r of records) {
    if (r.direction !== records[0].direction) fail(`rozny kierunek na stronie ${r.seq}: "${r.direction}" vs "${records[0].direction}"`);
    if (r.valid_from !== records[0].valid_from) fail(`rozna data obowiazywania na stronie ${r.seq}`);
    if (r.page_line && r.page_line !== meta.line) fail(`numer linii na stronie ${r.seq} ("${r.page_line}") != oczekiwany "${meta.line}"`);
  }
}

export function extractPdf(pdfPath, meta) {
  const pages = parseBbox(pdftotextBbox(pdfPath));
  const records = pages.map((words, i) => {
    const seq = i + 1;
    const parsed = parsePage(words, seq);
    return { line: meta.line, seq, source: meta.source, ...parsed };
  });
  validateDocument(records, meta);
  return records;
}

export function extractManifest(manifest) {
  return manifest.flatMap((entry) => {
    const source = path.basename(entry.file);
    const records = extractPdf(path.join(DIRS.raw, entry.file), { line: entry.line, source });
    console.log(`extract: ${source} -> ${records.length} stron, kierunek "${records[0].direction}"`);
    return records.map(({ page_line, ...rest }) => rest);
  });
}
