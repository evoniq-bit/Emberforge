using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Emberforge {
 internal static class BuildingNativeTests {
  [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate ulong Call();
  static void Put(long a,int v){Marshal.WriteInt32(new IntPtr(a),v);}
  static int Get(long a){return Marshal.ReadInt32(new IntPtr(a));}
  internal static string Run(){
   var checks=new List<string>();Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception("BUILDING TEST FAILED: "+name);checks.Add("PASS "+name);};
   var manifest=Resources.Json.Deserialize<HookManifest>(Resources.Text("BuildingHooks.json"));
   IntPtr memory=Native.VirtualAlloc(IntPtr.Zero,(UIntPtr)0x20000,0x3000,0x40);if(memory==IntPtr.Zero)throw new Exception("Building test allocation failed.");
   try{
    long origin=memory.ToInt64(),data=origin+manifest.DataOffset,item=origin+0x8000,target=origin+0x9000,worldHandle=origin+0xa000,world=origin+0xa100,system=origin+0xa200,entity=origin+0xa500,wrapper=origin+0x10000;
    byte[] image=GameSession.Image(manifest,origin,origin);
    foreach(var hook in manifest.Hooks)foreach(var fix in hook.Relocations){image[fix.Offset-1]=0xc3;for(int n=0;n<4;n++)image[fix.Offset+n]=0x90;}
    Marshal.Copy(image,0,memory,image.Length);
    Func<string,int,ulong> invoke=(name,slot)=>{
     var bytes=new List<byte>();Action<string> add=s=>bytes.AddRange(Resources.Hex(s.Replace(" ","")));Action<long> imm=v=>bytes.AddRange(BitConverter.GetBytes(v));
     add("5355565741544155415641574881ec00030000");
     // Preserve all ten nonvolatile SIMD registers around the synthetic game calls.
     for(int reg=6;reg<16;reg++){add(reg>=8?"F3 44 0F 7F":"F3 0F 7F");bytes.Add((byte)(0x84|((reg%8)<<3)));bytes.Add(0x24);bytes.AddRange(BitConverter.GetBytes(0x100+(reg-6)*16));}
     add("48bf");imm(item);add("49be");imm(item);add("49b8");imm(item);add("48bd");imm(target);add("48bb");imm(slot);add("49bf");imm(worldHandle);
     add("48b8");imm(entity);add("48b9");imm(entity);
     add("48b8");imm(origin+manifest.Hooks.Single(h=>h.Name==name).CodeOffset);add("ffd0");
     for(int reg=6;reg<16;reg++){add(reg>=8?"F3 44 0F 6F":"F3 0F 6F");bytes.Add((byte)(0x84|((reg%8)<<3)));bytes.Add(0x24);bytes.AddRange(BitConverter.GetBytes(0x100+(reg-6)*16));}
     add("4881c400030000415f415e415d415c5f5e5d5bc3");Marshal.Copy(bytes.ToArray(),0,new IntPtr(wrapper),bytes.Count);
     Native.FlushInstructionCache(new IntPtr(-1),memory,(UIntPtr)0x20000);
     return ((Call)Marshal.GetDelegateForFunctionPointer(new IntPtr(wrapper),typeof(Call)))();
    };
    Marshal.WriteByte(new IntPtr(item+0x438),155);Put(data+8,-1);Put(data+12,1);
    invoke("BuildingPlace",0);check(Get(data+0x24)==155,"Disabled block mapping preserves original material");
    Put(data,1);invoke("BuildingPlace",0);check(Get(data+0x24)==129,"Block replacement uses correct encoded block ID");
    invoke("BuildingLookup",0);check(Get(data+0x30)==129,"Build cost lookup uses the same material mapping");
    invoke("BuildingPreview",0);check(Get(data+0x30)==129,"Building preview uses the same material mapping");
    Marshal.WriteByte(new IntPtr(item+0x438),21);invoke("BuildingPlace",0);check(Get(data+0x24)==129,"Single block target applies to a terrain source");
    Put(data+8,7);Put(data+12,-1);invoke("BuildingPlace",0);check(Get(data+0x24)==7,"Single terrain target applies to a block or terrain source");
    Put(data+12,2);invoke("BuildingPlace",0);check(Get(data+0x24)==7,"Two targets preserve source terrain type");
    Marshal.WriteByte(new IntPtr(item+0x438),155);invoke("BuildingPlace",0);check(Get(data+0x24)==130,"Two targets preserve source block type");
    Put(data+8,-1);Put(data+12,128);invoke("BuildingPlace",0);check(Get(data+0x24)==155,"Out-of-range block target is refused");
    Put(data+12,-1);Put(data+8,0);invoke("BuildingPlace",0);check(Get(data+0x24)==155,"Terrain zero sentinel is refused");
    var source=Enumerable.Range(1,16).Select(i=>(byte)i).ToArray();var replacement=Enumerable.Range(33,16).Select(i=>(byte)i).ToArray();
    Marshal.Copy(source,0,new IntPtr(item+0x3a8),16);Marshal.Copy(source,0,new IntPtr(item+0x354),16);Marshal.Copy(replacement,0,new IntPtr(data+0x480),16);Marshal.Copy(replacement,0,new IntPtr(data+0x490),16);
    Func<int,byte[]> bytesAt=offset=>{var b=new byte[16];Marshal.Copy(new IntPtr(data+offset),b,0,16);return b;};
    invoke("BuildingPropPlace",0);check(bytesAt(0x450).SequenceEqual(source),"Disabled object replacement preserves original UUID");
    Put(data+4,1);invoke("BuildingPropPlace",0);check(bytesAt(0x450).SequenceEqual(replacement),"Object placement loads replacement UUID");
    invoke("BuildingPropPreview",0);check(bytesAt(0x460).SequenceEqual(replacement),"Object preview loads replacement mesh UUID");
    Marshal.Copy(source,0,new IntPtr(data+0x4a0),16);Marshal.Copy(source,0,new IntPtr(data+0x4b0),16);Put(data+0x4c0,1);
    invoke("BuildingPropPlace",0);check(bytesAt(0x450).SequenceEqual(replacement),"Fixed source matches complete placement UUID");
    Marshal.WriteByte(new IntPtr(item+0x3af),99);invoke("BuildingPropPlace",0);check(bytesAt(0x450)[7]==99,"Fixed source rejects a mismatch in the first UUID half");
    Marshal.Copy(source,0,new IntPtr(item+0x3a8),16);Marshal.WriteByte(new IntPtr(item+0x3b0),99);invoke("BuildingPropPlace",0);check(bytesAt(0x450)[8]==99,"Fixed source rejects a mismatch in the second UUID half");
    invoke("BuildingPropPreview",0);check(bytesAt(0x460).SequenceEqual(replacement),"Fixed source matches complete preview mesh UUID");
    Marshal.WriteByte(new IntPtr(item+0x35c),99);invoke("BuildingPropPreview",0);check(bytesAt(0x460)[8]==99,"Fixed source rejects different preview mesh");
    Put(data+0x4c0,0);Marshal.Copy(new byte[16],0,new IntPtr(data+0x480),16);invoke("BuildingPropPlace",0);check(bytesAt(0x450)[8]==99,"Empty object replacement leaves original UUID intact");
    check(Marshal.ReadByte(new IntPtr(item+0x3b0))==99,"Source definition memory was never rewritten");
    Marshal.WriteInt64(new IntPtr(worldHandle),world);Marshal.WriteInt64(new IntPtr(world+0x30),system);Marshal.WriteInt64(new IntPtr(system+0x188),entity);
    Marshal.WriteInt64(new IntPtr(world+0x7e0),origin+0xa900);Put(origin+0xa900,12345);Put(worldHandle+8,1);Put(data+0x160,12345);
    Marshal.WriteInt64(new IntPtr(entity),entity);invoke("BuildingInspector",0x88*10);
    check(Get(data+0x114)==1,"Aimed object uses corrected slot10 stride0x88");
    check(Marshal.ReadInt64(new IntPtr(data+0x200+10*0x20))==target,"Inspector keeps entity per attachment slot");
    // RBP is the entity register in this path. Our fixture maps it to target.
    check(Marshal.ReadInt64(new IntPtr(data+0x108))==target,"Inspector retains original RBP entity, not RAX handle contents");
    check(Marshal.ReadInt64(new IntPtr(data+0x140))==item&&Get(data+0x148)==12345,"Picking publishes target owner paired with the local cursor actor");
    int captures=Get(data+0x114);Put(origin+0xa900,9876);invoke("BuildingInspector",0x88*10);
    check(Get(data+0x114)==captures&&Get(data+0x148)==12345,"Other actor's picking target cannot replace local owner or target");
    Put(worldHandle+8,0);invoke("BuildingInspector",0x88*10);check(Get(data+0x114)==captures,"Missing query actor is rejected before record-table access");
    return "Building native validation\r\n"+string.Join("\r\n",checks)+"\r\nOnly synthetic memory in this test process was accessed.\r\n";
   }finally{Native.VirtualFree(memory,UIntPtr.Zero,0x8000);}
  }
 }
}
