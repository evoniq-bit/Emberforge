using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace Emberforge {
 internal static class CrosshairMaterialNativeTests {
  [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate ulong Call();
  internal static string Run(){
   var lines=new List<string>();Action<bool,string> ck=(ok,n)=>{if(!ok)throw new Exception("CROSSHAIR MATERIAL TEST FAILED: "+n);lines.Add("PASS "+n);};
   var h=Resources.Manifest().Hooks.Single(x=>x.Name=="BuildingTarget");foreach(var r in h.Relocations)r.Rva=0x12100;
   IntPtr memory=Native.VirtualAlloc(IntPtr.Zero,(UIntPtr)0x20000,0x3000,0x40);if(memory==IntPtr.Zero)throw new Exception("Test allocation failed.");
   try{
    long origin=memory.ToInt64(),record=origin+0x10000,source=origin+0x16000;
    byte[] img=GameSession.Image(new HookManifest{AllocationSize=0x6000,Hooks=new[]{h}},origin,origin);Marshal.Copy(img,0,memory,img.Length);
    // Record R9, original RAX and RFLAGS, restore wrapper ABI.
    byte[] cont=Resources.Hex("4C890A488942089C588942104883C4285D31C0C3");Marshal.Copy(cont,0,new IntPtr(origin+0x12100),cont.Length);
    var b=new List<byte>();Action<string> add=s=>b.AddRange(Resources.Hex(s));Action<long> imm=v=>b.AddRange(BitConverter.GetBytes(v));
    add("554883EC2848BD");imm(source);add("48B8");imm(0x1122334455667788L);add("48BA");imm(record);add("4C8D15");int at=b.Count;b.AddRange(new byte[4]);add("4839C041FFE2");byte[] wrap=b.ToArray();Array.Copy(BitConverter.GetBytes(h.CodeOffset-(0x11000+at+4)),0,wrap,at,4);Marshal.Copy(wrap,0,new IntPtr(origin+0x11000),wrap.Length);Native.FlushInstructionCache(new IntPtr(-1),memory,(UIntPtr)0x20000);var call=(Call)Marshal.GetDelegateForFunctionPointer(new IntPtr(origin+0x11000),typeof(Call));
    Func<int,int> get=o=>Marshal.ReadInt32(new IntPtr(origin+o));Action<int,int> set=(o,v)=>Marshal.WriteInt32(new IntPtr(origin+o),v);
    Action<int,int,int,int,int> sample=(status,present,material,pickaxe,owner)=>{Marshal.WriteByte(new IntPtr(source+0x928),(byte)status);Marshal.WriteByte(new IntPtr(source+0x985),(byte)present);Marshal.WriteByte(new IntPtr(source+0x984),(byte)material);Marshal.WriteByte(new IntPtr(source+0x986),7);Marshal.WriteInt32(new IntPtr(source+0x108),owner);set(0x5164,pickaxe);set(0x5160,12345);call();};
    sample(0,1,21,1,12345);ck(get(0x5080)==21&&get(0x5088)==-1&&get(0x5098)==21,"Current camera-ray terrain material21 captured");
    sample(0,1,129,1,12345);ck(get(0x5080)==-1&&get(0x5088)==1&&get(0x5098)==129,"Current camera-ray block material129 maps toblock1");
    sample(0x1f,0,21,1,12345);ck(get(0x5080)==-1&&get(0x5088)==-1&&get(0x5098)==-1,"Miss clears stale material rather than recalling previousblock");
    ck(get(0x5094)==0x1f&&get(0x50a0)==0,"Miss publishes current query diagnostics");
    sample(0,0,21,1,12345);ck(get(0x5098)==-1,"Hit with nooptionalvoxelmaterial is rejected");
    sample(0,1,0,1,12345);ck(get(0x5098)==-1,"Air material0 is rejected");
    sample(0,1,21,0,12345);ck(get(0x5098)==-1,"Weapon/rake held state rejects ray material");
    sample(0,1,21,1,54321);ck(get(0x5098)==-1,"Different cursor owner rejects ray material");
    sample(0,1,255,1,12345);ck(get(0x5088)==127&&get(0x5098)==255,"Maximum material255 is mapped without overflow");
    ck(get(0x5090)==8&&get(0x509c)==3,"Fresh querycounter advances on every ray but validcapturecounter only validpickaxe materials");
    ck(Marshal.ReadInt64(new IntPtr(record))==source+0x928,"Original R9 target argument points to camera-ray result");
    ck(Marshal.ReadInt64(new IntPtr(record+8))==0x1122334455667788L,"Capture preserves incoming RAX");
    ck((Marshal.ReadInt32(new IntPtr(record+0x10))&0x41)==0x40,"Capture preserves incoming conditionflags");
    return string.Join("\r\n",lines)+"\r\nOwned synthetic memory only; no livegame access.\r\n";
   }finally{Native.VirtualFree(memory,UIntPtr.Zero,0x8000);}
  }
 }
}
