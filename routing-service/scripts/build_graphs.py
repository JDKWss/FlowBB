from __future__ import annotations

import argparse
import hashlib
import json
import sys
from datetime import datetime, timezone
from importlib.metadata import version
from pathlib import Path

import networkx as nx
import osmnx as ox

CENTER_LATITUDE = 49.8176
CENTER_LONGITUDE = 19.0391
DEFAULT_RADIUS_METERS = 6_000
NETWORK_TYPES = {"Walking": "walk", "Bike": "bike", "Car": "drive"}
FILENAMES = {"Walking": "walk.graphml", "Bike": "bike.graphml", "Car": "drive.graphml"}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build FlowBB OSM routing graph artifacts.")
    parser.add_argument("--output", type=Path, default=Path("/app/data"))
    parser.add_argument("--radius-meters", type=int, default=DEFAULT_RADIUS_METERS)
    return parser.parse_args()


def build_graph(mode: str, radius_meters: int) -> nx.MultiDiGraph:
    graph = ox.graph_from_point(
        (CENTER_LATITUDE, CENTER_LONGITUDE),
        dist=radius_meters,
        dist_type="bbox",
        network_type=NETWORK_TYPES[mode],
        simplify=True,
        retain_all=False,
        truncate_by_edge=True,
    )
    if mode == "Car":
        graph = ox.routing.add_edge_speeds(graph)
        graph = ox.routing.add_edge_travel_times(graph)
    return graph


def main() -> int:
    args = parse_args()
    if args.radius_meters <= 0:
        raise ValueError("--radius-meters must be greater than zero")
    args.output.mkdir(parents=True, exist_ok=True)
    file_entries: dict[str, object] = {}
    for mode in NETWORK_TYPES:
        print(f"Building {mode} graph...", flush=True)
        graph = build_graph(mode, args.radius_meters)
        path = args.output / FILENAMES[mode]
        ox.save_graphml(graph, path)
        file_entries[mode] = {
            "filename": path.name,
            "sha256": sha256(path),
            "bytes": path.stat().st_size,
            "nodes": graph.number_of_nodes(),
            "edges": graph.number_of_edges(),
            "networkType": NETWORK_TYPES[mode],
            "weight": "travel_time" if mode == "Car" else "length",
        }

    generated_at = datetime.now(timezone.utc).isoformat()
    manifest = {
        "artifactVersion": f"bielsko-{generated_at[:10]}-r{args.radius_meters}",
        "generatedAt": generated_at,
        "source": "OpenStreetMap contributors via OSMnx/Overpass",
        "generationMethod": "osmnx.graph_from_point",
        "center": {"latitude": CENTER_LATITUDE, "longitude": CENTER_LONGITUDE},
        "radiusMeters": args.radius_meters,
        "supportedModes": list(NETWORK_TYPES),
        "libraryVersions": {"osmnx": version("osmnx"), "networkx": version("networkx")},
        "files": file_entries,
    }
    manifest_path = args.output / "manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(manifest, indent=2), flush=True)
    return 0


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


if __name__ == "__main__":
    sys.exit(main())
