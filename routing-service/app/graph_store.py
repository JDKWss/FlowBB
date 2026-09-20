from __future__ import annotations

import hashlib
import json
import logging
import time
from pathlib import Path
from typing import Any

import networkx as nx
import osmnx as ox

from app.config import Settings
from app.models import RoutingMode

LOGGER = logging.getLogger("uvicorn.error")
REQUIRED_MODES = (RoutingMode.WALKING, RoutingMode.BIKE, RoutingMode.CAR)


class GraphStore:
    def __init__(self, settings: Settings) -> None:
        self.settings = settings
        self.graphs: dict[RoutingMode, nx.MultiDiGraph] = {}
        self.artifact_version: str | None = None
        self.load_error: str | None = None
        self.load_milliseconds: float | None = None

    @property
    def ready(self) -> bool:
        return all(mode in self.graphs for mode in REQUIRED_MODES)

    def load(self) -> None:
        started = time.perf_counter()
        try:
            manifest = self._read_manifest()
            loaded = {mode: self._load_mode(manifest, mode) for mode in REQUIRED_MODES}
            self.graphs = loaded
            self.artifact_version = str(manifest["artifactVersion"])
            self.load_error = None
            self.load_milliseconds = (time.perf_counter() - started) * 1000
            LOGGER.info(
                "routing_graphs_loaded artifact_version=%s elapsed_ms=%.1f",
                self.artifact_version,
                self.load_milliseconds,
            )
        except Exception as error:  # startup must remain alive and report not-ready
            self.graphs = {}
            self.artifact_version = None
            self.load_error = type(error).__name__
            self.load_milliseconds = (time.perf_counter() - started) * 1000
            LOGGER.error("routing_graph_load_failed category=%s", self.load_error)

    def graph_for(self, mode: RoutingMode) -> nx.MultiDiGraph:
        return self.graphs[mode]

    @classmethod
    def loaded_for_tests(
        cls, settings: Settings, graphs: dict[RoutingMode, nx.MultiDiGraph]
    ) -> "GraphStore":
        store = cls(settings)
        store.graphs = graphs
        store.artifact_version = "synthetic-test-v1"
        store.load_milliseconds = 0.0
        return store

    def _read_manifest(self) -> dict[str, Any]:
        path = self.settings.data_dir / "manifest.json"
        with path.open(encoding="utf-8") as handle:
            manifest: dict[str, Any] = json.load(handle)
        if not manifest.get("artifactVersion") or not isinstance(manifest.get("files"), dict):
            raise ValueError("Invalid routing artifact manifest")
        return manifest

    def _load_mode(self, manifest: dict[str, Any], mode: RoutingMode) -> nx.MultiDiGraph:
        metadata = manifest["files"].get(mode.value)
        if not isinstance(metadata, dict):
            raise ValueError(f"Missing manifest entry for {mode.value}")
        filename = metadata.get("filename")
        expected_checksum = metadata.get("sha256")
        if not isinstance(filename, str) or not isinstance(expected_checksum, str):
            raise ValueError(f"Invalid manifest entry for {mode.value}")
        path = self.settings.data_dir / filename
        if not path.is_file() or _sha256(path) != expected_checksum:
            raise ValueError(f"Missing or invalid graph artifact for {mode.value}")
        graph = ox.load_graphml(path)
        if graph.number_of_nodes() == 0 or graph.number_of_edges() == 0:
            raise ValueError(f"Empty graph artifact for {mode.value}")
        return graph


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()
