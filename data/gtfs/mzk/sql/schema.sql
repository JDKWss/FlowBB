-- PoC: rozklady MZK Bielsko-Biala (docs/mzk-pipeline-implementation.md, sekcja 6.1, poziom 1).
-- Wersja produkcyjna powinna powstac jako migracja EF Core (wlasciciel: Data Lead + Backend/Core Lead).
CREATE EXTENSION IF NOT EXISTS postgis;

DROP TABLE IF EXISTS transit_departure, transit_source, transit_calendar_day, transit_stop, transit_line_direction CASCADE;

CREATE TABLE transit_line_direction (
  line         text     NOT NULL,
  direction_id smallint NOT NULL,
  headsign     text     NOT NULL,
  valid_from   date     NOT NULL,
  PRIMARY KEY (line, direction_id)
);

CREATE TABLE transit_stop (
  stop_id        text PRIMARY KEY,            -- slug(nazwa)_tabliczka, np. szyndzielnia_13
  name           text NOT NULL,
  plate          text,                        -- NULL, gdy PDF nie podaje numeru tabliczki
  geom           geometry(Point, 4326),       -- NULL: wspolrzedne dopasowuje osobny krok (OSM)
  osm_id         bigint,
  match_score    real,
  match          text NOT NULL DEFAULT 'missing' CHECK (match IN ('auto', 'override', 'missing')),
  transfer_group text
);
CREATE INDEX transit_stop_geom_gix ON transit_stop USING gist (geom);

CREATE TABLE transit_source (
  source_id     integer PRIMARY KEY,
  url           text UNIQUE NOT NULL,
  sha256        char(64) NOT NULL,
  last_modified timestamptz,
  uploads_date  text,
  valid_from    date,
  line          text NOT NULL,
  direction_id  smallint NOT NULL,
  superseded    boolean NOT NULL DEFAULT false,
  FOREIGN KEY (line, direction_id) REFERENCES transit_line_direction
);

CREATE TABLE transit_departure (
  departure_id bigserial PRIMARY KEY,
  source_id    integer  NOT NULL REFERENCES transit_source,
  line         text     NOT NULL,
  direction_id smallint NOT NULL,
  stop_seq     smallint NOT NULL,             -- numer strony w PDF = kolejnosc trasy
  stop_id      text     NOT NULL REFERENCES transit_stop,
  service      text     NOT NULL CHECK (service IN ('weekday', 'weekday_holiday', 'saturday', 'sunday')),
  dep_sec      integer  NOT NULL,             -- sekundy od polnocy jak w PDF (linie nocne: godziny 0-3 = po polnocy)
  flags        text[]   NOT NULL DEFAULT '{}',
  depot_run    boolean  NOT NULL DEFAULT false,
  FOREIGN KEY (line, direction_id) REFERENCES transit_line_direction,
  UNIQUE (source_id, stop_seq, service, dep_sec)
);
CREATE INDEX transit_departure_lookup ON transit_departure (stop_id, service, dep_sec) WHERE NOT depot_run;

CREATE TABLE transit_calendar_day (
  day            date PRIMARY KEY,
  service        text NOT NULL CHECK (service IN ('weekday', 'weekday_holiday', 'saturday', 'sunday')),
  public_holiday boolean NOT NULL,
  note           text
);
