from fastapi.testclient import TestClient

from app.config import Settings
from app.graph_store import GraphStore
from app.main import create_app


def test_health_reports_ready(client) -> None:
    response = client.get("/health")

    assert response.status_code == 200
    assert response.json() == {
        "status": "ready",
        "processAlive": True,
        "graphLoaded": True,
        "artifactVersion": "synthetic-test-v1",
        "modes": ["Walking", "Bike", "Car"],
    }


def test_health_and_route_report_not_ready(settings: Settings) -> None:
    app = create_app(settings, GraphStore(settings), load_graphs=False)
    with TestClient(app) as client:
        health = client.get("/health")
        route = client.post(
            "/route",
            json={
                "origin": {"latitude": 49.0, "longitude": 19.0},
                "destination": {"latitude": 49.1, "longitude": 19.1},
                "mode": "Walking",
            },
        )

    assert health.json()["status"] == "not_ready"
    assert health.json()["graphLoaded"] is False
    assert route.status_code == 503
    assert route.json()["code"] == "GRAPH_NOT_READY"
