from __future__ import annotations

import copy

import pytest


@pytest.mark.parametrize("mode", ["Walking", "Bike", "Car"])
def test_each_road_mode_returns_route(client, route_payload, mode: str) -> None:
    payload = copy.deepcopy(route_payload)
    payload["mode"] = mode

    response = client.post("/route", json=payload)

    assert response.status_code == 200
    result = response.json()
    assert result["mode"] == mode
    assert result["distanceMeters"] == 330.0
    assert result["durationSeconds"] > 0
    assert result["steps"] == []


def test_repeated_route_is_deterministic(client, route_payload) -> None:
    first = client.post("/route", json=route_payload)
    second = client.post("/route", json=route_payload)

    assert first.json() == second.json()


def test_invalid_coordinates_use_stable_error(client, route_payload) -> None:
    route_payload["origin"]["latitude"] = 91

    response = client.post("/route", json=route_payload)

    assert response.status_code == 422
    assert response.json()["code"] == "INVALID_INPUT"


def test_public_transport_is_rejected(client, route_payload) -> None:
    route_payload["mode"] = "PublicTransport"

    response = client.post("/route", json=route_payload)

    assert response.status_code == 422
    assert response.json()["code"] == "UNSUPPORTED_MODE"


def test_unknown_mode_is_rejected(client, route_payload) -> None:
    route_payload["mode"] = "Unknown"

    response = client.post("/route", json=route_payload)

    assert response.status_code == 422
    assert response.json()["code"] == "UNSUPPORTED_MODE"


def test_snap_distance_is_limited(client, route_payload) -> None:
    route_payload["origin"] = {"latitude": 50.0, "longitude": 20.0}

    response = client.post("/route", json=route_payload)

    assert response.status_code == 422
    assert response.json()["code"] == "SNAP_TOO_FAR"


def test_disconnected_points_return_route_not_found(client, route_payload) -> None:
    route_payload["destination"] = {"latitude": 49.0100, "longitude": 19.0100}

    response = client.post("/route", json=route_payload)

    assert response.status_code == 404
    assert response.json()["code"] == "ROUTE_NOT_FOUND"


def test_unexpected_graph_data_failure_uses_stable_error(client, ready_store, route_payload) -> None:
    graph = ready_store.graphs[next(iter(ready_store.graphs))]
    del graph["a"]["b"][0]["length"]

    response = client.post("/route", json=route_payload)

    assert response.status_code == 500
    assert response.json() == {
        "code": "ROUTING_FAILED",
        "message": "The route could not be calculated.",
    }
