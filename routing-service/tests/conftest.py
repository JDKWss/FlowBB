from __future__ import annotations

import networkx as nx
import pytest
from fastapi.testclient import TestClient
from shapely.geometry import LineString

from app.config import Settings
from app.graph_store import GraphStore
from app.main import create_app
from app.models import RoutingMode


def synthetic_graph() -> nx.MultiDiGraph:
    graph = nx.MultiDiGraph(crs="epsg:4326")
    graph.add_node("a", x=19.0000, y=49.0000)
    graph.add_node("b", x=19.0010, y=49.0010)
    graph.add_node("c", x=19.0020, y=49.0020)
    graph.add_node("isolated", x=19.0100, y=49.0100)
    graph.add_edge(
        "a",
        "b",
        length=160.0,
        travel_time=24.0,
        geometry=LineString([(19.0000, 49.0000), (19.0004, 49.0007), (19.0010, 49.0010)]),
    )
    graph.add_edge(
        "b",
        "c",
        length=170.0,
        travel_time=26.0,
        geometry=LineString([(19.0020, 49.0020), (19.0015, 49.0012), (19.0010, 49.0010)]),
    )
    return graph


@pytest.fixture
def settings(tmp_path) -> Settings:
    return Settings(data_dir=tmp_path, max_snap_meters=250.0)


@pytest.fixture
def ready_store(settings: Settings) -> GraphStore:
    graphs = {mode: synthetic_graph() for mode in RoutingMode}
    return GraphStore.loaded_for_tests(settings, graphs)


@pytest.fixture
def client(settings: Settings, ready_store: GraphStore):
    app = create_app(settings, ready_store, load_graphs=False)
    with TestClient(app) as test_client:
        yield test_client


@pytest.fixture
def route_payload() -> dict[str, object]:
    return {
        "origin": {"latitude": 49.0000, "longitude": 19.0000},
        "destination": {"latitude": 49.0020, "longitude": 19.0020},
        "mode": "Walking",
    }
