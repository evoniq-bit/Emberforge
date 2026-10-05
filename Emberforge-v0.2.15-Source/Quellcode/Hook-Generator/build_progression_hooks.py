import sys,struct,json
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent));import disasm_game as g
ROOT=Path(__file__).resolve().parents[1];DATA=0x5700
class Asm:
 def __init__(self,start):self.start=start;self.b=bytearray();self.labels={};self.local=[];self.ext=[]
 @property
 def pos(self):return self.start+len(self.b)
 def raw(self,s):self.b.extend(bytes.fromhex(s))
 def rip(self,s,offset,tail=''):
  self.raw(s);self.b.extend(struct.pack('<i',DATA+offset-(self.pos+4+len(bytes.fromhex(tail)))));self.raw(tail)
 def label(self,s):self.labels[s]=self.pos
 def branch(self,op,label):self.raw(op);self.local.append((len(self.b),label));self.b.extend(b'\0'*4)
 def external(self,op,rva):self.raw(op);self.ext.append(dict(Offset=self.pos,Rva=rva));self.b.extend(b'\0'*4)
 def finish(self):
  for p,l in self.local:struct.pack_into('<i',self.b,p,self.labels[l]-(self.start+p+4))
  return self.b.hex()
def component(a):
 # RDX entity, ECX fixed component index. Metadata verified against the current build.
 a.raw('4C 8B 52 18 48 8B 42 20 4D 85 D2');a.branch('0F 84','missing')
 a.raw('48 85 C0');a.branch('0F 84','missing')
 a.raw('41 0F A3 0A');a.branch('0F 83','missing')
 a.raw('45 0F B7 9C 4A 84 0A 00 00 45 85 DB');a.branch('0F 84','missing')
 a.raw('44 8B 42 30 45 85 C0');a.branch('0F 88','missing')
 a.raw('4D 0F AF C3 45 0F B7 9C 4A 84 00 00 00 4C 01 C0 4C 01 D8 C3')
 a.label('missing');a.raw('31 C0 C3')
a=Asm(0x1a00)
# Preserve the original selection load and all flags/registers touched by this hook.
a.raw('48 8B 30 9C 50 51 52 57 41 50 41 51 41 52 41 53')
a.raw('48 85 DB');a.branch('0F 84','done');a.raw('83 7B 10 01');a.branch('0F 85','done')
a.raw('48 85 F6');a.branch('0F 84','done')
a.rip('48 89 1D',0x10);a.rip('48 89 35',0x18);a.raw('49 8B 07');a.rip('48 89 05',0x20)
a.rip('FF 05',8)
a.rip('83 3D',0,'00');a.branch('0F 84','done')
a.rip('48 3B 1D',0x28);a.branch('0F 85','reject')
a.rip('8B 3D',0x40);a.raw('85 FF');a.branch('0F 8E','reject')
a.rip('83 3D',0,'01');a.branch('0F 85','stack')
a.raw('81 FF 40 42 0F 00');a.branch('0F 87','reject')
a.raw('48 89 DA B9 BF 00 00 00');a.branch('E8','component');a.raw('48 85 C0');a.branch('0F 84','reject')
a.raw('81 38 F4 DA CD 8D');a.branch('0F 85','reject');a.raw('83 78 08 24');a.branch('0F 85','reject')
a.raw('83 78 0C 03');a.branch('0F 85','reject');a.raw('F6 40 10 01');a.branch('0F 84','reject')
a.raw('8B 48 28 85 C9');a.branch('0F 88','reject');a.raw('01 F9');a.branch('0F 80','reject');a.raw('8B 50 24 85 D2');a.branch('0F 88','reject');a.raw('01 CA');a.branch('0F 80','reject');a.raw('89 48 28');a.branch('E9','success')
a.label('stack');a.rip('83 3D',0,'03');a.branch('0F 85','reject');a.raw('81 FF 50 C3 00 00');a.branch('0F 87','reject')
a.raw('0F B6 8E F8 00 00 00');a.rip('3B 0D',0x54);a.branch('0F 85','reject');a.raw('83 F9 0F');a.branch('0F 87','reject')
a.raw('89 C8 C1 E8 03 8B 94 86 D8 00 00 00');a.rip('3B 15',0x44);a.branch('0F 85','reject')
a.raw('83 E1 07');a.rip('3B 0D',0x48);a.branch('0F 85','reject')
a.rip('48 8B 15',0x30);a.raw('48 85 D2');a.branch('0F 84','reject')
a.raw('8B 42 10');a.rip('3B 05',0x44);a.branch('0F 85','reject')
a.raw('B9 09 01 00 00');a.branch('E8','component');a.raw('48 85 C0');a.branch('0F 84','reject')
a.rip('48 3B 05',0x38);a.branch('0F 85','reject')
a.rip('8B 0D',0x48);a.raw('48 8D 0C 49 48 8D 04 88 8B 08');a.rip('3B 0D',0x4c);a.branch('0F 85','reject')
a.raw('81 F9 DC 92 A7 41');a.branch('0F 84','reject')
a.raw('8B 48 04');a.rip('3B 0D',0x50);a.branch('0F 85','reject');a.raw('85 C9');a.branch('0F 8E','reject')
a.raw('83 78 08 00');a.branch('0F 85','reject');a.raw('89 78 04')
a.label('success');a.rip('C7 05',0xc,'01 00 00 00');a.branch('E9','finish')
a.label('reject');a.rip('C7 05',0xc,'02 00 00 00')
a.label('finish');a.rip('C7 05',0,'00 00 00 00')
a.label('done');a.raw('41 5B 41 5A 41 59 41 58 5F 5A 59 58 9D')
a.external('0F 84',0x28eec7);a.external('E9',0x28ed9c)
a.label('component');component(a)
assert len(a.b)<=0x400,len(a.b)
hooks=[dict(Name='ProgressionActions',Rva=0x28ed93,CodeOffset=a.start,Original=g.read(0x28ed93,9).hex(),Context=g.read(0x28ed93,24).hex(),Code=a.finish(),Relocations=a.ext)]
a=Asm(0x1e00);a.raw('03 CA 9C 50')
# Only local actor 1. The game derives the base total from level and unlocked roots.
a.raw('83 7C 24 70 01');a.branch('0F 85','done')
a.rip('89 0D',0x70);a.rip('FF 05',0x74);a.rip('8B 05',4);a.raw('85 C0');a.branch('0F 88','done')
a.raw('3D 50 C3 00 00');a.branch('0F 87','done');a.raw('01 C1');a.rip('89 0D',0x78)
a.label('done');a.raw('58 9D 89 4B 04 C6 03 00');a.external('E9',0x26cbbe)
hooks.append(dict(Name='AdditionalSkillPoints',Rva=0x26cbb5,CodeOffset=a.start,Original=g.read(0x26cbb5,9).hex(),Context=g.read(0x26cbb5,24).hex(),Code=a.finish(),Relocations=a.ext))
assert hooks[0]['Original']=='488b300f842b010000'
assert hooks[1]['Original']=='03ca894b04c6030048' # last byte is partial instruction -- fixed below
# Use 8 bytes, ending before the next instruction at 26CBBD.
hooks[1]['Original']=g.read(0x26cbb5,8).hex();hooks[1]['Relocations'][-1]['Rva']=0x26cbbd
a=Asm(0x1f00);a.raw('9C');a.rip('48 89 15',0x68);a.raw('9D');a.raw(g.read(0xcb4b50,8).hex());a.external('E9',0xcb4b58)
hooks.append(dict(Name='ProgressionItemDefinitions',Rva=0xcb4b50,CodeOffset=a.start,Original=g.read(0xcb4b50,8).hex(),Context=g.read(0xcb4b50,24).hex(),Code=a.finish(),Relocations=a.ext))
manifest=json.loads((ROOT/'CharacterHooks.json').read_text(encoding='utf-8-sig'))
manifest['Hooks']=[h for h in manifest['Hooks'] if h['Name'] not in {x['Name'] for x in hooks}]+hooks
(ROOT/'CharacterHooks.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
for h in hooks:print(h['Name'],len(bytes.fromhex(h['Code'])))
