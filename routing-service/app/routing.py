from __future__ import annotations

import math
from dataclasses import dataclass
from typing import Any

import networkx as nx

from app.config import Settings
from app.errors import route_not_found, routing_failed, snap_too_far
from app.geometry import concatenate, edge_coordinates
from app.models import (
    LineStringGeometry,
    RouteCalculationRequest,
    RouteCalculationResponse,
    RoutingMode,
)

EARTH_RADIUS_METERS = 6_371_008.8


@dataclass(frozen=True)
class SnapResult:
    node: Any
    distance_meters: float


@dataclass(frozen=True)
class CalculationMetrics:
    origin_snap_meters: float
    destination_snap_meters: float


class Router:
    def __init__(self, settings: Settings) -> None:
        self.settings = settings

    def calculate(
        self, graph: nx.MultiDiGraph, request: RouteCalculationRequest
    ) -> tuple[RouteCalculationResponse, CalculationMetrics]:
        origin = self._snap(graph, request.origin.latitude, request.origin.longitude)
        destination = self._snap(graph, request.destination.latitude, request.destination.longitude)
        self._validate_snap(origin, destination)
        weight = "travel_time" if request.mode is RoutingMode.CAR else "length"
        try:
            path = nx.shortest_path(graph, origin.node, destination.node, weight=weight)
        except nx.NetworkXNoPath as error:
            raise route_not_found() from error
        except (nx.NodeNotFound, KeyError, TypeError, ValueError) as error:
            raise routing_failed() from error

        edges = [self._select_edge(graph, source, target, weight) for source, target in zip(path, path[1:])]
        if not edges:
            raise route_not_found()
        distance = sum(float(edge["length"]) for edge in edges)
        duration = self._duration_seconds(request.mode, edges, distance)
        coordinates = concatenate(
            edge_coordinates(graph, source, target, edge)
            for source, target, edge in zip(path, path[1:], edges)
        )
        if len(coordinates) < 2 or distance <= 0 or duration <= 0:
            raise routing_failed()

        response = RouteCalculationResponse(
            mode=request.mode,
            distanceMeters=round(distance, 2),
            durationSeconds=duration,
            geometry=LineStringGeometry(coordinates=coordinates),
            steps=[],
        )
        metrics = CalculationMetrics(origin.distance_meters, destination.distance_meters)
        return response, metrics

    def _validate_snap(self, origin: SnapResult, destination: SnapResult) -> None:
        if max(origin.distance_meters, destination.distance_meters) > self.settings.max_snap_meters:
            raise snap_too_far()

    @staticmethod
    def _snap(graph: nx.MultiDiGraph, latitude: float, longitude: float) -> SnapResult:
        best_node: Any | None = None
        best_distance = math.inf
        for node, data in graph.nodes(data=True):
            distance = haversine_meters(latitude, longitude, float(data["y"]), float(data["x"]))
            if distance < best_distance:
                best_node = node
                best_distance = distance
        if best_node is None:
            raise routing_failed()
        return SnapResult(best_node, best_distance)

    @staticmethod
    def _select_edge(
        graph: nx.MultiDiGraph, source: Any, target: Any, weight: str
    ) -> dict[str, Any]:
        candidates = graph.get_edge_data(source, target)
        if not candidates:
            raise routing_failed()
        try:
            return min(candidates.values(), key=lambda edge: float(edge[weight]))
        except (KeyError, TypeError, ValueError) as error:
            raise routing_failed() from error

    def _duration_seconds(
        self, mode: RoutingMode, edges: list[dict[str, Any]], distance_meters: float
    ) -> int:
        if mode is RoutingMode.CAR:
            try:
                return max(1, math.ceil(sum(float(edge["travel_time"]) for edge in edges)))
            except (KeyError, TypeError, ValueError) as error:
                raise routing_failed() from error
        speed = self.settings.walking_speed_kmh if mode is RoutingMode.WALKING else self.settings.bike_speed_kmh
        return max(1, math.ceil(distance_meters / (speed * 1000 / 3600)))


def haversine_meters(first_lat: float, first_lon: float, second_lat: float, second_lon: float) -> float:
    latitude_delta = math.radians(second_lat - first_lat)
    longitude_delta = math.radians(second_lon - first_lon)
    first_latitude = math.radians(first_lat)
    second_latitude = math.radians(second_lat)
    value = math.sin(latitude_delta / 2) ** 2 + (
        math.cos(first_latitude) * math.cos(second_latitude) * math.sin(longitude_delta / 2) ** 2
    )
    return 2 * EARTH_RADIUS_METERS * math.asin(min(1.0, math.sqrt(value)))
