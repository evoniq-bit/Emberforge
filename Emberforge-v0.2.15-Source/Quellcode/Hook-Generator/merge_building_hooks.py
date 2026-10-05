"""Merge prepared building hooks into the unified character manifest (idempotent)."""
from pathlib import Path
import json
src=Path(__file__).resolve().parent.parent
character=json.loads((src/'CharacterHooks.json').read_text(encoding='utf8'))
building=json.loads((src/'BuildingHooks.json').read_text(encoding='utf8'))
assert character['Sha256']==building['Sha256'] and character['Build']==building['Build']
assert character['DataOffset']==0x4000 and character['AllocationSize']==0x6000
character['Hooks']=[h for h in character['Hooks'] if h['Name'] not in {b['Name'] for b in building['Hooks']}]+building['Hooks']
spans=[]
for h in character['Hooks']:
    assert h['CodeOffset']+len(bytes.fromhex(h['Code']))<=0x4000
    span=(h['Rva'],h['Rva']+len(bytes.fromhex(h['Original'])))
    assert all(span[1]<=other[0] or span[0]>=other[1] for other in spans)
    spans.append(span)
(src/'CharacterHooks.json').write_text(json.dumps(character,indent=2),encoding='utf8')
print('Unified hooks:',len(character['Hooks']),'code before0x4000, character data0x4000, building data0x5000')
