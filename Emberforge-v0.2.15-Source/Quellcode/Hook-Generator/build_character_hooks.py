"""Build position-independent, reversible x64 hooks for the verified game binary."""
from pathlib import Path
import json,struct,re,hashlib
import disasm_game as g

OUT=Path(__file__).resolve().parents[2]
SRC=OUT/'Quellcode'
SRC.mkdir(parents=True,exist_ok=True)
DATA=0x4000
class Asm:
 def __init__(self,start):self.start=start;self.b=bytearray();self.labels={};self.local=[];self.ext=[]
 @property
 def pos(self):return self.start+len(self.b)
 def raw(self,s):self.b.extend(bytes.fromhex(s))
 def rip(self,s,offset,tail=''):
  self.raw(s);self.b.extend(struct.pack('<i',DATA+offset-(self.pos+4+len(bytes.fromhex(tail)))));self.raw(tail)
 def label(self,s):self.labels[s]=self.pos
 def branch(self,op,label):
  self.raw(op);self.local.append((len(self.b),label));self.b.extend(b'\0'*4)
 def ret_to(self,rva):self.raw('E9');self.ext.append({'Offset':self.pos,'Rva':rva});self.b.extend(b'\0'*4)
 def address(self,rva):
  self.raw('48 8D 05');self.ext.append({'Offset':self.pos,'Rva':rva,'Kind':'Address'});self.b.extend(b'\0'*4)
 def finish(self):
  for p,l in self.local:struct.pack_into('<i',self.b,p,self.labels[l]-(self.start+p+4))
  assert len(self.b)<0x400
  return bytes(self.b)

hooks=[]
def add(name,rva,offset,original,build):
 a=Asm(offset);build(a);blob=a.finish()
 assert g.read(rva,len(original))==original
 ins=list(g.md.disasm(original,rva));assert sum(i.size for i in ins)==len(original)
 # Longer original context must be unique in the installed executable.
 context=g.read(rva,24)
 matches=[]
 for length in (24,48,96,192):
  context=g.read(rva,length)
  matches=[0x1000+m.start() for m in re.finditer(re.escape(context),g.read(0x1000,19550720))]
  if matches==[rva]:break
 assert matches==[rva],(name,matches)
 hooks.append({'Name':name,'Rva':rva,'CodeOffset':offset,'Original':original.hex(),'Code':blob.hex(),'Relocations':a.ext,'Context':context.hex()})

def capture(a,stamina=False):
 p=0x30 if stamina else 0x10
 a.raw('8B 04 91 89 44 24 '+('40' if stamina else '5C'))
 a.raw('9C 50')
 a.rip('89 05',p+0x18) # sampled current value
 a.rip('48 89 1D',p)   # attribute blob
 a.rip('89 15',p+0x10) # actual current attribute index, not an assumed neighbor
 a.raw('48 8D 04 91')
 a.rip('48 89 05',p+8) # exact current value address
 a.rip('FF 05',0x54 if stamina else 0x50)
 a.raw('58 9D');a.ret_to((0x23424e if stamina else 0x233f82)+7)

def maximum(a,stamina=False):
 p=0x30 if stamina else 0x10;flag=8 if stamina else 0
 a.raw('8B 04 91 89 44 24 '+('3C' if stamina else '30'))
 a.raw('9C 51 52')
 a.rip('89 05',p+0x1c);a.rip('89 15',p+0x14)
 a.rip('83 3D',flag,'00')
 if stamina:
  a.branch('0F 85','enabled');a.rip('83 3D',0x184,'00')
 a.branch('0F 84','done')
 a.label('enabled');a.rip('48 39 1D',p);a.branch('0F 85','done')
 a.raw('85 C0');a.branch('0F 8E','done')
 a.raw('3D 80 96 98 00');a.branch('0F 87','done')
 a.rip('48 8B 0D',p+8);a.raw('48 85 C9');a.branch('0F 84','done')
 a.raw('89 01')
 a.label('done');a.raw('5A 59 9D')
 a.ret_to((0x2342b9 if stamina else 0x233fe8)+7)

def fall(a):
 a.raw('9C 50')
 a.rip('83 3D',4,'00');a.branch('0F 85','check')
 a.rip('83 3D',0,'00');a.branch('0F 84','normal')
 a.label('check');a.raw('49 8D 04 88')
 a.rip('48 3B 05',0x18);a.branch('0F 85','normal')
 a.rip('FF 05',0x5c);a.raw('58 9D 48 8B CB');a.ret_to(0x2325a9)
 a.label('normal');a.raw('58 9D 45 89 0C 88 48 8B CB');a.ret_to(0x2325a9)

def commit(a):
 a.raw('9C 50 51 52')
 a.rip('FF 05',0x64)
 for p,flag,label,counter in [(0x10,0,'health_done',0x58),(0x30,8,'stamina_done',0x60),(0x80,0x70,'mana_done',0xa4),(0xb0,0x74,'cold_done',0xd4),(0xe0,0x78,'shroud_done',0x104)]:
  a.rip('83 3D',flag,'00')
  if flag==8:
   a.branch('0F 85','flight_stamina');a.rip('83 3D',0x184,'00');a.label('flight_stamina')
  a.branch('0F 84',label)
  a.rip('48 39 35',p);a.branch('0F 85',label)
  a.raw('F6 46 10 01');a.branch('0F 84',label)
  a.rip('8B 15',p+0x14);a.raw('3B 56 0C');a.branch('0F 83',label)
  a.raw('8B 4E 08 48 03 CE 8B 04 91 85 C0');a.branch('0F 8E',label)
  a.raw('3D 80 96 98 00');a.branch('0F 87',label)
  a.rip('8B 15',p+0x10);a.raw('3B 56 0C');a.branch('0F 83',label)
  a.raw('89 04 91');a.rip('FF 05',counter)
  a.label(label)
 a.raw('5A 59 58 9D 48 83 C4 50 5E C3')

def extra_capture(a,p,counter,rva,original,register):
 a.raw(original);a.raw('9C 50')
 a.rip(register,p+0x18);a.rip('48 89 1D',p);a.rip('89 15',p+0x10)
 a.raw('48 8D 04 91');a.rip('48 89 05',p+8);a.rip('FF 05',counter)
 a.raw('58 9D');a.ret_to(rva+len(bytes.fromhex(original)))

def extra_max(a,p,flag,rva,original,register):
 a.raw(original);a.raw('9C 50 51 52');a.raw(register)
 a.rip('89 05',p+0x1c);a.rip('89 15',p+0x14)
 a.rip('83 3D',flag,'00');a.branch('0F 84','done')
 a.rip('48 39 1D',p);a.branch('0F 85','done')
 a.raw('85 C0');a.branch('0F 8E','done');a.raw('3D 80 96 98 00');a.branch('0F 87','done')
 a.rip('48 8B 0D',p+8);a.raw('48 85 C9');a.branch('0F 84','done');a.raw('89 01')
 a.label('done');a.raw('5A 59 58 9D');a.ret_to(rva+len(bytes.fromhex(original)))

def shroud_query(a):
 # A generic getter is filtered by its exact two player-network callers.
 # Its return address is at original RSP+0x38 (push RDI; sub RSP,0x30).
 a.raw('9C 50 51 52')
 a.address(0x2346b0);a.raw('48 39 44 24 58');a.branch('0F 84','current')
 a.address(0x2346ce);a.raw('48 39 44 24 58');a.branch('0F 85','done')
 a.rip('48 39 3D',0xe0);a.branch('0F 85','done')
 a.rip('89 15',0xf4);a.raw('8B 04 91');a.rip('89 05',0xfc)
 a.rip('FF 05',0x10c)
 a.rip('83 3D',0x78,'00');a.branch('0F 84','done')
 a.raw('85 C0');a.branch('0F 8E','done');a.raw('3D 80 96 98 00');a.branch('0F 87','done')
 a.rip('48 8B 0D',0xe8);a.raw('48 85 C9');a.branch('0F 84','done');a.raw('89 01');a.branch('E9','done')
 a.label('current');a.rip('48 89 3D',0xe0);a.rip('89 15',0xf0)
 a.raw('8B 04 91');a.rip('89 05',0xf8);a.raw('48 8D 04 91');a.rip('48 89 05',0xe8);a.rip('FF 05',0x100)
 a.label('done');a.raw('5A 59 58 9D 8B 04 91 89 43 04');a.ret_to(0x21c30e)

def cold_damage(a):
 a.raw('9C 50');a.rip('83 3D',0x74,'00');a.branch('0F 84','normal')
 a.raw('49 8D 04 88');a.rip('48 3B 05',0x18);a.branch('0F 85','normal')
 a.rip('FF 05',0xd8);a.raw('58 9D 48 8B CB');a.ret_to(0x27e2fc)
 a.label('normal');a.raw('58 9D 41 29 04 88 48 8B CB');a.ret_to(0x27e2fc)

add('HealthCapture',0x233f82,0,bytes.fromhex('8B 04 91 89 44 24 5C'),capture)
add('HealthMaximum',0x233fe8,0x200,bytes.fromhex('8B 04 91 89 44 24 30'),maximum)
add('StaminaCapture',0x23424e,0x400,bytes.fromhex('8B 04 91 89 44 24 40'),lambda a:capture(a,True))
add('StaminaMaximum',0x2342b9,0x600,bytes.fromhex('8B 04 91 89 44 24 3C'),lambda a:maximum(a,True))
add('FallDamage',0x2325a2,0x800,bytes.fromhex('45 89 0C 88 48 8B CB'),fall)
add('AttributeCommit',0x1f3030,0xa00,bytes.fromhex('48 83 C4 50 5E C3'),commit)
add('ManaCapture',0x2343f5,0xe00,bytes.fromhex('C6 44 24 24 00 44 8B 2C 91'),lambda a:extra_capture(a,0x80,0xa0,0x2343f5,'C6 44 24 24 00 44 8B 2C 91','44 89 2D'))
add('ManaMaximum',0x23445c,0x1000,bytes.fromhex('C6 44 24 25 00 44 8B 24 91'),lambda a:extra_max(a,0x80,0x70,0x23445c,'C6 44 24 25 00 44 8B 24 91','44 89 E0'))
add('ColdCapture',0x234596,0x1200,bytes.fromhex('C6 44 24 28 00 44 8B 34 91'),lambda a:extra_capture(a,0xb0,0xd0,0x234596,'C6 44 24 28 00 44 8B 34 91','44 89 35'))
add('ColdMaximum',0x2345fe,0x1400,bytes.fromhex('C6 44 24 29 00 8B 34 91'),lambda a:extra_max(a,0xb0,0x74,0x2345fe,'C6 44 24 29 00 8B 34 91','89 F0'))
add('ShroudQuery',0x21c308,0x1600,bytes.fromhex('8B 04 91 89 43 04'),shroud_query)
add('ColdDamage',0x27e2f5,0x1800,bytes.fromhex('41 29 04 88 48 8B CB'),cold_damage)
manifest={'Build':'23966345','Sha256':hashlib.sha256(g.data).hexdigest(),'AllocationSize':24576,'DataOffset':DATA,'Hooks':hooks}
(SRC/'CharacterHooks.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
report=[]
for h in hooks:
 code=bytes.fromhex(h['Code']);instructions=list(g.md.disasm(code,h['CodeOffset']))
 assert sum(i.size for i in instructions)==len(code)
 report.append(h['Name']+' '+hex(h['Rva'])+' unique context, whole instructions, '+str(len(code))+' hook bytes')
 for i in instructions:report.append(f'  {i.address:04x} {i.mnemonic} {i.op_str}')
(OUT/'Hook-Pruefung.txt').write_text('\n'.join(report),encoding='utf-8')
print('\n'.join(line for line in report if not line.startswith('  ')))
