"""Current-build durability preservation; no process writes. Only three durability CALLs, negative deltas only."""
from pathlib import Path
import sys,struct,json,hashlib,argparse
here=Path(__file__).resolve().parent
for p in [here,*here.parents]:
 if (p/'work'/'deps').exists():sys.path.insert(0,str(p/'work'/'deps'));break
for p in [here,*here.parents]:
 if (p/'disasm_game.py').exists():sys.path.insert(0,str(p));break
 if (p/'work'/'disasm_game.py').exists():sys.path.insert(0,str(p/'work'));break
import disasm_game as g
SHA='af2f5a1227911d8aa06b3908d6bd0211838211cae14ea91099cb57d0df990781'
assert hashlib.sha256(g.data).hexdigest()==SHA
# Names come from independently traced game ECS registrations: durability_loss and durability_burndown.
# The hit consumer supplies -1; event and timed loss supply signed negative deltas.
# Exact negative-only CALL scoping avoids hooking the generic component-update helper.
flag=0x407c
specs=[]
for name,rva,start in [('DurabilityUse',0x1e965e,0x3400),('DurabilityContinuous',0x1f6899,0x3600),('DurabilityEvents',0x331985,0x3800)]:
 original=g.read(rva,5)
 assert original[0]==0xe8 and rva+5+struct.unpack_from('<i',original,1)[0]==0x3319b0
 b=bytearray();labels={};local=[];ext=[]
 def raw(s):b.extend(bytes.fromhex(s))
 def rip(op,target,tail=''):
  raw(op);b.extend(struct.pack('<i',target-(start+len(b)+4+len(bytes.fromhex(tail)))));raw(tail)
 def branch(op,label):raw(op);local.append((len(b),label));b.extend(b'\0'*4)
 def external(op,target):raw(op);ext.append(dict(Offset=start+len(b),Rva=target));b.extend(b'\0'*4)
 raw('9c');rip('83 3d',flag,'00');branch('0f 84','original')
 raw('45 85 c0');branch('0f 89','original') # never block repairs or zero delta
 raw('48 85 c9');branch('0f 84','original') # preserve helper's null-component path
 raw('f6 41 10 01');branch('0f 84','original') # preserve invalid/inactive component handling
 raw('9d');external('e9',rva+5) # no decrement, no break event, caller continues
 labels['original']=start+len(b);raw('9d');external('e8',0x3319b0);external('e9',rva+5)
 for at,label in local:struct.pack_into('<i',b,at,labels[label]-(start+at+4))
 assert len(b)<0x200 and sum(i.size for i in g.md.disasm(b,start))==len(b)
 specs.append(dict(Name=name,Rva=rva,CodeOffset=start,Original=original.hex(),Context=g.read(rva,24).hex(),Code=b.hex(),Relocations=ext))
(here/'DurabilityHooks.json').write_text(json.dumps(specs,indent=2),encoding='utf-8')
(here/'hook_disasm.txt').write_text('\n\n'.join(s['Name']+'\n'+'\n'.join(f'{i.address:08x}: {i.mnemonic} {i.op_str}' for i in g.md.disasm(bytes.fromhex(s['Code']),s['CodeOffset'])) for s in specs),encoding='utf-8')
parser=argparse.ArgumentParser();parser.add_argument('--manifest',type=Path);args=parser.parse_args()
# When copied into Quellcode/Hook-Generator, update the neighboring manifest automatically.
manifest=args.manifest or (here.parent/'CharacterHooks.json' if (here.parent/'CharacterHooks.json').exists() else None)
if manifest:
 m=json.loads(manifest.read_text(encoding='utf-8-sig'));assert m['Sha256']==SHA
 m['Hooks']=[h for h in m['Hooks'] if h['Name'] not in {s['Name'] for s in specs}]+specs
 spans=sorted((h['CodeOffset'],h['CodeOffset']+len(bytes.fromhex(h['Code'])),h['Name']) for h in m['Hooks'])
 assert all(a[1]<=b[0] for a,b in zip(spans,spans[1:]));assert spans[-1][1]<=0x4000
 manifest.write_text(json.dumps(m,indent=2),encoding='utf-8')
print('Three independently derived current-build durability CALLs validated; negative-only preservation flag 407C.')
