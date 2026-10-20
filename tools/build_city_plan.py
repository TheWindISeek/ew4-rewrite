"""Render a city planning atlas without changing rewrite runtime content."""

from __future__ import annotations

from collections import Counter
import hashlib
import html
import json
import math
from pathlib import Path
import sys

sys.path.insert(0, str(Path.home() / ".codex/tools/ew4-inspect-deps"))
from PIL import Image, ImageDraw, ImageEnhance, ImageFont

ROOT = Path(__file__).resolve().parents[1]
INPUT = ROOT / "maps/city_plan"
OUT = INPUT / "conquest_v2"
FONT = Path("C:/Windows/Fonts/msyh.ttc")
BOLD = Path("C:/Windows/Fonts/msyhbd.ttc")
WIDTH, HEIGHT = 4993, 3583
PLAN_SCALE = 2
LABEL_COLOR = (24, 28, 29)
WHITE = (255, 255, 255)


def project(lon, lat):
    p1, p2, lon0, p0 = map(math.radians, (25, 47, 110, 35))
    n = math.log(math.cos(p1) / math.cos(p2)) / math.log(
        math.tan(math.pi / 4 + p2 / 2) / math.tan(math.pi / 4 + p1 / 2))
    f = math.cos(p1) * math.tan(math.pi / 4 + p1 / 2) ** n / n
    radius = 6371008.8
    rho0 = radius * f / math.tan(math.pi / 4 + p0 / 2) ** n
    rho = radius * f / math.tan(math.pi / 4 + math.radians(lat) / 2) ** n
    theta = n * (math.radians(lon) - lon0)
    x, y = rho * math.sin(theta), rho0 - rho * math.cos(theta)
    ymax = -2050000 + 6600000 * HEIGHT / WIDTH
    return (x + 3350000) * WIDTH / 6600000, (ymax - y) * HEIGHT / (ymax + 2050000)


def font(size, bold=False):
    return ImageFont.truetype(str(BOLD if bold else FONT), size)


def write_json(path, data):
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def overlaps(a, b):
    return a[0] < b[2] and b[0] < a[2] and a[1] < b[3] and b[1] < a[3]


class Occupancy:
    def __init__(self):
        self.bins = {}
        self.boxes = []

    def keys(self, box):
        for x in range(int(box[0]) // 100, int(box[2]) // 100 + 1):
            for y in range(int(box[1]) // 100, int(box[3]) // 100 + 1):
                yield x, y

    def add(self, box, owner):
        index = len(self.boxes)
        self.boxes.append((box, owner))
        for key in self.keys(box):
            self.bins.setdefault(key, []).append(index)

    def clear(self, box, owner):
        indices = {i for key in self.keys(box) for i in self.bins.get(key, [])}
        return not any(other != owner and overlaps(box, b) for i in indices for b, other in [self.boxes[i]])


def load_cities():
    source = json.loads((INPUT / "cities.json").read_text(encoding="utf-8"))
    selection = json.loads((INPUT / "selection_v2.json").read_text(encoding="utf-8"))
    cities, excluded = [], []
    for group, records in source["groups"].items():
        for name, lon, lat, tier in records:
            if name not in selection[group]:
                continue
            x, y = project(lon, lat)
            record = dict(name=name, lon=lon, lat=lat, tier=tier, group=group, x=round(x, 3), y=round(y, 3))
            if 0 <= x < WIDTH and 0 <= y < HEIGHT:
                record["id"] = "city-" + hashlib.sha256(f"{lon:.4f},{lat:.4f}".encode()).hexdigest()[:12]
                cities.append(record)
            else:
                excluded.append(record)
    if len({c["id"] for c in cities}) != len(cities):
        raise ValueError("Duplicate city coordinates: merge historical aliases in source")
    if len({c["name"] for c in cities}) != len(cities):
        raise ValueError("Duplicate display names")
    requested = {name for names in selection.values() for name in names}
    if requested != {c["name"] for c in cities}:
        raise ValueError("Missing or out-of-bounds selected cities: " + str(requested - {c["name"] for c in cities}))
    return source, cities, excluded


def annotate(base, cities, extent, output_width, title, subtitle, path):
    left, top, right, bottom = extent
    scale = output_width / (right - left)
    image_height = round((bottom - top) * scale)
    header = 144
    image = Image.new("RGB", (output_width, image_height + header + 76), WHITE)
    cropped = base.crop(tuple(map(round, extent))).resize((output_width, image_height), Image.Resampling.LANCZOS)
    cropped = ImageEnhance.Color(cropped).enhance(.18)
    cropped = Image.blend(cropped, Image.new("RGB", cropped.size, WHITE), .27)
    image.paste(cropped, (0, header))
    draw = ImageDraw.Draw(image)
    draw.text((42, 22), title, font=font(48, True), fill=LABEL_COLOR)
    draw.text((44, 88), subtitle, font=font(25), fill=LABEL_COLOR)
    selected = [c for c in cities if left <= c["x"] < right and top <= c["y"] < bottom]
    points = {c["id"]: ((c["x"] - left) * scale, (c["y"] - top) * scale + header) for c in selected}
    occupancy = Occupancy()
    for c in selected:
        x, y = points[c["id"]]
        occupancy.add((x - 7, y - 7, x + 7, y + 7), c["id"])
    placements, failures = [], []
    fonts = {1: font(30, True), 2: font(27), 3: font(23)}
    # Fix labels for sparse cities first only within the same tier; dense clusters get more leader options.
    for c in sorted(selected, key=lambda c: (c["tier"], c["y"], c["x"])):
        x, y = points[c["id"]]
        label_font = fonts[c["tier"]]
        bbox = draw.textbbox((0, 0), c["name"], font=label_font)
        w, h = bbox[2] - bbox[0], bbox[3] - bbox[1]
        candidates = []
        for distance in (12, 24, 42, 66, 96, 136, 188, 248, 320, 410, 520, 650):
            for angle in (0, -45, 45, 180, -135, 135, -90, 90):
                radians = math.radians(angle)
                px, py = x + distance * math.cos(radians), y + distance * math.sin(radians)
                tx = px if math.cos(radians) > .5 else px - w if math.cos(radians) < -.5 else px - w / 2
                ty = py - h / 2
                rect = (tx - 4, ty - 4, tx + w + 4, ty + h + 4)
                if rect[0] < 8 or rect[2] > output_width - 8 or rect[1] < header + 8 or rect[3] > header + image_height - 8:
                    continue
                if occupancy.clear(rect, c["id"]):
                    candidates.append((distance, rect, tx, ty))
            if candidates:
                break
        if not candidates:
            failures.append(c["name"])
            continue
        _, rect, tx, ty = candidates[0]
        occupancy.add(rect, c["id"])
        placements.append((c, rect, tx, ty, label_font, bbox))
    for c, rect, tx, ty, label_font, bbox in placements:
        x, y = points[c["id"]]
        near_x, near_y = min(max(x, rect[0] + 4), rect[2] - 4), min(max(y, rect[1] + 4), rect[3] - 4)
        if math.hypot(near_x - x, near_y - y) > 22:
            draw.line([(x, y), (near_x, near_y)], fill=(80, 85, 87), width=1)
    for c in selected:
        x, y = points[c["id"]]
        radius = 6 if c["tier"] == 1 else 4.5 if c["tier"] == 2 else 3.5
        draw.ellipse((x-radius-2, y-radius-2, x+radius+2, y+radius+2), fill=WHITE)
        draw.ellipse((x-radius, y-radius, x+radius, y+radius), fill=WHITE if c["tier"] == 3 else LABEL_COLOR, outline=LABEL_COLOR, width=2)
    for c, rect, tx, ty, label_font, bbox in placements:
        draw.text((tx - bbox[0], ty - bbox[1]), c["name"], font=label_font, fill=LABEL_COLOR, stroke_width=3, stroke_fill=WHITE)
    footer = f"图内 {len(selected)} 处  |  实心：城市  空心：县镇/关隘/交通据点  |  坐标为地理候选点，非最终游戏落格"
    draw.text((42, header + image_height + 22), footer, font=font(24), fill=LABEL_COLOR)
    image.save(path, optimize=True)
    label_boxes = [(c["id"], rect) for c, rect, *_ in placements]
    collisions = sum(overlaps(a, b) for i, (_, a) in enumerate(label_boxes) for _, b in label_boxes[i+1:])
    return dict(file=path.name, points=len(selected), labels=len(placements), missing_labels=failures, label_overlaps=collisions,
                width=image.width, height=image.height)


def grid_sample(base, cities):
    extent = (2900, 1920, 3570, 2600)
    scale, header = 3.4, 150
    left, top, right, bottom = extent
    width, height = round((right-left)*scale), round((bottom-top)*scale)
    image = Image.new("RGB", (width, height+header+100), WHITE)
    image.paste(Image.blend(base.crop(extent).convert("RGB"), Image.new("RGB", (right-left,bottom-top), WHITE), .35).resize((width,height)), (0,header))
    draw = ImageDraw.Draw(image)
    draw.text((35,20), "江淮与长江下游：加密网格候选示意",font=font(43,True),fill=LABEL_COLOR)
    draw.text((35,86), "同一地理范围，横纵格数约为旧图2倍；本图不表示最终城市格点",font=font(24),fill=LABEL_COLOR)
    for row in range(136):
        for col in range(158):
            cx=(col*64+(row%2)*32)/PLAN_SCALE
            cy=(row*54-18)/PLAN_SCALE
            if not left-10 <= cx <= right+10 or not top-10 <= cy <= bottom+10:
                continue
            corners=[((cx+dx/PLAN_SCALE-left)*scale,(cy+dy/PLAN_SCALE-top)*scale+header) for dx,dy in ((0,-36),(32,-18),(32,18),(0,36),(-32,18),(-32,-18))]
            if any(y < header or y > header + height for _, y in corners):
                continue
            draw.line(corners+[corners[0]],fill=(137,147,149),width=1)
    occupied=Occupancy()
    selected=[c for c in cities if left<=c["x"]<right and top<=c["y"]<bottom]
    for c in selected:
        x,y=(c["x"]-left)*scale,(c["y"]-top)*scale+header
        occupied.add((x-8,y-8,x+8,y+8),c["id"])
    missing=[]
    for c in sorted(selected,key=lambda c:c["tier"]):
        x,y=(c["x"]-left)*scale,(c["y"]-top)*scale+header
        f=font(24 if c["tier"]==3 else 29,c["tier"]==1)
        b=draw.textbbox((0,0),c["name"],font=f)
        w,h=b[2]-b[0],b[3]-b[1]
        chosen=None
        for distance in (12,30,55,90,140,200):
            for dx,dy in ((1,0),(-1,0),(0,-1),(0,1),(1,-1),(-1,1),(1,1),(-1,-1)):
                tx=x+dx*distance-(w if dx<0 else w/2 if dx==0 else 0)
                ty=y+dy*distance-h/2
                rect=(tx-3,ty-3,tx+w+3,ty+h+3)
                if rect[0]>=6 and rect[2]<width-6 and rect[1]>=header+6 and rect[3]<header+height-6 and occupied.clear(rect,c["id"]):
                    chosen=(tx,ty,rect);break
            if chosen:break
        if chosen:
            tx,ty,rect=chosen;occupied.add(rect,c["id"])
            if math.hypot(tx-x,ty-y)>45:draw.line([(x,y),(max(rect[0],min(x,rect[2])),max(rect[1],min(y,rect[3])))],fill=LABEL_COLOR,width=1)
            draw.text((tx-b[0],ty-b[1]),c["name"],font=f,fill=LABEL_COLOR,stroke_width=3,stroke_fill=WHITE)
        else:missing.append(c["name"])
        r=4 if c["tier"]==3 else 6
        draw.ellipse((x-r,y-r,x+r,y+r),fill=WHITE if c["tier"]==3 else LABEL_COLOR,outline=LABEL_COLOR,width=2)
    draw.text((35,header+height+20),"候选全图 158×136 ≈ 2.15万格；每横向格约42公里。运行性能尚未验证。",font=font(24),fill=LABEL_COLOR)
    path=OUT/"grid_density_detail.png"
    image.save(path,optimize=True)
    return dict(file=path.name,points=len(selected),missing_labels=missing,width=image.width,height=image.height)


def make_html(cities, report):
    groups=Counter(c["group"] for c in cities)
    options="".join(f'<option value="{html.escape(k)}">{html.escape(k)} ({v})</option>' for k,v in groups.items())
    rows="".join(f'<tr data-group="{html.escape(c["group"])}"><td>{html.escape(c["name"])}</td><td>{html.escape(c["group"])}</td><td>{["","重要城市","城市","县镇/据点"][c["tier"]]}</td><td>{c["lon"]:.2f}, {c["lat"]:.2f}</td></tr>' for c in cities)
    data=json.dumps([{k:c[k] for k in ("name","x","y","group","tier")} for c in cities],ensure_ascii=False).replace("<","\\u003c")
    page='''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>东亚城市候选图</title>
<style>*{box-sizing:border-box}body{margin:0;font-family:Microsoft YaHei,Arial,sans-serif;color:#202526;background:#fff}header{height:62px;padding:14px 20px;border-bottom:1px solid #ccc;display:flex;align-items:center;gap:14px}h1{font-size:20px;margin:0}header span{font-size:13px;color:#555}main{display:grid;grid-template-columns:minmax(0,1fr) 340px;height:calc(100vh - 62px)}#viewport{overflow:auto;background:#d9dedf;position:relative}#board{position:relative;width:4993px;height:3583px;background:url('../eastasia_v5/eastasia_base.png') 0 0/100% 100% no-repeat;transform-origin:0 0}.marker{position:absolute;border:0;background:transparent;font:13px Microsoft YaHei;white-space:nowrap;padding:0;transform:translate(-4px,-50%);cursor:pointer;text-shadow:1px 1px 2px white,-1px -1px 2px white,1px -1px 2px white,-1px 1px 2px white;color:#111}.marker b{display:inline-block;width:7px;height:7px;border:1.5px solid #111;border-radius:50%;background:#111;margin-right:3px}.marker.town b{background:#fff;width:6px;height:6px}.marker.focus{background:#fff;border:2px solid #111;z-index:9;padding:3px;font-weight:700}.marker.dim{opacity:.08;pointer-events:none}.marker.hide-town{display:none}aside{padding:14px;overflow:auto;border-left:1px solid #bbb}input,select{width:100%;padding:8px;margin-bottom:8px;font:14px Microsoft YaHei;border:1px solid #aaa;border-radius:4px}input[type=range]{width:180px;padding:0;margin:0}input[type=checkbox]{width:auto;margin-right:6px}label{font-size:13px}nav{display:flex;gap:8px;margin:12px 0;flex-wrap:wrap}a{color:#246071;font-size:13px}table{font-size:12px;border-collapse:collapse;width:100%}td{padding:8px 3px;border-bottom:1px solid #ddd}tr{cursor:pointer}tr:hover{background:#e8eeee}td:last-child{display:none}#count{font-size:13px;margin:10px 0}button{cursor:pointer;background:#fff;border:1px solid #aaa;border-radius:4px;padding:6px;font:13px Microsoft YaHei}@media(max-width:760px){header{height:auto;flex-wrap:wrap;padding:10px}header span{width:100%}main{height:calc(100vh - 112px);grid-template-columns:minmax(0,1fr) 220px}h1{font-size:17px}aside{padding:8px}input[type=range]{width:120px}}</style>
<header><h1>东亚城市与低等级据点</h1><span>__TOTAL__处 · 跨年份候选 · 无势力归属</span><label>缩放 <input id="zoom" type="range" min="15" max="220" value="35"></label><button id="fit">全图</button></header>
<main><div id="viewport"><div id="space"><div id="board"></div></div></div><aside><input id="search" placeholder="城市或地区"><select id="region"><option value="">全部地区</option>__OPTIONS__</select><label><input id="towns" type="checkbox" checked>显示县镇与据点</label><div id="count"></div><nav><a href="eastasia_cities_overview.png" target="_blank">高清总图</a><a href="china_cities_detail.png" target="_blank">中国腹地详图</a><a href="grid_density_detail.png" target="_blank">加密网格</a><a href="cities.json" target="_blank">候选数据</a></nav><table><tbody>__ROWS__</tbody></table></aside></main>
<script>const cities=__DATA__, board=document.querySelector('#board'), viewport=document.querySelector('#viewport'), space=document.querySelector('#space'), zoom=document.querySelector('#zoom');let scale=.35;cities.forEach((c,i)=>{const el=document.createElement('button');el.className='marker'+(c.tier===3?' town':'');el.style.left=c.x+'px';el.style.top=c.y+'px';el.innerHTML='<b></b>'+c.name;el.title=c.name+' · '+c.group;el.dataset.index=i;el.onclick=()=>focusCity(i);board.append(el)});const markers=[...board.children],rows=[...document.querySelectorAll('tbody tr')];function updateScale(){scale=Number(zoom.value)/100;board.style.transform='scale('+scale+')';space.style.width=(4993*scale)+'px';space.style.height=(3583*scale)+'px';markers.forEach(m=>m.style.fontSize=(Math.max(12,13*scale)/scale)+'px')}function fit(){zoom.value=Math.max(15,Math.min(220,Math.floor(Math.min(viewport.clientWidth/4993,viewport.clientHeight/3583)*100)));updateScale()}function focusCity(i){markers.forEach(m=>m.classList.remove('focus'));markers[i].classList.add('focus');viewport.scrollTo({left:cities[i].x*scale-viewport.clientWidth/2,top:cities[i].y*scale-viewport.clientHeight/2,behavior:'smooth'})}rows.forEach((row,i)=>row.onclick=()=>focusCity(i));function filter(){const q=document.querySelector('#search').value.trim(),group=document.querySelector('#region').value,towns=document.querySelector('#towns').checked;let count=0;cities.forEach((c,i)=>{const show=(!q||(c.name+c.group).includes(q))&&(!group||group===c.group)&&(towns||c.tier!==3);rows[i].hidden=!show;markers[i].classList.toggle('dim',!show);markers[i].classList.toggle('hide-town',!towns&&c.tier===3);if(show)count++});document.querySelector('#count').textContent=count+' / '+cities.length+' 处'}zoom.oninput=updateScale;document.querySelector('#fit').onclick=fit;document.querySelector('#search').oninput=filter;document.querySelector('#region').onchange=filter;document.querySelector('#towns').onchange=filter;fit();filter();</script></html>'''
    page=page.replace('__TOTAL__',str(len(cities))).replace('__OPTIONS__',options).replace('__ROWS__',rows).replace('__DATA__',data)
    page=page.replace("../eastasia_v5/", "../../eastasia_v5/")
    (OUT/'index.html').write_text(page,encoding='utf-8')


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    _,cities,excluded=load_cities()
    base=Image.open(ROOT/'maps/eastasia_v5/eastasia_base.png').convert('RGB')
    report={"city_count":len(cities),"tiers":dict(Counter(c['tier'] for c in cities)),"groups":dict(Counter(c['group'] for c in cities)),
            "excluded_outside_existing_extent":excluded,"grid_proposal":{"width":158,"height":136,"cells":21488,"approximate_horizontal_km":42.30,"runtime_tested":False},"images":[]}
    report['images'].append(annotate(base,cities,(0,0,WIDTH,HEIGHT),8000,'东亚城市与低等级据点候选总图',
        f'征服精简版 · {len(cities)}处 · 跨1915—1946年选点 · 无势力归属',OUT/'eastasia_cities_overview.png'))
    report['images'].append(annotate(base,cities,(2020,1300,3710,3470),4200,'中国腹地与沿海：城市及县镇候选详图',
        '省区主要城市之外，补入铁路节点、关隘与县镇；具体等级和归属留待剧本配置',OUT/'china_cities_detail.png'))
    report['images'].append(annotate(base,cities,(2120,140,WIDTH,1930),5200,'东北、蒙古、苏联远东及日本北部',
        '外部势力活动区域同步保留城市与口岸候选；时期名称仅用于识别',OUT/'northeast_cities_detail.png'))
    report['images'].append(annotate(base,cities,(1300,2480,4140,HEIGHT),5000,'西南、华南与周边殖民地据点',
        '覆盖缅甸、印度东北、印度支那北部、暹罗北部及港澳；图外城市不扩入',OUT/'south_cities_detail.png'))
    report['images'].append(grid_sample(base,cities))
    korea = [c for c in cities if c['group'] == '朝鲜半岛']
    korea_extent = (min(c['x'] for c in korea)-100, min(c['y'] for c in korea)-90,
                    max(c['x'] for c in korea)+120, max(c['y'] for c in korea)+90)
    report['images'].append(annotate(base,cities,korea_extent,1600,'朝鲜半岛：城市分布示意',
        '半岛14处 · 不固定势力归属与分界线',OUT/'korea_cities_detail.png'))
    report['images'].append(annotate(base,cities,(0,2480,1250,3540),1100,'印度地区：新增城市位置',
        '那格浦尔 · 维沙卡帕特南',OUT/'india_cities_detail.png'))
    report['small_previews'] = []
    for source_name, output_name, size in (
        ('eastasia_cities_overview.png', 'eastasia_overview_small.jpg', (3000,2300)),
        ('korea_cities_detail.png', 'korea_cities_small.jpg', (1400,2100)),
        ('india_cities_detail.png', 'india_cities_small.jpg', (1100,1200)),
    ):
        with Image.open(OUT/source_name) as source_image:
            preview = source_image.convert('RGB')
            preview.thumbnail(size, Image.Resampling.LANCZOS)
            preview.save(OUT/output_name, quality=78, optimize=True, progressive=True, subsampling=0)
            report['small_previews'].append(dict(file=output_name, width=preview.width, height=preview.height,
                                                 bytes=(OUT/output_name).stat().st_size))
    write_json(OUT/'projected_cities.json',cities)
    write_json(OUT/'cities.json',cities)
    write_json(OUT/'verification.json',report)
    make_html(cities,report)
    print(json.dumps({"cities":len(cities),"tiers":report['tiers'],"excluded":len(excluded),"images":report['images']},ensure_ascii=False))
    if any(r.get('missing_labels') or r.get('label_overlaps') for r in report['images']):
        raise SystemExit('Map labels require layout review')


if __name__=='__main__':
    main()
