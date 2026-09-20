"""Golden routes na prawdziwych grafach Bielska-Bialej (bramka ADR 002, issue #127).

Testy sa pomijane bez ``ROUTING_REAL_DATA_DIR``: wymagaja artefaktow z ``routing-prepare`` (OSMnx/Overpass), ktorych
CI nie pobiera. Uruchomienie:

    docker build --target test -t flowbb-routing-test routing-service
    docker run --rm -v <wolumen-routing-data>:/data:ro -e ROUTING_REAL_DATA_DIR=/data flowbb-routing-test \
        python -m pytest -q tests/test_real_graphs.py

Oczekiwane dystanse maja tolerancje, bo OSM zmienia sie miedzy generacjami artefaktu.
"""
from __future__ import annotations

import os
from pathlib import Path

import pytest
from fastapi.testclient import TestClient

from app.config import Settings
from app.main import create_app

DATA_DIR = os.getenv("ROUTING_REAL_DATA_DIR")

pytestmark = pytest.mark.skipif(not DATA_DIR, reason="Brak ROUTING_REAL_DATA_DIR: test wymaga prawdziwych grafow.")

RYNEK = (49.82245, 19.04431)
DWORZEC = (49.8098, 19.0460)
LIPNIK = (49.8006, 19.0089)
OUTSIDE_AREA = (50.2, 19.0)

# (para, tryb, oczekiwany dystans w metrach) z pomiaru na artefakcie bielsko-2026-09-20-r6000.
GOLDEN = [
    (RYNEK, DWORZEC, "Walking", 1486.0),
    (RYNEK, DWORZEC, "Bike", 2088.0),
    (RYNEK, DWORZEC, "Car", 1725.0),
    (RYNEK, LIPNIK, "Walking", 4170.0),
    (RYNEK, LIPNIK, "Bike", 4339.0),
    (RYNEK, LIPNIK, "Car", 4398.0),
]
TOLERANCE = 0.10


@pytest.fixture(scope="module")
def real_client():
    settings = Settings(data_dir=Path(DATA_DIR))
    with TestClient(create_app(settings)) as client:
        yield client


def route(client: TestClient, origin, destination, mode: str):
    return client.post(
        "/route",
        json={
            "origin": {"latitude": origin[0], "longitude": origin[1]},
            "destination": {"latitude": destination[0], "longitude": destination[1]},
            "mode": mode,
        },
    )


def test_health_reports_ready_with_all_modes(real_client) -> None:
    body = real_client.get("/health").json()

    assert body["status"] == "ready"
    assert body["modes"] == ["Walking", "Bike", "Car"]


@pytest.mark.parametrize(("origin", "destination", "mode", "expected"), GOLDEN)
def test_golden_route_distance_is_within_tolerance(real_client, origin, destination, mode, expected) -> None:
    response = route(real_client, origin, destination, mode)

    assert response.status_code == 200
    assert response.json()["distanceMeters"] == pytest.approx(expected, rel=TOLERANCE)


@pytest.mark.parametrize(("origin", "destination", "mode", "expected"), GOLDEN[:3])
def test_repeated_requests_are_identical_and_use_lon_lat_order(real_client, origin, destination, mode, expected) -> None:
    first = route(real_client, origin, destination, mode).json()
    second = route(real_client, origin, destination, mode).json()

    assert first == second
    longitude, latitude = first["geometry"]["coordinates"][0]
    assert 18 < longitude < 20
    assert 49 < latitude < 51


def test_car_route_respects_one_way_streets(real_client) -> None:
    forward = route(real_client, RYNEK, DWORZEC, "Car").json()
    backward = route(real_client, DWORZEC, RYNEK, "Car").json()

    assert forward["distanceMeters"] != backward["distanceMeters"]


def test_modes_use_different_graphs(real_client) -> None:
    geometries = {
        mode: route(real_client, RYNEK, DWORZEC, mode).json()["geometry"]["coordinates"]
        for mode in ("Walking", "Bike", "Car")
    }

    assert geometries["Walking"] != geometries["Bike"]
    assert geometries["Bike"] != geometries["Car"]


def test_destination_outside_area_is_rejected_by_snapping(real_client) -> None:
    response = route(real_client, RYNEK, OUTSIDE_AREA, "Walking")

    assert response.status_code == 422
    assert response.json()["code"] == "SNAP_TOO_FAR"
