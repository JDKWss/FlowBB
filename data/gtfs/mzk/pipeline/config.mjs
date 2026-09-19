import path from 'node:path';
import { fileURLToPath } from 'node:url';

export const ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
export const DIRS = {
  raw: path.join(ROOT, 'raw'),
  cache: path.join(ROOT, 'cache'),
  parsed: path.join(ROOT, 'parsed'),
  sql: path.join(ROOT, 'sql'),
};

export const LIST_URL = 'https://komunikacja.bielsko-biala.pl/index.php/rozklad-jazdy-do-wydruku/';
export const USER_AGENT = 'FlowBB hackathon research';

// Serwer MZK wysyla niekompletny lancuch TLS (brak posredniego Sectigo DV R36).
export const INTERMEDIATE_CERT_URL = 'http://crt.sectigo.com/SectigoPublicServerAuthenticationCADVR36.crt';
export const SYSTEM_CA_BUNDLE = '/etc/ssl/certs/ca-certificates.crt';

export const DEFAULT_LINES = ['4', '7', '16', 'N1', 'N2'];

// Naglowek kolumny w PDF -> typ rozkladu. Nieznany naglowek to blad, nie ostrzezenie.
export const SERVICE_BY_HEADER = {
  'Dni Robocze': 'weekday',
  'Dni Robocze wakacyjne': 'weekday_holiday',
  Soboty: 'saturday',
  'Niedziele i Święta': 'sunday',
};

export const SERVICES = ['weekday', 'weekday_holiday', 'saturday', 'sunday'];
