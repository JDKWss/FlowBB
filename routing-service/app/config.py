from __future__ import annotations

import os
from dataclasses import dataclass
from pathlib import Path


@dataclass(frozen=True)
class Settings:
    data_dir: Path = Path("/app/data")
    max_snap_meters: float = 500.0
    walking_speed_kmh: float = 4.8
    bike_speed_kmh: float = 15.0

    @classmethod
    def from_env(cls) -> "Settings":
        return cls(
            data_dir=Path(os.getenv("ROUTING_DATA_DIR", "/app/data")),
            max_snap_meters=_positive_float("ROUTING_MAX_SNAP_METERS", 500.0),
            walking_speed_kmh=_positive_float("ROUTING_WALKING_SPEED_KMH", 4.8),
            bike_speed_kmh=_positive_float("ROUTING_BIKE_SPEED_KMH", 15.0),
        )


def _positive_float(name: str, default: float) -> float:
    value = float(os.getenv(name, str(default)))
    if value <= 0:
        raise ValueError(f"{name} must be greater than zero")
    return value
