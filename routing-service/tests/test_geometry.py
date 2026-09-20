from app.routing import haversine_meters


def test_geometry_uses_lon_lat_and_contains_road_shape(client, route_payload) -> None:
    response = client.post("/route", json=route_payload)

    coordinates = response.json()["geometry"]["coordinates"]
    assert coordinates[0] == [19.0, 49.0]
    assert coordinates[-1] == [19.002, 49.002]
    assert len(coordinates) == 5
    assert [19.0004, 49.0007] in coordinates
    assert [19.0015, 49.0012] in coordinates


def test_route_distance_is_not_direct_line(client, route_payload) -> None:
    response = client.post("/route", json=route_payload)

    direct = haversine_meters(49.0, 19.0, 49.002, 19.002)
    assert response.json()["distanceMeters"] >= direct
