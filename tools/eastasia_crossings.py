"""Select actual reciprocal river edges for the base map's bridge candidates."""

import json
import math
from collections import Counter
from shapely.geometry import LineString

def point(cell,width):
    row,col = divmod(cell,width)
    return col*64+(row%2)*32,row*54-18


def build(plan_path,data,project,river_paths):
    height,width,_ = data.shape
    edges = []
    for cell in range(width*height):
        row,col = divmod(cell,width)
        for direction in range(3):
            if not int(data[row,col,8]) & (1<<direction):
                continue
            other = [(col+1,row),(col+row%2,row+1),(col+row%2-1,row+1)][direction]
            nc,nr = other
            if 0 <= nc < width and 0 <= nr < height and data[row,col,0] != 1 and data[nr,nc,0] != 1:
                a,b = point(cell,width),point(nr*width+nc,width)
                edges.append((cell,nr*width+nc,direction,(a[0]+b[0])/2,(a[1]+b[1])/2))
    selected,used,omitted = [],set(),[]
    river_edges = {name: {(a,b) for a,b,d,mx,my in edges if LineString([
        tuple(v/2 for v in point(a,width)),tuple(v/2 for v in point(b,width))]).intersects(path)}
        for name,path in river_paths.items()}
    for item in json.loads(plan_path.read_text(encoding='utf-8'))['crossings']:
        x,y = project(item['lon'],item['lat'])
        allowed = river_edges.get(item.get('river'))
        choices = [(math.hypot(mx-x,my-y),a,b,d) for a,b,d,mx,my in edges
                   if (a,b) not in used and (allowed is None or (a,b) in allowed)]
        if not choices:
            omitted.append(dict(name=item['name'],reason='No available edge on requested river'))
            continue
        distance,a,b,direction = min(choices)
        if distance > 120:
            omitted.append(dict(name=item['name'],reason='No mapped river crossing within approximately 80 km'))
            continue
        used.add((a,b))
        selected.append({'kind':'bridge',**item,'cell':a,'other':b,'direction':direction,'world_pixel':list(point(a,width)),
                         'offset_world_pixels':round(distance,2)})
    if not selected:
        raise ValueError('No valid river crossings selected')
    return selected,dict(bridges=len(selected),omitted=omitted,river_edges_preserved=len(edges)*2,
                         river_counts=dict(Counter(b.get('river','other') for b in selected)),
                         kind_counts=dict(Counter(b['kind'] for b in selected)))
