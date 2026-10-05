using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace Emberforge {
 internal static class CraftingNativeTests {
  [UnmanagedFunctionPointer(CallingConvention.Winapi)] delegate ulong Call();
  internal static string Run(){
   var lines=new List<string>();Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception("CRAFT TEST FAILED: "+name);lines.Add("PASS "+name);};
   HookSpec actual=Resources.Manifest().Hooks.Single(h=>h.Name=="CraftingNoConsumption");
   var hook=Resources.Json.Deserialize<HookSpec>(Resources.Json.Serialize(actual));
   foreach(var fix in hook.Relocations)fix.Rva=fix.Rva==0x36d370?0x12000:0x12100;
   var manifest=new HookManifest{AllocationSize=0x6000,DataOffset=0x4000,Hooks=new[]{hook}};
   IntPtr memory=Native.VirtualAlloc(IntPtr.Zero,(UIntPtr)0x20000,0x3000,0x40);if(memory==IntPtr.Zero)throw new Exception("Craft test allocation failed.");
   try{
    long origin=memory.ToInt64(),record=origin+0x10000;byte[] image=GameSession.Image(manifest,origin,origin);Marshal.Copy(image,0,memory,image.Length);
    // Synthetic ingredient staging: count calls, subtract requested amount, fail on shortage.
    byte[] payment=Resources.Hex("FF4104443909720644290933C0C3B804000000F9C3");Marshal.Copy(payment,0,new IntPtr(origin+0x12000),payment.Length);
    // Synthetic recipe continuation: keep success result, make output only on success,
    // capture flags and registers, then restore the wrapper's caller stack.
    byte[] continuation=Resources.Hex("8941309C58894128488951184C8959204C8941084C894910837930007503FF41348B41304883C428C3");Marshal.Copy(continuation,0,new IntPtr(origin+0x12100),continuation.Length);
    var b=new List<byte>();Action<string> add=s=>b.AddRange(Resources.Hex(s));Action<long> imm=v=>b.AddRange(BitConverter.GetBytes(v));
    add("4883EC2848B9");imm(record);add("48BA");imm(0x1122334455667788L);add("49B8");imm(0x2233445566778899L);add("49BB");imm(0x33445566778899aaL);
    add("44 8B 89 38 00 00 00".Replace(" ",""));add("488D05");int displacementAt=b.Count;b.AddRange(new byte[4]);add("4531D2FFE0");
    byte[] wrapper=b.ToArray();Array.Copy(BitConverter.GetBytes(hook.CodeOffset-(0x11000+displacementAt+4)),0,wrapper,displacementAt,4);Marshal.Copy(wrapper,0,new IntPtr(origin+0x11000),wrapper.Length);
    Func<int,int,int,int,ulong> invoke=(enabled,simulation,amount,requested)=>{
     Marshal.Copy(new byte[0xc0],0,new IntPtr(record),0xc0);Marshal.WriteInt32(new IntPtr(record),amount);Marshal.WriteInt32(new IntPtr(record+0x38),requested);Marshal.WriteByte(new IntPtr(record+0xb0),(byte)simulation);Marshal.WriteInt32(new IntPtr(origin+0x5604),enabled);
     Native.FlushInstructionCache(new IntPtr(-1),memory,(UIntPtr)0x20000);
     return ((Call)Marshal.GetDelegateForFunctionPointer(new IntPtr(origin+0x11000),typeof(Call)))();
    };
    Func<int,int> get=offset=>Marshal.ReadInt32(new IntPtr(record+offset));
    check(invoke(0,0,37,3)==0&&get(0)==34&&get(4)==1&&get(0x34)==1,"Disabled crafting deducts ingredients and preserves successful output");
    check(invoke(0,0,2,3)==4&&get(0)==2&&get(4)==1&&get(0x34)==0,"Disabled ingredient shortage rejects recipe output");
    check(invoke(1,0,37,3)==0&&get(0)==37&&get(4)==0&&get(0x34)==1,"Enabled crafting skips only ingredient stage and preserves output");
    check(Marshal.ReadInt64(new IntPtr(record+8))==0x2233445566778899L&&Marshal.ReadInt64(new IntPtr(record+0x10))==3,"Enabled path preserves argument registers R8 and R9");
    check(Marshal.ReadInt64(new IntPtr(record+0x18))==0x1122334455667788L&&Marshal.ReadInt64(new IntPtr(record+0x20))==0x33445566778899aaL,"Enabled path preserves RDX and R11");
    check((get(0x28)&0x41)==0x40,"Enabled path restores incoming ZF and CF");
    check(invoke(1,0x20,37,3)==0&&get(0)==34&&get(4)==1,"Simulation flag remains on original ingredient validation path");
    check(invoke(1,0x20,2,3)==4&&get(0x34)==0,"Simulation retains ingredient shortage rejection");
    check(invoke(1,0,50000,128)==0&&get(0)==50000&&get(0x34)==1,"Batch recipe count passes through unchanged while ingredients remain");
    check(invoke(0,0,50000,128)==0&&get(0)==49872,"Disabling restores batch ingredient deduction");
    check(Marshal.ReadInt32(new IntPtr(origin+0x5610))==2,"Only eligible normal crafting transactions increment the counter");
    check(actual.Original=="e82af9ffff"&&actual.Rva==0x36da41,"Hook replaces precisely one verified five-byte ingredient call");
    return "Crafting native validation\r\n"+string.Join("\r\n",lines)+"\r\nSynthetic memory only; real NPC and manual crafting need in-game confirmation.\r\n";
   }finally{Native.VirtualFree(memory,UIntPtr.Zero,0x8000);}
  }
 }
}



