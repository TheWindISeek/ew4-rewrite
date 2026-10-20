"""Archive only the explicitly supplied follow-up screenshots, preserving originals."""
import hashlib
import json
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / "reverse/references/frontend_refinement_20261019"
SOURCE = Path("C:/Users/WIN11/AppData/Local/Temp")
IDS = """
3c60460b-0b46-4821-a6a4-310575d36d90 04a98b4b-7c9e-4be0-93f2-1b3ca61b502d
7932a2e3-d9e4-45ce-a31e-a538b827df2b 3ac4d913-5c19-478e-9324-8a7c19b60a59
5517e178-5714-421f-90c9-1faf54c4ecd3 d985f1c9-6b4f-4aba-87a7-36c6de361d33
54159aa8-5f62-4fe4-ae1a-2dd52598f271 33ad2a6c-a9bd-404f-8d41-255b2bd57924
52014221-8ff8-4777-b26f-6662c3658c00 f8b06667-18f8-4b5b-846a-cd9023f672f3
4589d4f3-d6af-47fb-8ada-2f30a0069718 8768fbb5-6e02-416e-9b6f-a24f6b9c0ffb
214839e2-b752-46b8-b8f5-60dff833a4c3 398bfbf0-2cfc-4d5b-a4ef-6d09f3bff790
b48d07e4-e489-4803-96c8-92788654d6ae 1c5dcebd-af8c-4733-bfb2-491da4a2c91d
c8387578-3ee3-44ca-9b6d-5c3011b003ba b37f30da-8abe-4d72-a80c-fd07647a159e
ecb7087a-0ca6-41b6-b906-c243cc77705e f7db3d72-e93c-4344-9ff8-bdd357ea5bbf
4c3021d8-1f89-4411-864d-892c9bf878fc 6545c261-0a1d-4340-9e4c-eae8148f913d
ca3e00d5-b10c-45a5-a186-18ee8a207590
""".split()
manifest = DEST / "manifest.json"
records = {r["name"]: r for r in json.loads(manifest.read_text(encoding="utf-8"))}
for identifier in IDS:
    name = "codex-clipboard-" + identifier + ".png"
    source, target = SOURCE / name, DEST / name
    digest = hashlib.sha256(source.read_bytes()).hexdigest()
    if target.exists():
        assert hashlib.sha256(target.read_bytes()).hexdigest() == digest, name
    else:
        shutil.copy2(source, target)
    assert hashlib.sha256(target.read_bytes()).hexdigest() == digest, name
    records[name] = dict(name=name, bytes=target.stat().st_size, sha256=digest)
manifest.write_text(json.dumps(list(records.values()), indent=2), encoding="utf-8")
print(json.dumps(dict(records=len(records), supplied=len(IDS), errors=0)))
