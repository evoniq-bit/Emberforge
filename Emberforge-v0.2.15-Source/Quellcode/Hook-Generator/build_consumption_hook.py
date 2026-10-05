"""Independent current-build placement payment hook; no CT code, no process access.

3EA870 is the shared ingredient payment/refund routine. Only the three traced
server placement call sites and deduction=true may bypass it. Refunds, other
callers and the disabled path run all original instructions. A separate free
build flag uses the same narrow placement whitelist. Client availability is
cleared by a second return hook; real in-game behavior still requires testing.
"""
from pathlib import Path
import sys, struct, json
for p in Path(__file__).resolve().parents:
    if (p/'work'/'deps').exists(): sys.path.insert(0,str(p/'work'/'deps')); break
import disasm_game as g
src=Path(__file__).resolve().parent.parent
rva=0x3ea870; start=0x3000; data=0x5600; free_data=0x5610
original=bytes.fromhex('40 55 56 48 83 EC 78')
assert g.read(rva,7)==original
assert sum(i.size for i in g.md.disasm(original,rva))==7
b=bytearray(); labels={}; local=[]; ext=[]
def raw(s): b.extend(bytes.fromhex(s))
def rip(s,target,tail=''):
    raw(s); b.extend(struct.pack('<i',target-(start+len(b)+4+len(bytes.fromhex(tail)))));raw(tail)
def branch(op,label):
    raw(op);local.append((len(b),label));b.extend(b'\0'*4)
def external(op,target):
    raw(op);ext.append({'Offset':start+len(b),'Rva':target});b.extend(b'\0'*4)
raw('9C 50 41 53') # flags, rax, r11; original return address now [rsp+18]
rip('83 3D',data,'00');branch('0F 85','payment_enabled')
rip('83 3D',free_data,'00');branch('0F 84','original')
labels['payment_enabled']=start+len(b)
raw('45 84 C0');branch('0F 84','original') # refund/add path untouched
raw('48 8B 44 24 18')
for caller in (0x280e72,0x282b6c,0x282bc9):
    assert g.read(caller-5,1)==b'\xe8'
    assert caller+struct.unpack('<i',g.read(caller-4,4))[0]==rva
    external('4C 8D 1D',caller);raw('4C 39 D8');branch('0F 84','preserve')
branch('E9','original')
labels['preserve']=start+len(b);rip('FF 05',data+8)
raw('41 5B 58 9D B0 01 C3') # AL bool success; no transaction entries changed
labels['original']=start+len(b);raw('41 5B 58 9D');raw(original.hex());external('E9',rva+7)
for at,label in local:struct.pack_into('<i',b,at,labels[label]-(start+at+4))
assert len(b)<0x200
assert sum(i.size for i in g.md.disasm(bytes(b),start))==len(b)
path=src/'CharacterHooks.json';manifest=json.loads(path.read_text('utf-8-sig'))
manifest['Hooks']=[h for h in manifest['Hooks'] if h['Name']!='BuildingNoConsumption']
assert all(not(start<=h['CodeOffset']<start+0x200) for h in manifest['Hooks'])
manifest['Hooks'].append(dict(Name='BuildingNoConsumption',Rva=rva,CodeOffset=start,
    Original=original.hex(),Context=g.read(rva,24).hex(),Code=b.hex(),Relocations=ext))
path.write_text(json.dumps(manifest,indent=2),encoding='utf-8')

# The cursor builder writes its final restriction bitmask through R15. Clear
# only the missing-material bit while the free-build flag is active, then run
# the original shared epilogue. All other placement restrictions remain intact.
cursor_rva=0x2504d4; cursor_start=0x3e00; cursor_original=bytes.fromhex('48 81 C4 40 02 00 00')
assert g.read(cursor_rva,7)==cursor_original
c=bytearray(); cursor_ext=[]; cursor_local=[]
def craw(s): c.extend(bytes.fromhex(s))
def crip(s,target,tail=''):
    craw(s); c.extend(struct.pack('<i',target-(cursor_start+len(c)+4+len(bytes.fromhex(tail))))); craw(tail)
def cbranch(op,label):
    craw(op); cursor_local.append((len(c),label)); c.extend(b'\0'*4)
crip('83 3D',free_data,'00'); cbranch('0F 84','keep')
craw('41 81 27 FF EF FF FF')
labels2={'keep':cursor_start+len(c)}
craw(cursor_original.hex())
craw('E9'); cursor_ext.append({'Offset':cursor_start+len(c),'Rva':cursor_rva+7}); c.extend(b'\0'*4)
for at,label in cursor_local: struct.pack_into('<i',c,at,labels2[label]-(cursor_start+at+4))
assert len(c)<0x100
assert sum(i.size for i in g.md.disasm(bytes(c),cursor_start))==len(c)
manifest=json.loads(path.read_text('utf-8-sig'))
manifest['Hooks']=[h for h in manifest['Hooks'] if h['Name']!='BuildingFreeCursor']
manifest['Hooks'].append(dict(Name='BuildingFreeCursor',Rva=cursor_rva,CodeOffset=cursor_start,
    Original=cursor_original.hex(),Context=g.read(cursor_rva,24).hex(),Code=c.hex(),Relocations=cursor_ext))
path.write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print('Validated placement payment and free-build cursor hooks: '+str(len(b))+' + '+str(len(c))+' bytes')
