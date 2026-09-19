import { execFileSync } from 'node:child_process';
import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import {
  DIRS,
  INTERMEDIATE_CERT_URL,
  LIST_URL,
  SYSTEM_CA_BUNDLE,
  USER_AGENT,
} from './config.mjs';
import { decodeEntities, ensureDir, readJson } from './util.mjs';

const bundlePath = () => path.join(DIRS.cache, 'bundle.pem');

function curl(args) {
  return execFileSync('curl', ['-sSfL', '-A', USER_AGENT, ...args], {
    encoding: 'utf8',
    maxBuffer: 64 * 1024 * 1024,
  });
}

// Bundle = systemowe CA + posredni certyfikat Sectigo. Weryfikacja TLS zostaje wlaczona.
export function ensureBundle() {
  const target = bundlePath();
  if (fs.existsSync(target)) return target;
  ensureDir(DIRS.cache);
  const der = path.join(DIRS.cache, 'intermediate.crt');
  const pem = path.join(DIRS.cache, 'intermediate.pem');
  curl(['-o', der, INTERMEDIATE_CERT_URL]);
  execFileSync('openssl', ['x509', '-inform', 'DER', '-in', der, '-out', pem]);
  fs.writeFileSync(target, fs.readFileSync(SYSTEM_CA_BUNDLE, 'utf8') + fs.readFileSync(pem, 'utf8'));
  return target;
}

function cleanText(html) {
  return decodeEntities(html.replace(/<[^>]+>/g, '')).replace(/­/g, '').replace(/\s+/g, ' ').trim();
}

// Sekcje strony: <div class="wp-block-media-text"> z obrazkiem (alt="7o") i linkami do PDF.
export function parseListing(html) {
  const sections = html.split('wp-block-media-text alignwide').slice(1);
  const entries = [];
  for (const section of sections) {
    const alt = section.match(/<img[^>]*\salt="([^"]+)"/)?.[1];
    if (!alt) continue;
    const line = alt.replace(/o$/, '');
    for (const a of section.matchAll(/<a[^>]*href="([^"]+\.pdf)"[^>]*>([\s\S]*?)<\/a>/g)) {
      const text = cleanText(a[2]);
      const isDirection = /^Kierunek:/i.test(text);
      entries.push({
        line,
        url: a[1],
        kind: isDirection ? 'kier' : 'pozostale',
        label: isDirection ? text.replace(/^Kierunek:\s*/i, '') : null,
        uploads_date: a[1].match(/\/uploads\/(\d{4}\/\d{2})\//)?.[1] ?? null,
      });
    }
  }
  return entries;
}

// Dla tego samego (linia, cel) wygrywa plik z pozniejsza data w sciezce uploads.
export function dropSuperseded(entries) {
  const best = new Map();
  for (const e of entries) {
    const key = `${e.line}|${e.label}`;
    const prev = best.get(key);
    if (!prev || (e.uploads_date ?? '') > (prev.uploads_date ?? '')) best.set(key, e);
  }
  return entries.filter((e) => best.get(`${e.line}|${e.label}`) === e);
}

function sha256(file) {
  return crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
}

function download(entry, bundle) {
  const file = path.join(DIRS.raw, path.basename(new URL(entry.url).pathname));
  const headers = path.join(DIRS.cache, 'last-headers.txt');
  // -z: pobierz tylko jesli zdalny plik jest nowszy; -R: ustaw mtime z Last-Modified.
  const timeCond = fs.existsSync(file) ? ['-z', file] : [];
  curl(['--cacert', bundle, '-R', '-D', headers, ...timeCond, '-o', file, entry.url]);
  const lastModified = fs.readFileSync(headers, 'utf8').match(/^last-modified:\s*(.+)$/im)?.[1]?.trim() ?? null;
  return { file, lastModified };
}

export function fetchLines(lines) {
  ensureDir(DIRS.raw);
  ensureDir(DIRS.parsed);
  const bundle = ensureBundle();
  const html = curl(['--cacert', bundle, LIST_URL]);
  const all = parseListing(html);
  if (all.length === 0) throw new Error(`Nie znaleziono zadnych PDF-ow na ${LIST_URL} (zmiana strony?)`);

  const wanted = dropSuperseded(all.filter((e) => e.kind === 'kier' && lines.includes(e.line)));
  const missing = lines.filter((l) => !wanted.some((e) => e.line === l));
  if (missing.length) throw new Error(`Brak PDF-ow dla linii: ${missing.join(', ')}`);

  const manifest = wanted.map((entry) => {
    const { file, lastModified } = download(entry, bundle);
    return { ...entry, file: path.relative(DIRS.raw, file), sha256: sha256(file), last_modified: lastModified };
  });
  fs.writeFileSync(path.join(DIRS.parsed, 'manifest.json'), `${JSON.stringify(manifest, null, 2)}\n`);
  console.log(`fetch: ${manifest.length} PDF-ow (z ${all.length} na stronie), linie: ${lines.join(', ')}`);
  return manifest;
}

export const readManifest = () => readJson(path.join(DIRS.parsed, 'manifest.json'));
