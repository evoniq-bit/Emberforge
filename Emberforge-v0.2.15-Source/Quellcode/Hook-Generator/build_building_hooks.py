"""Port the user-tested CT building paths to standalone, position-independent hooks.

No game process is opened. Exact executable bytes, contexts and instruction boundaries
are checked against the installed binary. Building code/data occupy their own pages
inside the character engine's unified allocation and recovery receipt.
"""
from pathlib import Path
import sys, struct, re, json, hashlib
for parent in Path(__file__).resolve().parents:
    dependency = parent/'work'/'deps'
    if dependency.exists(): sys.path.insert(0,str(dependency)); break
import disasm_game as g

SRC=Path(__file__).resolve().parent.parent
DATA=0x5000
class Asm:
    def __init__(self,start): self.start=start; self.b=bytearray(); self.labels={}; self.local=[]; self.ext=[]
    @property
    def pos(self): return self.start+len(self.b)
    def raw(self,s): self.b.extend(bytes.fromhex(s))
    def rip(self,s,offset,tail=''):
        self.raw(s); self.b.extend(struct.pack('<i',DATA+offset-(self.pos+4+len(bytes.fromhex(tail))))); self.raw(tail)
    def label(self,s): self.labels[s]=self.pos
    def branch(self,op,label): self.raw(op); self.local.append((len(self.b),label)); self.b.extend(b'\0'*4)
    def ret_to(self,rva): self.raw('E9'); self.ext.append({'Offset':self.pos,'Rva':rva}); self.b.extend(b'\0'*4)
    def finish(self):
        for p,label in self.local: struct.pack_into('<i',self.b,p,self.labels[label]-(self.start+p+4))
        assert len(self.b)<0x200, len(self.b)
        return bytes(self.b)

hooks=[]
def add(name,pattern,length,build):
    # The CT's uniquely matched original signatures, without accepting updated builds.
    matches=[]
    for section,rva,raw,size in g.secs:
        if section=='.text':
            matches.extend(rva+m.start() for m in re.finditer(re.escape(bytes.fromhex(pattern)),g.data[raw:raw+size]))
    assert len(matches)==1,(name,matches)
    rva=matches[0]; original=g.read(rva,length)
    assert sum(i.size for i in g.md.disasm(original,rva))==length
    offset=0x2000+len(hooks)*0x200
    a=Asm(offset);build(a,original,rva+length);code=a.finish()
    assert sum(i.size for i in g.md.disasm(code,offset))==len(code)
    hooks.append({'Name':name,'Rva':rva,'CodeOffset':offset,'Original':original.hex(),
                  'Code':code.hex(),'Context':g.read(rva,24).hex(),'Relocations':a.ext})

def map_material(a):
    # Exact cross-type mapping from TEST v3: one valid target covers both source kinds.
    a.rip('83 3D',0,'00');a.branch('0F 84','mapped')
    a.rip('83 3D',8,'01');a.branch('0F 8C','block_only')
    a.rip('81 3D',8,'7F 00 00 00');a.branch('0F 87','block_only')
    a.rip('83 3D',12,'00');a.branch('0F 8C','terrain')
    a.rip('81 3D',12,'7F 00 00 00');a.branch('0F 87','terrain')
    a.raw('3D 80 00 00 00');a.branch('0F 83','block')
    a.label('terrain');a.rip('8B 05',8);a.branch('E9','mapped')
    a.label('block_only');a.rip('83 3D',12,'00');a.branch('0F 8C','mapped')
    a.rip('81 3D',12,'7F 00 00 00');a.branch('0F 87','mapped')
    a.label('block');a.rip('8B 05',12);a.raw('05 80 00 00 00')
    a.label('mapped')

def block(a,original,ret,kind):
    a.raw(original.hex());a.raw('9C')
    if kind=='lookup':a.raw('50 89 D0')
    elif kind=='preview':a.raw('50 44 89 E8')
    a.rip('89 05',0x20 if kind=='place' else 0x2c)
    a.rip('FF 05',0x28 if kind=='place' else 0x34)
    map_material(a)
    a.rip('89 05',0x24 if kind=='place' else 0x30)
    if kind=='lookup':a.raw('89 C2 58')
    elif kind=='preview':a.raw('41 89 C5 58')
    a.raw('9D');a.ret_to(ret)

def material_capture(a,original,ret):
    # Local client_cursor camera ray return, not the old slide_fx ground query.
    assert g.read(0x249c61,5)==bytes.fromhex('e89a86ffff')
    a.raw(original.hex());a.raw('9C 50')
    for offset in (0x80,0x88,0x98):a.rip('C7 05',offset,'FF FF FF FF')
    a.rip('FF 05',0x90)
    a.raw('0F B6 85 28 09 00 00');a.rip('89 05',0x94)
    a.raw('0F B6 85 85 09 00 00');a.rip('89 05',0xa0)
    a.raw('0F B6 85 86 09 00 00');a.rip('89 05',0xa8)
    a.rip('83 3D',0x164,'01');a.branch('0F 85','done')
    a.raw('8B 85 08 01 00 00');a.rip('3B 05',0x160);a.branch('0F 85','done')
    a.raw('80 BD 28 09 00 00 00');a.branch('0F 85','done')
    a.raw('80 BD 85 09 00 00 01');a.branch('0F 85','done')
    a.raw('0F B6 85 84 09 00 00 85 C0');a.branch('0F 84','done')
    a.rip('89 05',0x98);a.rip('FF 05',0x9c)
    a.raw('3D 80 00 00 00');a.branch('0F 83','block')
    a.rip('89 05',0x80);a.branch('E9','done')
    a.label('block');a.raw('2D 80 00 00 00');a.rip('89 05',0x88)
    a.label('done');a.raw('58 9D');a.ret_to(ret)

def prop(a,original,ret,preview):
    p=0x440 if preview else 0x430
    a.raw(original.hex());a.raw('9C 50')
    a.rip('FF 05',0x404 if preview else 0x400)
    a.rip('4C 89 05' if preview else '48 89 3D',0x428 if preview else 0x420)
    a.raw('41 8B 00' if preview else '8B 07');a.rip('89 05',0x40c if preview else 0x408)
    a.rip('0F 11 05',p)
    a.rip('83 3D',4,'00');a.branch('0F 84','done')
    # Fixed source compares both halves of the loaded UUID; it never modifies definitions.
    a.rip('83 3D',0x4c0,'00');a.branch('0F 84','target')
    a.raw('66 48 0F 7E C0');a.rip('48 3B 05',0x4b0 if preview else 0x4a0);a.branch('0F 85','done')
    a.raw('49 8B 80 5C 03 00 00' if preview else '48 8B 87 B0 03 00 00')
    a.rip('48 3B 05',0x4b8 if preview else 0x4a8);a.branch('0F 85','done')
    a.label('target');a.rip('48 8B 05',0x490 if preview else 0x480)
    a.rip('48 0B 05',0x498 if preview else 0x488);a.branch('0F 84','done')
    a.rip('0F 10 05',0x490 if preview else 0x480)
    a.label('done');a.rip('0F 11 05',0x460 if preview else 0x450)
    a.raw('58 9D');a.ret_to(ret)

def inspector(a,original,ret):
    a.raw('9C 50 51 52');a.rip('FF 05',0x110)
    a.raw('49 8B 07 48 85 C0');a.branch('0F 84','done');a.rip('48 89 05',0x100)
    a.raw('48 8B 50 30 48 85 D2');a.branch('0F 84','done');a.rip('48 89 15',0x120)
    a.raw('48 8B 82 88 01 00 00');a.rip('48 89 05',0x128)
    a.raw('48 8B 82 58 01 00 00');a.rip('48 89 05',0x130)
    a.raw('48 8B 82 50 01 00 00');a.rip('48 89 05',0x138)
    a.raw('48 89 D8 31 D2 B9 88 00 00 00 48 F7 F1 85 D2');a.branch('0F 85','done')
    a.raw('83 F8 0F');a.branch('0F 87','done')
    a.raw('89 C1 48 C1 E0 05');a.rip('48 8D 15',0x200);a.raw('48 01 C2 48 89 2A')
    a.rip('48 8B 05',0x100);a.raw('48 89 42 08 FF 42 10')
    a.raw('83 F9 0A');a.branch('0F 85','done')
    # Merge into existing inspector() after slot10 cmp gate, before aimed pointer/counter.
    # Verified 8D3490 current-entity logic: jobcontext+8 = iteration ordinal (1-based),
    # context[0]+7E0 = world current-query entity records, stride16, firstdword=entityID.
    a.raw('41 8B 4F 08 85 C9')              # mov ecx,[r15+8]; test ecx,ecx
    # Native iteration should be valid; reject no currententity.
    a.branch('0F 84', 'done')
    a.raw('FF C9 48 C1 E1 04 49 8B 07')   # dec ecx; shl rcx,4; mov rax,[r15]
    a.raw('48 8B 80 E0 07 00 00 8B 0C 08') # mov rax,[rax+7E0]; mov ecx,[rax+rcx]
    # Gate to heldcursorlocalactor (must exist beforepublishing) avoidsremoteplayer target.
    a.rip('3B 0D',0x160)                  # cmp ecx,[data+160]
    a.branch('0F 85', 'done')
    a.rip('89 0D',0x148) # publish actor only after local-owner match
    a.rip('48 89 3D', 0x140)              # mov [BuildingData+140], RDI
    # Existing code remains:
    a.rip('48 89 2D', 0x108)
    a.rip('FF 05', 0x114)
    a.label('done');a.raw('5A 59 58 9D');a.raw(original.hex());a.ret_to(ret)

add('BuildingPlace','41 0F B6 86 38 04 00 00 33 FF 88 85 80 02 00 00 41 38 7F 20',8,lambda a,o,r:block(a,o,r,'place'))
add('BuildingLookup','41 0F B6 96 38 04 00 00 48 8B 8E E8 00 00 00',8,lambda a,o,r:block(a,o,r,'lookup'))
add('BuildingPreview','45 0F B6 AE 38 04 00 00 4C 8D 44 24 50 0F 57 C9 48 89 9C 24',8,lambda a,o,r:block(a,o,r,'preview'))
add('BuildingTarget','4C 8D 8D 28 09 00 00 4C 8D 44 24 50 48 8D 95 10 03 00 00',7,material_capture)
add('BuildingPropPlace','0F 10 87 A8 03 00 00 48 8B D7 48 8D 8D 10 02 00 00 66 4C 0F 7E C0',7,lambda a,o,r:prop(a,o,r,False))
add('BuildingPropPreview','41 0F 10 80 54 03 00 00 EB 18 49 8B 80 6C 03 00 00',8,lambda a,o,r:prop(a,o,r,True))
add('BuildingInspector','48 8B 00 48 85 C0 75 1D 4C 8B 44 24 58 48 8D 8C 24 C0 00 00 00',6,inspector)
manifest={'Build':'23966345','Sha256':hashlib.sha256(g.data).hexdigest(),'AllocationSize':0x6000,'DataOffset':DATA,'Hooks':hooks}
(SRC/'BuildingHooks.json').write_text(json.dumps(manifest,indent=2),encoding='utf8')
report=[]
for hook in hooks:
    report.append(hook['Name']+' '+hex(hook['Rva'])+' '+str(len(bytes.fromhex(hook['Code'])))+' bytes, signature unique, original instructions complete')
    report.extend('  '+hex(i.address)+' '+i.mnemonic+' '+i.op_str for i in g.md.disasm(bytes.fromhex(hook['Code']),hook['CodeOffset']))
(SRC/'Building-Hook-Pruefung.txt').write_text('\n'.join(report),encoding='utf8')
print('\n'.join(line for line in report if not line.startswith('  ')))
