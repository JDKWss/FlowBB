from __future__ import annotations


class RoutingError(Exception):
    def __init__(self, code: str, message: str, status_code: int) -> None:
        super().__init__(message)
        self.code = code
        self.message = message
        self.status_code = status_code


def graph_not_ready() -> RoutingError:
    return RoutingError("GRAPH_NOT_READY", "Routing graphs are not ready.", 503)


def snap_too_far() -> RoutingError:
    return RoutingError("SNAP_TOO_FAR", "A coordinate is too far from the routable network.", 422)


def route_not_found() -> RoutingError:
    return RoutingError("ROUTE_NOT_FOUND", "No route exists between the snapped points.", 404)


def routing_failed() -> RoutingError:
    return RoutingError("ROUTING_FAILED", "The route could not be calculated.", 500)
