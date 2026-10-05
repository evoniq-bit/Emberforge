"""Current held pickaxe capture from local client_cursor; no running process access."""
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
# Reflection verifies ItemDefinition.flags offset56E, IsTerraformer enum bit6,
# equipment104 + EquipmentSetup.terraformingType3D4 =4D8, Remove enum=0.
assert struct.unpack('<Q',g.read(0x15b4588,8))[0]==0x56e
assert struct.unpack('<Q',g.read(0x1af8170,8))[0]==6
assert struct.unpack('<Q',g.read(0x1b3cfb8,8))[0]==0x3d4
assert struct.unpack('<Q',g.read(0x190cf60,8))[0]==0
specs=[]
for name,rva,start,original in [('HeldPickaxeCapture',0x2491b7,0x3a00,'4c8bf8488b85d0010000'),('HeldPickaxeClear',0x249188,0x3c00,'488b85c0010000')]:
 assert g.read(rva,len(bytes.fromhex(original)))==bytes.fromhex(original)
 b=bytearray();labels={};local=[];ext=[]
 def raw(s):b.extend(bytes.fromhex(s))
 def rip(op,target,tail=''):
  raw(op);b.extend(struct.pack('<i',target-(start+len(b)+4+len(bytes.fromhex(tail)))));raw(tail)
 def branch(op,label):raw(op);local.append((len(b),label));b.extend(b'\0'*4)
 def external(op,target):raw(op);ext.append(dict(Offset=start+len(b),Rva=target));b.extend(b'\0'*4)
 raw('9c 50')
 if name=='HeldPickaxeCapture':
  rip('48 89 05',0x5150)
  rip('c7 05',0x5164,'00 00 00 00')
  raw('48 85 c0');branch('0f 84','done')
  raw('f6 80 6e 05 00 00 40');branch('0f 84','done')
  raw('80 b8 d8 04 00 00 00');branch('0f 85','done')
  rip('c7 05',0x5164,'01 00 00 00')
  labels['done']=start+len(b)
  raw('8b 85 08 01 00 00');rip('89 05',0x5160)
  rip('ff 05',0x5158)
 else:
  rip('48 c7 05',0x5150,'00 00 00 00')
  rip('c7 05',0x5160,'00 00 00 00');rip('c7 05',0x5164,'00 00 00 00')
  # Invalidate previous ray before every local iteration, including branches that skip querying.
  rip('c7 05',0x5094,'01 00 00 00');rip('c7 05',0x50a0,'00 00 00 00');rip('c7 05',0x5098,'ff ff ff ff')
  rip('ff 05',0x5158)
 raw('58 9d');raw(original);external('e9',rva+len(bytes.fromhex(original)))
 for at,label in local:struct.pack_into('<i',b,at,labels[label]-(start+at+4))
 assert len(b)<0x200 and sum(i.size for i in g.md.disasm(b,start))==len(b)
 specs.append(dict(Name=name,Rva=rva,CodeOffset=start,Original=original,Context=g.read(rva,24).hex(),Code=b.hex(),Relocations=ext))
(here/'HeldPickaxeHooks.json').write_text(json.dumps(specs,indent=2),encoding='utf-8')
(here/'held_hook_disasm.txt').write_text('\n\n'.join(s['Name']+'\n'+'\n'.join(f'{i.address:08x}: {i.mnemonic} {i.op_str}' for i in g.md.disasm(bytes.fromhex(s['Code']),s['CodeOffset'])) for s in specs),encoding='utf-8')
parser=argparse.ArgumentParser();parser.add_argument('--manifest',type=Path);args=parser.parse_args();manifest=args.manifest or (here.parent/'CharacterHooks.json' if (here.parent/'CharacterHooks.json').exists() else None)
if manifest:
 m=json.loads(manifest.read_text(encoding='utf-8-sig'));assert m['Sha256']==SHA
 m['Hooks']=[h for h in m['Hooks'] if h['Name'] not in {s['Name'] for s in specs}]+specs
 spans=sorted((h['CodeOffset'],h['CodeOffset']+len(bytes.fromhex(h['Code'])),h['Name']) for h in m['Hooks']);assert all(a[1]<=b[0] for a,b in zip(spans,spans[1:]));assert spans[-1][1]<=0x4000
 manifest.write_text(json.dumps(m,indent=2),encoding='utf-8')
print('Held pickaxe capture/clear validated. ptr5150 counter5158 entity5160 pickaxe5164.')
