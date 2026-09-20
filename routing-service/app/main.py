from __future__ import annotations

import logging
import time
from contextlib import asynccontextmanager

from fastapi import FastAPI, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse

from app.config import Settings
from app.errors import RoutingError, graph_not_ready, routing_failed
from app.graph_store import GraphStore, REQUIRED_MODES
from app.models import ErrorResponse, HealthResponse, RouteCalculationRequest, RouteCalculationResponse
from app.routing import Router

LOGGER = logging.getLogger("uvicorn.error")


def create_app(
    settings: Settings | None = None,
    graph_store: GraphStore | None = None,
    *,
    load_graphs: bool = True,
) -> FastAPI:
    service_settings = settings or Settings.from_env()
    store = graph_store or GraphStore(service_settings)
    router = Router(service_settings)

    @asynccontextmanager
    async def lifespan(_: FastAPI):
        if load_graphs:
            store.load()
        yield

    application = FastAPI(title="FlowBB private routing service", lifespan=lifespan)
    application.state.graph_store = store

    @application.exception_handler(RoutingError)
    async def routing_error_handler(_: Request, error: RoutingError) -> JSONResponse:
        LOGGER.warning("routing_failed category=%s", error.code)
        return JSONResponse(
            status_code=error.status_code,
            content=ErrorResponse(code=error.code, message=error.message).model_dump(),
        )

    @application.exception_handler(RequestValidationError)
    async def validation_error_handler(_: Request, error: RequestValidationError) -> JSONResponse:
        unsupported_mode = any(item["loc"][-1:] == ("mode",) for item in error.errors())
        code = "UNSUPPORTED_MODE" if unsupported_mode else "INVALID_INPUT"
        message = "Mode must be Walking, Bike, or Car." if unsupported_mode else "Request validation failed."
        return JSONResponse(status_code=422, content=ErrorResponse(code=code, message=message).model_dump())

    @application.get("/health", response_model=HealthResponse)
    async def health() -> HealthResponse:
        return HealthResponse(
            status="ready" if store.ready else "not_ready",
            graphLoaded=store.ready,
            artifactVersion=store.artifact_version,
            modes=list(REQUIRED_MODES) if store.ready else [],
        )

    @application.post(
        "/route",
        response_model=RouteCalculationResponse,
        responses={
            404: {"model": ErrorResponse},
            422: {"model": ErrorResponse},
            500: {"model": ErrorResponse},
            503: {"model": ErrorResponse},
        },
    )
    async def route(route_request: RouteCalculationRequest) -> RouteCalculationResponse:
        if not store.ready:
            raise graph_not_ready()
        started = time.perf_counter()
        try:
            response, metrics = router.calculate(store.graph_for(route_request.mode), route_request)
        except RoutingError:
            raise
        except Exception as error:
            LOGGER.exception("routing_failed category=%s", type(error).__name__)
            raise routing_failed() from error
        elapsed_ms = (time.perf_counter() - started) * 1000
        LOGGER.info(
            "routing_completed mode=%s elapsed_ms=%.1f distance_meters=%.1f origin_snap_meters=%.1f destination_snap_meters=%.1f artifact_version=%s",
            route_request.mode.value,
            elapsed_ms,
            response.distanceMeters,
            metrics.origin_snap_meters,
            metrics.destination_snap_meters,
            store.artifact_version,
        )
        return response

    return application


app = create_app()
