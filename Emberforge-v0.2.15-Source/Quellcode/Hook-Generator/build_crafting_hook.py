"""Recipe ingredient-preservation hook built from current binary, no game writes.
Normal recipe ingredient CALL alone is skipped. UI/simulation alternate CALL,
knowledge/comfort validation, experience cost, outputs and commit stay original.
"""
from pathlib import Path
import sys,struct,json,hashlib
for p in Path(__file__).resolve().parents:
 if (p/'work'/'deps').exists():sys.path.insert(0,str(p/'work'/'deps'));break
sys.path.insert(0,str(Path(__file__).resolve().parents[1]))
import disasm_game as g
assert hashlib.sha256(g.data).hexdigest()=='af2f5a1227911d8aa06b3908d6bd0211838211cae14ea91099cb57d0df990781'
rva=0x36da41;start=0x3200;flag=0x5604;counter=0x5610
original=bytes.fromhex('e8 2a f9 ff ff')
assert g.read(rva,5)==original
assert rva+5+struct.unpack_from('<i',original,1)[0]==0x36d370
# Alternate helper is deliberately outside the patch; original short JMP is retained.
assert g.read(rva+5,7)==bytes.fromhex('eb 05 e8 53 06 00 00')
b=bytearray();labels={};local=[];ext=[]
def raw(s):b.extend(bytes.fromhex(s))
def rip(op,target,tail=''):
 raw(op);b.extend(struct.pack('<i',target-(start+len(b)+4+len(bytes.fromhex(tail)))));raw(tail)
def branch(op,label):
 raw(op);local.append((len(b),label));b.extend(b'\0'*4)
def external(op,target):
 raw(op);ext.append(dict(Offset=start+len(b),Rva=target));b.extend(b'\0'*4)
raw('9c')
rip('83 3d',flag,'00');branch('0f 84','original')
# Additional guard protects unexpected transactions routed here with simulation flag.
raw('f6 81 b0 00 00 00 20');branch('0f 85','original')
rip('ff 05',counter);raw('9d b8 00 00 00 00');external('e9',rva+5)
labels['original']=start+len(b);raw('9d');external('e8',0x36d370);external('e9',rva+5)
for at,label in local:struct.pack_into('<i',b,at,labels[label]-(start+at+4))
assert len(b)<0x200
assert sum(i.size for i in g.md.disasm(bytes(b),start))==len(b)
hook=dict(Name='CraftingNoConsumption',Rva=rva,CodeOffset=start,Original=original.hex(),Context=g.read(rva,24).hex(),Code=b.hex(),Relocations=ext)
Path(__file__).with_name('CraftingHook.json').write_text(json.dumps(hook,indent=2),encoding='utf-8')
manifest_path=Path(__file__).resolve().parent.parent/'CharacterHooks.json'
manifest=json.loads(manifest_path.read_text('utf-8-sig'))
manifest['Hooks']=[h for h in manifest['Hooks'] if h['Name']!='CraftingNoConsumption']
assert all(not (start <= h['CodeOffset'] < start+0x200) for h in manifest['Hooks'])
manifest['Hooks'].append(hook)
manifest_path.write_text(json.dumps(manifest,indent=2),encoding='utf-8')

Path(__file__).with_name('hook_disasm.txt').write_text('\n'.join(f'{i.address:08x}: {i.mnemonic} {i.op_str}' for i in g.md.disasm(b,start)),encoding='utf-8')
print('Current-build recipe preservation hook validated:',len(b),'bytes')
