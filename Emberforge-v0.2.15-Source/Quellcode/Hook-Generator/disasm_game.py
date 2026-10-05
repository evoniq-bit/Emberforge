import sys, struct, re
from pathlib import Path
sys.path.insert(0,str(Path(__file__).parent/'deps'))
from capstone import Cs, CS_ARCH_X86, CS_MODE_64

data=Path(r'C:\Program Files (x86)\Steam\steamapps\common\Enshrouded\enshrouded.exe').read_bytes()
pe=struct.unpack_from('<I',data,0x3c)[0]
ns=struct.unpack_from('<H',data,pe+6)[0]
ol=struct.unpack_from('<H',data,pe+20)[0]
secs=[]
for i in range(ns):
 p=pe+24+ol+i*40
 name=data[p:p+8].rstrip(b'\0').decode()
 size,rva,rawsize,raw=struct.unpack_from('<IIII',data,p+8)
 secs.append((name,rva,raw,rawsize))
def read(rva,n):
 for _,va,raw,size in secs:
  if va<=rva<va+size:return data[raw+rva-va:raw+rva-va+n]
 return b''
md=Cs(CS_ARCH_X86,CS_MODE_64)
if __name__ != '__main__':
 pass
elif len(sys.argv)>1:
 for arg in sys.argv[1:]:
  a,n=(arg.split(':')+["300"])[:2]
  addr=int(a,16); count=int(n,16)
  print('\nDISASM',hex(addr))
  for ins in md.disasm(read(addr,count),addr):print(f'{ins.address:08X}: {ins.bytes.hex(" "):28s} {ins.mnemonic} {ins.op_str}')
else:
 for name,rva,raw,size in secs:
  if name!='.text':continue
  code=data[raw:raw+size]
  for m in re.finditer(rb'\x0f[\xb6\xb7][\x80-\x87]....',code,re.DOTALL):
   off=int.from_bytes(m.group()[3:7],'little')
   if not 0x300<=off<=0x650:continue
   p=m.start()
   if p and code[p-1] in range(0x40,0x50):p-=1
   ins=next(md.disasm(code[p:p+12],rva+p),None)
   print(hex(rva+p),ins.mnemonic,ins.op_str,' | ','; '.join(i.mnemonic+' '+i.op_str for i in md.disasm(code[p+ins.size:p+ins.size+36],rva+p+ins.size)))
