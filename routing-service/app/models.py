from __future__ import annotations

from enum import Enum
from typing import Literal

from pydantic import BaseModel, ConfigDict, Field


class RoutingMode(str, Enum):
    WALKING = "Walking"
    BIKE = "Bike"
    CAR = "Car"


class Coordinate(BaseModel):
    model_config = ConfigDict(extra="forbid")

    latitude: float = Field(ge=-90, le=90, allow_inf_nan=False)
    longitude: float = Field(ge=-180, le=180, allow_inf_nan=False)


class RouteCalculationRequest(BaseModel):
    model_config = ConfigDict(extra="forbid")

    origin: Coordinate
    destination: Coordinate
    mode: RoutingMode


class LineStringGeometry(BaseModel):
    model_config = ConfigDict(extra="forbid")

    type: Literal["LineString"] = "LineString"
    coordinates: list[tuple[float, float]]


class RouteStep(BaseModel):
    model_config = ConfigDict(extra="forbid")

    instruction: str
    distanceMeters: float = Field(ge=0)
    durationSeconds: int = Field(ge=0)


class RouteCalculationResponse(BaseModel):
    model_config = ConfigDict(extra="forbid")

    mode: RoutingMode
    distanceMeters: float = Field(gt=0)
    durationSeconds: int = Field(gt=0)
    geometry: LineStringGeometry
    steps: list[RouteStep] = Field(default_factory=list)


class ErrorResponse(BaseModel):
    model_config = ConfigDict(extra="forbid")

    code: str
    message: str


class HealthResponse(BaseModel):
    model_config = ConfigDict(extra="forbid")

    status: Literal["ready", "not_ready"]
    processAlive: Literal[True] = True
    graphLoaded: bool
    artifactVersion: str | None
    modes: list[RoutingMode]
