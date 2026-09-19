#!/usr/bin/env node
// Pobiera wydarzenia z Teatru Polskiego, Cavatina Hall i bb2026.pl
// i zapisuje je w jednym, znormalizowanym pliku JSON.
//
// Uzycie (Node 18+, bez zaleznosci):
//   node data/seed/ingest-events.mjs [--out data/seed/events.seed.json] [--partial] [--all]
//
//   --out      plik wyjsciowy (domyslnie data/seed/events.seed.json)
//   --partial  zapisz wynik nawet gdy ktores zrodlo zawiodlo (domyslnie: blad, bez zapisu)
//   --all      nie odrzucaj wydarzen z przeszlosci
//
// Ksztalt rekordu to propozycja - kontrakt (`contracts/openapi.yaml`) zatwierdza Backend/Core Lead.

import { mkdir, writeFile } from "node:fs/promises";
import { dirname } from "node:path";

const TZ = "Europe/Warsaw";
const UA = "Mozilla/5.0 (FlowBB hackathon research)";
const TIMEOUT_MS = 30_000;

const args = process.argv.slice(2);
const flag = (name) => args.includes(name);
const outPath = args.includes("--out") ? args[args.indexOf("--out") + 1] : "data/seed/events.seed.json";
const keepPast = flag("--all");
const allowPartial = flag("--partial");
const now = new Date();

// ---------- pomocnicze ----------

async function getJson(url) {
  const res = await fetch(url, { headers: { "User-Agent": UA, Accept: "application/json" }, signal: AbortSignal.timeout(TIMEOUT_MS) });
  if (!res.ok) throw new Error(`${res.status} ${res.statusText} dla ${url}`);
  return { body: await res.json(), headers: res.headers };
}

const NAMED_ENTITIES = { amp: "&", lt: "<", gt: ">", quot: '"', apos: "'", nbsp: " ", ndash: "–", mdash: "—", hellip: "…", laquo: "«", raquo: "»" };
function decodeHtml(s) {
  return String(s ?? "")
    .replace(/<[^>]+>/g, "")
    .replace(/&#(\d+);/g, (_, n) => String.fromCodePoint(Number(n)))
    .replace(/&#x([0-9a-f]+);/gi, (_, n) => String.fromCodePoint(parseInt(n, 16)))
    .replace(/&([a-z]+);/gi, (m, n) => NAMED_ENTITIES[n.toLowerCase()] ?? m)
    .replace(/\s+/g, " ")
    .trim();
}

const warsawParts = new Intl.DateTimeFormat("en-CA", {
  timeZone: TZ, hourCycle: "h23", year: "numeric", month: "2-digit", day: "2-digit", hour: "2-digit", minute: "2-digit", second: "2-digit",
});
function partsInWarsaw(date) {
  const p = Object.fromEntries(warsawParts.formatToParts(date).map((x) => [x.type, x.value]));
  return { y: +p.year, mo: +p.month, d: +p.day, h: +p.hour, mi: +p.minute, s: +p.second };
}
function offsetString(minutes) {
  const sign = minutes >= 0 ? "+" : "-";
  const abs = Math.abs(minutes);
  return `${sign}${String(Math.floor(abs / 60)).padStart(2, "0")}:${String(abs % 60).padStart(2, "0")}`;
}

// Instant (Date) -> "2026-09-27T19:00:00+02:00" w strefie Warszawy.
function instantToWarsawIso(date) {
  const p = partsInWarsaw(date);
  const asUtc = Date.UTC(p.y, p.mo - 1, p.d, p.h, p.mi, p.s);
  const offsetMin = Math.round((asUtc - Math.floor(date.getTime() / 1000) * 1000) / 60000);
  const pad = (n) => String(n).padStart(2, "0");
  return `${p.y}-${pad(p.mo)}-${pad(p.d)}T${pad(p.h)}:${pad(p.mi)}:${pad(p.s)}${offsetString(offsetMin)}`;
}

// Lokalny czas scienny w Warszawie ("2026-11-05 19:00[:00]") -> ISO ze strefa.
function warsawLocalToIso(local) {
  const m = /^(\d{4})-(\d{2})-(\d{2})[ T](\d{2}):(\d{2})(?::(\d{2}))?/.exec(local ?? "");
  if (!m) return null;
  const [y, mo, d, h, mi, s] = [+m[1], +m[2], +m[3], +m[4], +m[5], +(m[6] ?? 0)];
  const wall = Date.UTC(y, mo - 1, d, h, mi, s);
  // Zgaduj offset z lokalnego czasu, potem popraw (obsluguje zmiane czasu).
  let guess = wall - 2 * 3600_000;
  for (let i = 0; i < 2; i++) {
    const p = partsInWarsaw(new Date(guess));
    guess -= Date.UTC(p.y, p.mo - 1, p.d, p.h, p.mi, p.s) - wall;
  }
  return instantToWarsawIso(new Date(guess));
}

const isFuture = (iso) => keepPast || !iso || new Date(iso) >= now;

// ---------- Teatr Polski ----------

async function fetchTeatr() {
  const { body } = await getJson("https://teatr.bielsko.pl/api/repertoire");
  return (body.events ?? []).map((e) => ({
    source: "teatr-polski",
    externalId: e.repertoireEventId,
    title: decodeHtml(e.title),
    startAt: instantToWarsawIso(new Date(e.date)), // API zwraca UTC
    endAt: e.duration ? instantToWarsawIso(new Date(new Date(e.date).getTime() + e.duration * 60_000)) : null,
    venueName: decodeHtml(e.stage?.name),
    address: null,
    categories: (e.repertoireCategories ?? []).map((c) => (typeof c === "string" ? c : c.name ?? c.title ?? JSON.stringify(c))),
    url: e.showEvent?.slug ? `https://teatr.bielsko.pl/spektakl/${e.showEvent.slug}` : null,
    capacity: e.capacity ?? null,
    freeSeats: e.freeSeats ?? null,
    status: e.status ?? null,
    allDay: false,
    multiDay: false,
  }));
}

// ---------- Cavatina Hall ----------

async function fetchCavatina() {
  const base = "https://cavatinahall.pl/wp-json/wp/v2/events?per_page=100&_fields=id,title,link,acf,event_category";
  const out = [];
  let totalPages = 1;
  for (let page = 1; page <= totalPages; page++) {
    const { body, headers } = await getJson(`${base}&page=${page}`);
    totalPages = Number(headers.get("x-wp-totalpages") ?? 1);
    for (const e of body) {
      const startAt = warsawLocalToIso(e.acf?.event_datetime); // `date` to data publikacji - nie uzywac
      if (!startAt) continue; // wpisy bez terminu
      if (e.link?.includes("/en/")) continue; // angielskie tlumaczenia tych samych wydarzen (duplikaty)
      out.push({
        source: "cavatina-hall",
        externalId: String(e.id),
        title: decodeHtml(e.title?.rendered),
        startAt,
        endAt: null,
        venueName: "Cavatina Hall",
        address: null,
        categories: (e.event_category ?? []).map(String), // id termow WP, bez nazw
        url: e.link ?? null,
        capacity: null,
        freeSeats: null,
        status: null,
        allDay: false,
        multiDay: false,
      });
    }
  }
  return out;
}

// ---------- bb2026.pl (The Events Calendar) ----------

async function fetchBb2026() {
  const today = instantToWarsawIso(now).slice(0, 10);
  let url = `https://bb2026.pl/wp-json/tribe/events/v1/events?per_page=50&start_date=${today}`;
  const out = [];
  while (url) {
    const { body } = await getJson(url);
    for (const e of body.events ?? []) {
      const venue = Array.isArray(e.venue) ? null : e.venue; // puste miejsce = []
      const startAt = warsawLocalToIso(e.start_date);
      const endAt = warsawLocalToIso(e.end_date);
      out.push({
        source: "bb2026",
        externalId: String(e.id),
        title: decodeHtml(e.title),
        startAt,
        endAt,
        venueName: venue?.venue ? decodeHtml(venue.venue) : null,
        address: venue?.address ? decodeHtml([venue.address, venue.city].filter(Boolean).join(", ")) : null,
        categories: (e.categories ?? []).map((c) => decodeHtml(c.name)),
        url: e.url ?? null,
        capacity: null,
        freeSeats: null,
        status: null,
        allDay: Boolean(e.all_day),
        multiDay: Boolean(startAt && endAt && startAt.slice(0, 10) !== endAt.slice(0, 10)),
      });
    }
    url = body.next_rest_url ?? null;
  }
  return out;
}

// ---------- uruchomienie ----------

const sources = { "teatr-polski": fetchTeatr, "cavatina-hall": fetchCavatina, bb2026: fetchBb2026 };
const events = [];
const report = {};
let failed = false;

for (const [name, fetcher] of Object.entries(sources)) {
  try {
    const all = await fetcher();
    const kept = all.filter((e) => isFuture(e.startAt));
    events.push(...kept);
    report[name] = { fetched: all.length, kept: kept.length };
  } catch (err) {
    failed = true;
    report[name] = { error: String(err.message ?? err) };
  }
}

events.sort((a, b) => (a.startAt < b.startAt ? -1 : a.startAt > b.startAt ? 1 : 0));
console.table(report);

if (failed && !allowPartial) {
  console.error("Co najmniej jedno zrodlo zawiodlo - nic nie zapisano. Uzyj --partial, aby zapisac czesciowy wynik.");
  process.exit(1);
}

await mkdir(dirname(outPath), { recursive: true });
await writeFile(
  outPath,
  JSON.stringify({ fetchedAt: instantToWarsawIso(now), timezone: TZ, note: "Terminy realne, popyt syntetyczny (DEMO DATA / SYMULACJA).", sources: report, events }, null, 2) + "\n",
);
console.log(`Zapisano ${events.length} wydarzen do ${outPath}`);
