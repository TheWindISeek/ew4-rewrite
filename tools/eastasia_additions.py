"""Append requested towns without renumbering or moving the approved base cities."""

import hashlib
import json
import math

from shapely.geometry import Point


def apply(runtime, data, placements, land):
    plan = json.loads((runtime.REPORT/'map_additions.json').read_text(encoding='utf-8'))
    used = {city['cell'] for city in placements}
    by_name = {city['name']: city for city in placements}
    candidates = []
    for row in range(runtime.GRID_H):
        for col in range(runtime.GRID_W):
            sx, sy = runtime.world_to_source(*runtime.center(col, row))
            if data[row, col, 0] != 1 and land.covers(Point(sx, sy)):
                candidates.append((row*runtime.GRID_W+col, sx, sy))
    for entry in plan['cities']:
        if entry['name'] in by_name:
            continue
        x, y = map(float, runtime.geo.PROJ.pixels(entry['lon'], entry['lat']))
        if not (0 <= x*2 < runtime.IMAGE_W and 0 <= y*2 < runtime.IMAGE_H):
            raise ValueError(f'Additional city outside map: {entry["name"]}')
        nearby = [(math.hypot(sx-x, sy-y), cell) for cell, sx, sy in candidates
                  if cell not in used and math.hypot(sx-x, sy-y) < 38]
        if not nearby:
            raise ValueError(f'No visible land near additional city: {entry["name"]}')
        distance, cell = min(nearby)
        used.add(cell)
        row, col = divmod(cell, runtime.GRID_W)
        data[row, col, :4] = [0, 255, 0, 0]
        identity = hashlib.sha256(f'{entry["lon"]:.4f},{entry["lat"]:.4f}'.encode()).hexdigest()[:12]
        city = dict(**entry, id='city-'+identity, city_id=max(c['city_id'] for c in placements)+1,
                    x=round(x, 3), y=round(y, 3), cell=cell, col=col, row=row,
                    world_pixel=list(runtime.center(col, row)), generalized_land=False,
                    displacement_km=round(distance*runtime.geo.METRES_PER_PIXEL/1000, 2), addition=True)
        placements.append(city)
        by_name[entry['name']] = city
    return placements
