// KFC3 layout informed by ember-kfc (MIT), Zach Landquist, 2026.
// Independently implemented bounded reader. See Katalog-Quellen.txt and its license.
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Web.Script.Serialization;

namespace Emberforge {
 public sealed class GameCatalogData {
  public int Format{get;set;}public string Path{get;set;}public string Fingerprint{get;set;}public string Build{get;set;}public int Resources{get;set;}public BuildingCatalogItem[] Items{get;set;}
 }
 internal static class GameCatalog {
  const uint TemplateType=0x39768775,ModelComponent=422260440;
  internal static readonly JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=16000000};
  struct Table {internal int Offset,Count;}
  struct Entry {internal string Id;internal int Offset,Size;}
  struct Chunk {internal int Offset,Slot,Frame,Stream,Size;}
  static void Need(bool value){if(!value)throw new InvalidDataException("Das Ressourcenformat passt nicht zu diesem Leser. Der bisherige Katalog bleibt erhalten.");}
  static uint U(byte[] b,int at){Need(at>=0&&(long)at+4<=b.Length);return BitConverter.ToUInt32(b,at);}
  static string IdAt(byte[] b,int at){Span(b,at,16,1);var value=new byte[16];Buffer.BlockCopy(b,at,value,0,16);return Resources.Hex(value);}
  static int Number(uint n){Need(n<=int.MaxValue);return (int)n;}
  static void Span(byte[] b,int at,int count,int stride){Need(at>=0&&count>=0&&stride>=0&&(long)at+(long)count*stride<=b.Length);}
  static Table Location(byte[] b,int slot,int stride){int at=16+slot*8;int rel=Number(U(b,at)),count=Number(U(b,at+4));if(rel==0)return new Table();Need((long)at+rel<=int.MaxValue);at+=rel;Span(b,at,count,stride);return new Table{Offset=at,Count=count};}
  internal static string Fingerprint(string path){using(var hash=SHA256.Create())using(var stream=File.OpenRead(path))return Resources.Hex(hash.ComputeHash(stream));}
  static bool HexId(string id){return id!=null&&id.Length==32&&id.All(Uri.IsHexDigit)&&id.Any(c=>c!='0');}
  internal static bool Valid(GameCatalogData c){return c!=null&&c.Format==1&&c.Items!=null&&c.Items.Length>0&&c.Items.Length<=100000&&c.Items.All(i=>i!=null&&!string.IsNullOrWhiteSpace(i.Name)&&i.Name.Length<1024&&!i.Name.Any(char.IsControl)&&HexId(i.Id)&&i.Mesh!=null&&i.Mesh.Length==32&&i.Mesh.All(Uri.IsHexDigit))&&c.Items.Select(i=>i.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count()==c.Items.Length;}
  internal static GameCatalogData LoadCache(){try{var c=Json.Deserialize<GameCatalogData>(File.ReadAllText(Files.PathOf("Spielkatalog.json")));if(!Valid(c)||!File.Exists(c.Path)||Fingerprint(c.Path)!=c.Fingerprint)return null;return c;}catch{return null;}}
  internal static void SaveCache(GameCatalogData c){Need(Valid(c));string path=Files.PathOf("Spielkatalog.json"),temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllText(temporary,Json.Serialize(c),Encoding.UTF8);if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);}finally{if(File.Exists(temporary))File.Delete(temporary);}}
  internal static string GuessPath(){
   try{foreach(var p in System.Diagnostics.Process.GetProcessesByName("enshrouded"))using(p){string file=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(p.MainModule.FileName),"enshrouded.kfc");if(File.Exists(file))return file;}}catch{}
   string folder=Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);return System.IO.Path.Combine(folder,"Steam","steamapps","common","Enshrouded","enshrouded.kfc");
  }
  internal static GameCatalogData Read(string path,Action<string> progress){
   path=System.IO.Path.GetFullPath(path);Need(new FileInfo(path).Length<=64000000);byte[] b=File.ReadAllBytes(path);Need(U(b,0)==0x3343464b);
   var version=Location(b,0,1);string build=Encoding.UTF8.GetString(b,version.Offset,version.Count);
   var streams=Location(b,4,12);Need(streams.Count==1);int streamSize=Number(U(b,streams.Offset));long compressed=U(b,streams.Offset+4);
   var keys=Location(b,10,32);var values=Location(b,11,8);Need(keys.Count==values.Count&&keys.Count==U(b,streams.Offset+8)&&keys.Count<=1000000);
   var entries=new List<Entry>();var models=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   for(int i=0;i<keys.Count;i++){
    int k=keys.Offset+i*32,v=values.Offset+i*8;string id=IdAt(b,k);models.Add(id);
    int off=Number(U(b,v)),size=Number(U(b,v+4));Need((long)off+size<=streamSize);
    if(U(b,k+16)==TemplateType){Need(U(b,k+20)==0&&size>=20&&size<=4000000);entries.Add(new Entry{Id=id,Offset=off,Size=size});}
   }
   Need(entries.Count>0&&entries.Count<=100000&&entries.Select(e=>e.Id).Distinct().Count()==entries.Count);
   var ct=Location(b,15,20);Need(ct.Count>0&&ct.Count<=10000);var chunks=new List<Chunk>();long endSlot=0,endStream=0,frames=0;
   for(int i=0;i<ct.Count;i++){
    int p=ct.Offset+i*20;var c=new Chunk{Offset=Number(U(b,p)),Slot=Number(U(b,p+4)),Frame=Number(U(b,p+8)),Stream=Number(U(b,p+12)),Size=Number(U(b,p+16))};
    Need(c.Offset==endSlot&&c.Stream==endStream&&c.Frame>0&&c.Frame<=c.Slot&&c.Frame<=64000000&&c.Size>0&&c.Size<=64000000);
    endSlot+=(long)c.Slot;endStream+=(long)c.Size;frames+=(long)c.Frame;chunks.Add(c);
   }
   Need(endStream==streamSize&&frames==compressed);string payload=System.IO.Path.ChangeExtension(path,"kfc_resources");Need(new FileInfo(payload).Length==endSlot);
   var result=new List<BuildingCatalogItem>();int cachedIndex=-1;byte[] cached=null;
   using(var file=new FileStream(payload,FileMode.Open,FileAccess.Read,FileShare.Read)){
    int number=0;
    foreach(var e in entries.OrderBy(e=>e.Offset)){
     byte[] data=new byte[e.Size];int written=0;
     for(int ci=0;ci<chunks.Count;ci++){
      var c=chunks[ci];if((long)c.Stream+c.Size<=e.Offset||c.Stream>=(long)e.Offset+e.Size)continue;
      if(cachedIndex!=ci){byte[] packed=new byte[c.Frame];file.Position=c.Offset;int count=0;while(count<packed.Length){int n=file.Read(packed,count,packed.Length-count);Need(n>0);count+=n;}cached=Zstd.Decode(packed,c.Size);cachedIndex=ci;}
      int start=Math.Max(e.Offset,c.Stream),stop=(int)Math.Min((long)e.Offset+e.Size,(long)c.Stream+c.Size),length=stop-start;
      Buffer.BlockCopy(cached,start-c.Stream,data,start-e.Offset,length);written+=length;
     }
     Need(written==e.Size);try{result.Add(ParseTemplate(e.Id,data,models));}catch(Exception ex){throw new InvalidDataException("Objektdefinition "+e.Id+" konnte nicht vollständig gelesen werden.",ex);}
     if(++number%200==0&&progress!=null)progress("Lese Objektdefinitionen … "+number+" / "+entries.Count);
    }
   }
   // A concurrent Steam update must never leave a mixed catalog behind.
   string fingerprint;using(var hash=SHA256.Create())fingerprint=Resources.Hex(hash.ComputeHash(b));Need(Fingerprint(path)==fingerprint);
   var output=new GameCatalogData{Format=1,Path=path,Fingerprint=fingerprint,Build=build,Resources=keys.Count,Items=result.OrderBy(i=>i.Name,StringComparer.OrdinalIgnoreCase).ToArray()};Need(Valid(output));return output;
  }
  internal static BuildingCatalogItem ParseTemplate(string id,byte[] data,HashSet<string> resources){
   // Word 8 contains template flags, not a schema version; flags differ among templates.
   Need(data.Length>=20);int start=Number(U(data,0)),length=Number(U(data,4));Need(length>0&&length<1024);Span(data,start,length,1);
   string name=new UTF8Encoding(false,true).GetString(data,start,length).TrimEnd('\0');Need(name.Length>0&&!name.Any(char.IsControl));
   var table=LocationTemplate(data);string mesh=new string('0',32);
   for(int i=0;i<table.Count;i++){
    int p=table.Offset+i*12;uint type=U(data,p);int rel=Number(U(data,p+4)),size=Number(U(data,p+8));Need((long)p+4+rel<=int.MaxValue);int at=p+4+rel;Span(data,at,size,1);
    if(type==ModelComponent){Need(size==16);mesh=IdAt(data,at);Need(mesh==new string('0',32)||resources.Contains(mesh));}
   }
   return new BuildingCatalogItem{Name=name,Id=id,Mesh=mesh};
  }
  static Table LocationTemplate(byte[] data){int rel=Number(U(data,12)),count=Number(U(data,16));Need((long)12+rel<=int.MaxValue);int at=12+rel;Span(data,at,count,12);return new Table{Offset=at,Count=count};}
 }
 internal static class Zstd {
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr LoadLibraryEx(string path,IntPtr file,uint flags);
  [DllImport("kernel32.dll",CharSet=CharSet.Ansi)] static extern IntPtr GetProcAddress(IntPtr module,string name);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate UIntPtr Decompress(byte[] target,UIntPtr capacity,byte[] source,UIntPtr size);
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate uint IsError(UIntPtr code);
  static Decompress decompress;static IsError error;static IntPtr module;
  internal static byte[] Decode(byte[] packed,int size){
   if(decompress==null){
    string path=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"libzstd.dll");if(GameCatalog.Fingerprint(path)!="8f07e1112ed283e5cd2798833e9a3c32d8961381bc36da04af57a1b0ca9bd40b")throw new InvalidDataException("Die Katalog-Lesebibliothek fehlt oder wurde verändert. Den vollständigen Emberforge-Ordner verwenden.");
    module=LoadLibraryEx(path,IntPtr.Zero,0x100|0x800);if(module==IntPtr.Zero)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),"Katalog-Lesebibliothek konnte nicht geladen werden.");
    decompress=(Decompress)Marshal.GetDelegateForFunctionPointer(GetProcAddress(module,"ZSTD_decompress"),typeof(Decompress));error=(IsError)Marshal.GetDelegateForFunctionPointer(GetProcAddress(module,"ZSTD_isError"),typeof(IsError));
   }
   byte[] data=new byte[size];var length=decompress(data,(UIntPtr)size,packed,(UIntPtr)packed.Length);if(error(length)!=0||length.ToUInt64()!=(ulong)size)throw new InvalidDataException("Komprimierte Spielressource ist beschädigt oder nicht unterstützt.");return data;
  }
 }
}
