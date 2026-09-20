import path from 'node:path';
import { DIRS, ROOT } from './config.mjs';
import { pad2, readJson, ensureDir, writeJsonLines } from './util.mjs';

const FIXED_HOLIDAYS = ['01-01', '01-06', '05-01', '05-03', '08-15', '11-01', '11-11', '12-25', '12-26'];

const iso = (d) => `${d.getUTCFullYear()}-${pad2(d.getUTCMonth() + 1)}-${pad2(d.getUTCDate())}`;
const addDays = (d, n) => new Date(d.getTime() + n * 86400000);

// Wielkanoc (algorytm Meeusa/Jonesa/Butchera), data UTC.
export function easterSunday(year) {
  const a = year % 19;
  const b = Math.floor(year / 100);
  const c = year % 100;
  const d = Math.floor(b / 4);
  const e = b % 4;
  const f = Math.floor((b + 8) / 25);
  const g = Math.floor((b - f + 1) / 3);
  const h = (19 * a + b - d - g + 15) % 30;
  const i = Math.floor(c / 4);
  const k = c % 4;
  const l = (32 + 2 * e + 2 * i - h - k) % 7;
  const m = Math.floor((a + 11 * h + 22 * l) / 451);
  const month = Math.floor((h + l - 7 * m + 114) / 31);
  const day = ((h + l - 7 * m + 114) % 31) + 1;
  return new Date(Date.UTC(year, month - 1, day));
}

export function publicHolidays(year) {
  const easter = easterSunday(year);
  const holidays = new Map(FIXED_HOLIDAYS.map((md) => [`${year}-${md}`, 'swieto stale']));
  holidays.set(iso(easter), 'Wielkanoc');
  holidays.set(iso(addDays(easter, 1)), 'Poniedzialek Wielkanocny');
  holidays.set(iso(addDays(easter, 49)), 'Zielone Swiatki');
  holidays.set(iso(addDays(easter, 60)), 'Boze Cialo');
  return holidays;
}

const inRanges = (day, ranges) => ranges.some(([from, to]) => day >= from && day <= to);

// service: dzien swiateczny = rozklad niedzielny; "weekday_holiday" tylko gdy skonfigurowano wakacje.
export function buildCalendarDays(config) {
  const days = [];
  const extra = new Set(config.extra_holidays ?? []);
  const ranges = config.school_holiday_ranges ?? [];
  const holidaysByYear = new Map();
  const end = new Date(`${config.valid_to}T00:00:00Z`);
  for (let d = new Date(`${config.valid_from}T00:00:00Z`); d <= end; d = addDays(d, 1)) {
    const year = d.getUTCFullYear();
    if (!holidaysByYear.has(year)) holidaysByYear.set(year, publicHolidays(year));
    const day = iso(d);
    const holidayNote = holidaysByYear.get(year).get(day) ?? (extra.has(day) ? 'dodatkowe swieto z konfiguracji' : null);
    const dow = d.getUTCDay();
    let service;
    if (holidayNote || dow === 0) service = 'sunday';
    else if (dow === 6) service = 'saturday';
    else service = inRanges(day, ranges) ? 'weekday_holiday' : 'weekday';
    days.push({ day, service, public_holiday: Boolean(holidayNote), note: holidayNote });
  }
  return days;
}

export function writeCalendar() {
  const config = readJson(path.join(ROOT, 'calendar_config.json'));
  const days = buildCalendarDays(config);
  ensureDir(DIRS.parsed);
  writeJsonLines(path.join(DIRS.parsed, 'calendar_days.json'), days);
  console.log(`calendar: ${days.length} dni (${config.valid_from} .. ${config.valid_to}), swieta: ${days.filter((d) => d.public_holiday).length}`);
  return days;
}
