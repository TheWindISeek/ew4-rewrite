"""Compose regional lettering without rebuilding terrain, cities or wave sprites."""

import math


def glyphs(library, text, height, sea, length=0, regional=False):
    family = 's_s_c' if sea else 'l_b' if regional else 'l_s'
    items = {item['name']: item for item in library['items']}
    sprites = {sprite['name']: sprite for sprite in library['atlas']['sprites']}
    scale = height/max(float(sprites[f'{family}_{char}.png']['h']) for char in 'abcdefghijklmnopqrstuvwxyz')
    letters, cursor = [], 0
    lettering = text.title().replace(' Of ', ' of ') if sea and regional else text
    for char in lettering:
        if char == ' ':
            cursor += height*.48
            continue
        glyph_family = 's_s_l' if sea and regional and char.islower() else family
        item = items[f'{glyph_family}_{char.lower()}']
        glyph_scale = scale
        if sea and regional and char == 'o':
            # The original lowercase o has an a-like entry stroke; use a round O at x-height.
            item = items['s_s_c_o']
            glyph_scale *= float(sprites['s_s_l_o.png']['h'])/float(sprites['s_s_c_o.png']['h'])
        elif sea and regional and char == 'f':
            glyph_scale *= .5
        sprite = sprites[item['name']+'.png']
        letters.append((item, cursor, float(sprite['h'])*glyph_scale, glyph_scale))
        cursor += float(sprite['w'])*glyph_scale
    if len(letters) > 1 and length > cursor:
        spacing = (length-cursor)/(len(letters)-1)
        letters = [(item, x+i*spacing, h, scale) for i, (item, x, h, scale) in enumerate(letters)]
        cursor = length
    elif regional and length > 0 and cursor > length:
        fit = length/cursor
        letters = [(item, x*fit, h*fit, scale*fit) for item, x, h, scale in letters]
        cursor = length
    return letters, cursor


def regional_labels(library, labels, plan):
    layouts = plan.get('regionalLayout', {})
    if not layouts:
        return labels
    existing = {label['text'] for label in labels}
    for entry in plan['labels']:
        if entry['text'] in existing:
            continue
        style = layouts[entry['text']]
        labels.append(dict(index=str(max(int(label['index']) for label in labels)+1),
                           libName='eastasia_'+entry['text'].lower().replace(' ', '_'),
                           colorAlphaPercent='100', text=entry['text'], textZh=plan['chineseNames'][entry['text']],
                           tx=style['x'], ty=style['y'], size=style['size'], angle=style.get('angle', 0),
                           sea=entry.get('sea', False), lake=entry.get('lake'), displacement=0,
                           letteringLength=style['length'], opacity=plan['labelOpacity']))
    first_item = min(item['id'] for item in library['items'] if item['name'].startswith('eastasia_'))
    first_element = min(pair[0] for item in library['items'][first_item:]
                        for layer in item['layers'] for frame in layer for pair in frame['indices'])
    assert all(item['name'].startswith('eastasia_') for item in library['items'][first_item:])
    library['items'] = library['items'][:first_item]
    library['elements'] = library['elements'][:first_element]
    for label in labels:
        style = layouts.get(label['text'])
        if style:
            label.update(size=style['size'], letteringLength=style['length'],
                         tx=style.get('x', label['tx']), ty=style.get('y', label['ty']),
                         angle=style.get('angle', label['angle']), geographicStyle='regional')
        letters, width = glyphs(library, label['text'], label['size'], label['sea'], label['letteringLength'], bool(style))
        label['letteringLength'] = round(width, 2)
        sine, cosine = math.sin(math.radians(label['angle'])), math.cos(math.radians(label['angle']))
        indices = []
        for item, cursor, height, scale in letters:
            px, py = item['pivot']
            x, y = cursor-width/2-px*scale, label['size']/2-height-py*scale
            indices.append([len(library['elements']), item['id']])
            library['elements'].append(dict(transform=[cosine*scale, sine*scale, -sine*scale, cosine*scale,
                                                       cosine*x-sine*y, sine*x+cosine*y],
                                            alpha=1, start_frame=0, playback_mode=0))
        library['items'].append(dict(id=len(library['items']), name=label['libName'], kind=0,
                                     layers=[[dict(key=0, indices=indices)]], pivot=[0, 0], duration=1))
        corners = [(label['tx']+cosine*x-sine*y, label['ty']+sine*x+cosine*y)
                   for x, y in [(-width/2-5, -label['size']/2-5), (width/2+5, -label['size']/2-5),
                                (width/2+5, label['size']/2+5), (-width/2-5, label['size']/2+5)]]
        left, top = min(x for x, y in corners), min(y for x, y in corners)
        label.update(left=round(left, 3), top=round(top, 3),
                     width=round(max(x for x, y in corners)-left, 3),
                     height=round(max(y for x, y in corners)-top, 3), polygon=corners+[corners[0]])
    return labels


def verify_layout(labels, width, height):
    def overlaps(a, b):
        for polygon in (a, b):
            for i, (x, y) in enumerate(polygon[:4]):
                nx, ny = polygon[(i+1) % 4]
                axis = (ny-y, x-nx)
                pa = [px*axis[0]+py*axis[1] for px, py in a]
                pb = [px*axis[0]+py*axis[1] for px, py in b]
                if max(pa) <= min(pb) or max(pb) <= min(pa):
                    return False
        return True

    for i, label in enumerate(labels):
        assert all(0 <= x <= width and 0 <= y <= height for x, y in label['polygon']), label['text']+' exceeds map'
        for other in labels[:i]:
            assert not overlaps(label['polygon'], other['polygon']), label['text']+' overlaps '+other['text']
