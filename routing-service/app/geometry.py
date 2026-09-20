from __future__ import annotations

from collections.abc import Iterable
from typing import Any

import networkx as nx
from shapely.geometry import LineString


def edge_coordinates(
    graph: nx.MultiDiGraph, source: Any, target: Any, edge: dict[str, Any]
) -> list[tuple[float, float]]:
    geometry = edge.get("geometry")
    coordinates = list(geometry.coords) if isinstance(geometry, LineString) else [
        (float(graph.nodes[source]["x"]), float(graph.nodes[source]["y"])),
        (float(graph.nodes[target]["x"]), float(graph.nodes[target]["y"])),
    ]
    source_point = (float(graph.nodes[source]["x"]), float(graph.nodes[source]["y"]))
    if _squared_distance(coordinates[-1], source_point) < _squared_distance(coordinates[0], source_point):
        coordinates.reverse()
    return [(float(longitude), float(latitude)) for longitude, latitude in coordinates]


def concatenate(parts: Iterable[list[tuple[float, float]]]) -> list[tuple[float, float]]:
    result: list[tuple[float, float]] = []
    for part in parts:
        for coordinate in part:
            if not result or coordinate != result[-1]:
                result.append(coordinate)
    return result


def _squared_distance(first: tuple[float, float], second: tuple[float, float]) -> float:
    return (first[0] - second[0]) ** 2 + (first[1] - second[1]) ** 2
