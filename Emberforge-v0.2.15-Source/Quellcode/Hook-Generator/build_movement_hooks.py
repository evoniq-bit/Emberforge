"""Emberforge-specific reversible player hooks; current executable only.

Glider flight is adapted from Turk's supplied Glider Flight script.
Emberforge provides integration, filtering, validation and recovery.
"""
from pathlib import Path
import sys, json, struct, re
for parent in Path(__file__).resolve().parents:
    if (parent/'work'/'deps').exists():
        sys.path.insert(0,str(parent/'work'/'deps'))
import disasm_game as g
SRC=Path(__file__).resolve().parents[1]
DATA=0x4000
class Asm:
    def __init__(self,start): self.start=start;self.b=bytearray();self.labels={};self.local=[];self.ext=[]
    @property
    def pos(self):return self.start+len(self.b)
    def raw(self,s):self.b.extend(bytes.fromhex(s))
    def rip(self,s,off,tail=''):
        self.raw(s);self.b.extend(struct.pack('<i',DATA+off-(self.pos+4+len(bytes.fromhex(tail)))));self.raw(tail)
    def label(self,s):self.labels[s]=self.pos
    def branch(self,op,label):self.raw(op);self.local.append((len(self.b),label));self.b.extend(bytes(4))
    def external(self,op,rva):self.raw(op);self.ext.append(dict(Offset=self.pos,Rva=rva));self.b.extend(bytes(4))
    def finish(self):
        for p,l in self.local:struct.pack_into('<i',self.b,p,self.labels[l]-(self.start+p+4))
        return self.b.hex()
hooks=[]
def add(name,rva,offset,n,fn):
    original=g.read(rva,n);assert sum(i.size for i in g.md.disasm(original,rva))==n
    a=Asm(offset);fn(a,original);code=a.finish()
    assert sum(i.size for i in g.md.disasm(bytes.fromhex(code),offset))==len(a.b)
    context=g.read(rva,48);hits=[]
    for sec,va,raw,size in g.secs:
        if sec=='.text':hits.extend(va+m.start() for m in re.finditer(re.escape(context),g.data[raw:raw+size]))
    assert hits==[rva],(name,hits)
    hooks.append(dict(Name=name,Rva=rva,CodeOffset=offset,Original=original.hex(),Context=context.hex(),Code=code,Relocations=a.ext))

def breath(a,original):
    # oxygen_update's optional Health component is at original RSP+48.
    # Compare against the exact local-player health component captured by the
    # established character hook. Never freeze NPC oxygen or change the jump.
    a.raw('9C 50 48 8B 44 24 58 48 85 C0');a.branch('0F 84','normal')
    a.rip('48 3B 05',0x10);a.branch('0F 85','normal')
    a.rip('48 89 1D',0x1b0);a.rip('FF 05',0x190)
    a.rip('83 3D',0x180,'00');a.branch('0F 84','normal')
    a.rip('FF 05',0x194);a.raw('58 9D');a.external('E9',0x1f925e)
    a.label('normal');a.raw('58 9D 41 29 04 88');a.external('E9',0x1f925e)

add('PlayerBreathConsumption',0x1f9102,0xc40,9,breath)
def turk_flight(a,original):
    # Adaptation of Turk's "Glider Flight", dated 2024-04-14.
    # The original changes the minimum pitch bound to -1.57 radians.
    # Emberforge adds a toggle, local-player filter and reversible relocation.
    # There is no Y-velocity override, horizontal cache or altitude lock.
    a.raw('9C 50');a.rip('83 3D',0x184,'00');a.branch('0F 84','normal')
    a.raw('48 8B 45 20 48 85 C0');a.branch('0F 84','normal')
    a.rip('48 3B 05',0x1a0);a.branch('0F 85','normal')
    a.rip('FF 05',0x1f0);a.raw('B8 C3 F5 C8 BF 66 0F 6E C0 58 9D');a.external('E9',0x22afe9)
    a.label('normal');a.raw('58 9D');a.external('F3 0F 10 05',0x1400330);a.external('E9',0x22afe9)
add('TurkGliderFlightPitch',0x22afe1,0x2e00,8,turk_flight)
def dismantle(a,original):
    # Tag only placements carrying the actual selected replacement UUID.
    # A zero origin-item argument selects the world's normal object-removal path.
    # Other props, source filtering, placement flags and inventory remain untouched.
    a.raw(original.hex());a.raw('9C 50')
    a.rip('83 3D',0x188,'00');a.branch('0F 84','done')
    a.rip('83 3D',0x1004,'00');a.branch('0F 84','done')
    a.raw('48 8B 02');a.rip('48 3B 05',0x1480);a.branch('0F 85','done')
    a.raw('48 8B 42 08');a.rip('48 3B 05',0x1488);a.branch('0F 85','done')
    a.rip('44 89 05',0x1d0);a.raw('45 33 C0');a.rip('FF 05',0x1d4)
    a.label('done');a.raw('58 9D');a.external('E9',0x282c30)
add('OverriddenPropRemovalOrigin',0x282c26,0xcc0,10,dismantle)
manifest=json.loads((SRC/'CharacterHooks.json').read_text(encoding='utf-8-sig'))
assert manifest['Sha256']=='af2f5a1227911d8aa06b3908d6bd0211838211cae14ea91099cb57d0df990781'
removed={'PlayerGliderVerticalVelocity','PlayerGliderLocomotionVelocity'}
manifest['Hooks']=[h for h in manifest['Hooks'] if h['Name'] not in removed|{x['Name'] for x in hooks}]+hooks
spans=[];patches=[]
for h in manifest['Hooks']:
    s=(h['CodeOffset'],h['CodeOffset']+len(bytes.fromhex(h['Code'])))
    assert s[1]<=manifest['DataOffset'] and all(s[1]<=p[0] or s[0]>=p[1] for p in spans),(h['Name'],s)
    spans.append(s);p=(h['Rva'],h['Rva']+len(bytes.fromhex(h['Original'])))
    assert all(p[1]<=x[0] or p[0]>=x[1] for x in patches);patches.append(p)
(SRC/'CharacterHooks.json').write_text(json.dumps(manifest,indent=2),encoding='utf8')
for h in hooks:print(h['Name'],hex(h['Rva']),len(bytes.fromhex(h['Code'])))

