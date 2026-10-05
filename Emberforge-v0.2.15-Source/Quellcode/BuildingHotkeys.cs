using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;

namespace Emberforge {
 public sealed class BuildingShortcut {
  public int Key{get;set;} public int Modifiers{get;set;}
 }
 public sealed class BuildingShortcutPreferences {
  public BuildingShortcut[] Shortcuts{get;set;}
  internal static BuildingShortcutPreferences Empty(){return new BuildingShortcutPreferences{Shortcuts=Enumerable.Range(0,4).Select(i=>new BuildingShortcut()).ToArray()};}
  internal static BuildingShortcutPreferences Normalize(BuildingShortcutPreferences p){
   if(p==null||p.Shortcuts==null||p.Shortcuts.Length!=4||p.Shortcuts.Any(k=>k==null||!BuildingHotkeys.ValidKey(k.Key)||k.Modifiers<0||k.Modifiers>7))return Empty();
   if(p.Shortcuts.Where(k=>k.Key!=0).GroupBy(k=>k.Key+":"+k.Modifiers).Any(g=>g.Count()>1))return Empty();return p;
  }
  internal static BuildingShortcutPreferences Load(){try{return Normalize(Resources.Json.Deserialize<BuildingShortcutPreferences>(File.ReadAllText(Files.PathOf("Bau-Hotkeys.json"))));}catch{return Empty();}}
  internal void Save(){File.WriteAllText(Files.PathOf("Bau-Hotkeys.json"),Resources.Json.Serialize(this));}
 }
 internal sealed class ShortcutChoice {public int Value{get;set;}public string Title{get;set;}public override string ToString(){return Title;}}
 internal sealed class BuildingHotkeys : IDisposable {
  // IDs are separate from the seven character shortcuts (100..106).
  internal const int FirstId=200;
  readonly Window window;readonly BuildingView building;readonly Action<string> status;readonly bool preview;
  readonly ComboBox[] keys=new ComboBox[4],modifiers=new ComboBox[4];
  readonly Func<IntPtr,int,uint,uint,bool> register;readonly Func<IntPtr,int,bool> unregister;
  readonly Func<int[]> characterKeys;readonly Action<bool> captureChanged,globalChanged;readonly CheckBox[] globalBoxes=new CheckBox[2];Panel settingsHost,materialHost,objectHost;
  readonly Button[] captureButtons=new Button[4];readonly TextBlock[] notices=new TextBlock[2];int captureIndex=-1;
  BuildingShortcutPreferences prefs;IntPtr hwnd;bool global,validBindings=true,foreground=true,registered;
  internal BuildingHotkeys(Window owner,BuildingView view,Panel host,bool isPreview,Action<string> report,Func<int[]> currentCharacterKeys,Action<bool> onCaptureChanged,Func<bool> currentGlobal,Action<bool> onGlobalChanged){
   window=owner;building=view;status=report;preview=isPreview;prefs=preview?BuildingShortcutPreferences.Empty():BuildingShortcutPreferences.Load();
   register=Native.RegisterHotKey;unregister=Native.UnregisterHotKey;
   characterKeys=currentCharacterKeys;captureChanged=onCaptureChanged;global=currentGlobal();globalChanged=onGlobalChanged;
   settingsHost=host;materialHost=view.HotkeyHost(false);objectHost=view.HotkeyHost(true);BuildSettings(settingsHost);BuildPage(materialHost,false);BuildPage(objectHost,true);Labels();
   window.Deactivated+=(s,e)=>CancelCapture();
  }
  string T(string key){return (string)window.FindResource(key);}
  internal void SetLanguage(){CancelCapture();settingsHost.Children.Clear();materialHost.Children.Clear();objectHost.Children.Clear();BuildSettings(settingsHost);BuildPage(materialHost,false);BuildPage(objectHost,true);Labels();}
  internal static bool ValidKey(int key){return key==0||key>=0x41&&key<=0x5a||key>=0x30&&key<=0x39||key>=0x70&&key<=0x7b||new[]{0x2d,0x2e,0x24,0x23,0x21,0x22}.Contains(key);}
  internal static string Validate(BuildingShortcut[] bindings,int[] characterKeys){
   if(bindings==null||bindings.Length!=4||bindings.Any(k=>k==null||!ValidKey(k.Key)||k.Modifiers<0||k.Modifiers>7))return "buildingKeysInvalid";
   if(bindings.Where(k=>k.Key!=0).GroupBy(k=>k.Key+":"+k.Modifiers).Any(g=>g.Count()>1))return "buildingKeysDuplicate";
   if(bindings.Any(k=>k.Key!=0&&k.Modifiers==0&&characterKeys.Any(c=>k.Key==0x70+c)))return "buildingKeysCharacterConflict";
   return null;
  }
  internal static int Resolve(BuildingShortcut[] bindings,int key,int mods){if(key==0)return -1;for(int i=0;i<bindings.Length;i++)if(bindings[i].Key==key&&bindings[i].Modifiers==mods)return i;return -1;}
  internal static int ModifierValue(ModifierKeys keys){return ((keys&ModifierKeys.Alt)!=0?1:0)|((keys&ModifierKeys.Control)!=0?2:0)|((keys&ModifierKeys.Shift)!=0?4:0);}
  internal static bool RegisterSet(IntPtr handle,BuildingShortcut[] bindings,bool enabled,Func<IntPtr,int,uint,uint,bool> add,Func<IntPtr,int,bool> remove){
   for(int i=0;i<4;i++)remove(handle,FirstId+i);
   if(!enabled||handle==IntPtr.Zero)return true;
   for(int i=0;i<4;i++)if(bindings[i].Key!=0&&!add(handle,FirstId+i,(uint)bindings[i].Modifiers|0x4000,(uint)bindings[i].Key)){
    for(int n=0;n<4;n++)remove(handle,FirstId+n);return false;
   }return true;
  }
  void BuildSettings(Panel host){
   var panel=new StackPanel();
   panel.Children.Add(new TextBlock{Text=T("buildingKeysHeading"),FontSize=19,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,12)});
   panel.Children.Add(new TextBlock{Text=T("buildingKeysDescription"),TextWrapping=TextWrapping.Wrap,Foreground=(Brush)window.FindResource("Muted"),FontSize=12,Margin=new Thickness(0,0,0,16)});
   var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(210)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(160)});
   var choices=new List<ShortcutChoice>{new ShortcutChoice{Value=0,Title=T("buildingKeysUnassigned")}};
   for(int i=0x41;i<=0x5a;i++)choices.Add(new ShortcutChoice{Value=i,Title=((char)i).ToString()});
   for(int i=0x30;i<=0x39;i++)choices.Add(new ShortcutChoice{Value=i,Title=((char)i).ToString()});
   for(int i=0;i<12;i++)choices.Add(new ShortcutChoice{Value=0x70+i,Title="F"+(i+1)});
   string[] navigation={"Einfg","Entf","Pos1","Ende","Bild ↑","Bild ↓"};int[] navigationCodes={0x2d,0x2e,0x24,0x23,0x21,0x22};
   for(int i=0;i<navigation.Length;i++)choices.Add(new ShortcutChoice{Value=navigationCodes[i],Title=navigation[i]});
   string[] mods={T("buildingKeysNoModifier"),"Alt","Strg","Strg + Alt","Umschalt","Alt + Umschalt","Strg + Umschalt","Strg + Alt + Umschalt"};
   string[] labels={"buildingKeysMaterialCapture","buildingKeysMaterialOff","buildingKeysObjectCapture","buildingKeysObjectOff"};
   for(int i=0;i<4;i++){
    grid.RowDefinitions.Add(new RowDefinition());var label=new TextBlock{Text=T(labels[i]),TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,6,10,6)};Grid.SetRow(label,i);grid.Children.Add(label);
    modifiers[i]=new ComboBox{ItemsSource=Enumerable.Range(0,8).Select(n=>new ShortcutChoice{Value=n,Title=mods[n]}).ToArray(),DisplayMemberPath="Title",SelectedValuePath="Value",SelectedValue=prefs.Shortcuts[i].Modifiers,MinWidth=0};
    keys[i]=new ComboBox{ItemsSource=choices.ToArray(),DisplayMemberPath="Title",SelectedValuePath="Value",SelectedValue=prefs.Shortcuts[i].Key,MinWidth=0};
    AutomationProperties.SetName(modifiers[i],T(labels[i])+" – "+T("buildingKeysModifier"));AutomationProperties.SetName(keys[i],T(labels[i])+" – "+T("buildingKeysKey"));
    Grid.SetRow(modifiers[i],i);Grid.SetColumn(modifiers[i],1);grid.Children.Add(modifiers[i]);Grid.SetRow(keys[i],i);Grid.SetColumn(keys[i],2);grid.Children.Add(keys[i]);
   }
   panel.Children.Add(grid);panel.Children.Add(new TextBlock{Text=T("buildingKeysHint"),TextWrapping=TextWrapping.Wrap,Foreground=(Brush)window.FindResource("Muted"),FontSize=12,Margin=new Thickness(0,14,0,0)});host.Children.Add(panel);
  }
  internal bool TrySave(int[] characterKeys,bool useGlobal){
   CancelCapture();
   var candidate=new BuildingShortcutPreferences{Shortcuts=Enumerable.Range(0,4).Select(i=>new BuildingShortcut{Key=keys[i].SelectedValue==null?-1:(int)keys[i].SelectedValue,Modifiers=modifiers[i].SelectedValue==null?-1:(int)modifiers[i].SelectedValue}).ToArray()};
   string error=Validate(candidate.Shortcuts,characterKeys);if(error!=null){status(T(error));return false;}
   var previous=prefs;bool oldGlobal=global;
   if(!RegisterSet(hwnd,candidate.Shortcuts,useGlobal&&!preview,register,unregister)){RegisterSet(hwnd,previous.Shortcuts,oldGlobal&&!preview,register,unregister);status(T("buildingKeysSystemConflict"));return false;}
   try{if(!preview)candidate.Save();prefs=candidate;global=useGlobal;validBindings=true;registered=useGlobal&&!preview;Labels();UpdateForeground(foreground);return true;}
   catch(Exception ex){RegisterSet(hwnd,previous.Shortcuts,oldGlobal&&!preview,register,unregister);Files.Log(ex.ToString());status(T("buildingKeysSaveError"));return false;}
  }
  internal void Register(IntPtr handle,bool enabled,int[] characterKeys){hwnd=handle;global=enabled;Labels();string error=Validate(prefs.Shortcuts,characterKeys);validBindings=error==null;if(!validBindings){Unregister();status(T(error));return;}registered=false;UpdateForeground(foreground);}
  internal void UpdateForeground(bool relevant){foreground=relevant;bool wanted=global&&validBindings&&!preview&&foreground&&captureIndex<0&&!TextInputFocused()&&hwnd!=IntPtr.Zero;if(wanted==registered)return;registered=wanted;if(!RegisterSet(hwnd,prefs.Shortcuts,wanted,register,unregister))status(T("buildingKeysSystemConflict"));}
  internal void Unregister(){registered=false;if(hwnd!=IntPtr.Zero)for(int i=0;i<4;i++)unregister(hwnd,FirstId+i);}
  internal bool Handle(int id,bool allowed){int index=id-FirstId;if(index<0||index>=4)return false;if(allowed&&captureIndex<0&&global&&validBindings&&prefs.Shortcuts[index].Key!=0&&!TextInputFocused())Invoke(index);return true;}
  internal bool Local(KeyEventArgs e,bool allowed){if(!allowed||captureIndex>=0||!validBindings||e.IsRepeat||TextInputFocused()||(Keyboard.Modifiers&ModifierKeys.Windows)!=0)return false;int key=KeyInterop.VirtualKeyFromKey(e.Key==Key.System?e.SystemKey:e.Key);int index=Resolve(prefs.Shortcuts,key,ModifierValue(Keyboard.Modifiers));if(index<0)return false;Invoke(index);return true;}
  void BuildPage(Panel host,bool objects){
   host.Children.Add(new TextBlock{Text=T("buildingKeysRecordHint"),TextWrapping=TextWrapping.Wrap,Foreground=(Brush)window.FindResource("Muted"),Margin=new Thickness(0,0,0,12)});
   for(int row=0;row<2;row++){
    int index=(objects?2:0)+row;var grid=new Grid{Margin=new Thickness(0,4,0,4)};grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(190)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
    grid.Children.Add(new TextBlock{Text=T(row==0?"buildingKeysCaptureAction":"buildingKeysClearAction"),TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,12,0)});
    captureButtons[index]=new Button{MinHeight=38,Margin=new Thickness(0,0,8,0)};captureButtons[index].Click+=(s,e)=>BeginCapture(index);AutomationProperties.SetName(captureButtons[index],T(row==0?"buildingKeysCaptureAction":"buildingKeysClearAction")+" – "+T("buildingKeysKey"));Grid.SetColumn(captureButtons[index],1);grid.Children.Add(captureButtons[index]);
    var clear=new Button{Content="×",MinWidth=38,ToolTip=T("buildingKeysRemoveBinding")};clear.Click+=(s,e)=>{CancelCapture();ApplyBinding(index,0,0);};Grid.SetColumn(clear,2);grid.Children.Add(clear);host.Children.Add(grid);
   }
   globalBoxes[objects?1:0]=new CheckBox{Content=T("globalHotkeys"),IsChecked=global,Margin=new Thickness(0,10,0,0)};globalBoxes[objects?1:0].Click+=(s,e)=>{bool enabled=((CheckBox)s).IsChecked==true;CancelCapture();globalChanged(enabled);};host.Children.Add(globalBoxes[objects?1:0]);
   notices[objects?1:0]=new TextBlock{TextWrapping=TextWrapping.Wrap,Foreground=new SolidColorBrush(Color.FromRgb(232,183,120)),Margin=new Thickness(0,8,0,0)};host.Children.Add(notices[objects?1:0]);
  }
  internal void BeginCapture(int index){if(index<0||index>=4)return;captureIndex=index;Unregister();captureChanged(true);notices[index/2].Text=T("buildingKeysListeningHint");Labels();captureButtons[index].Focus();}
  internal void CancelCapture(){if(captureIndex<0)return;int index=captureIndex;captureIndex=-1;notices[index/2].Text="";Labels();captureChanged(false);}
  internal bool CaptureKey(KeyEventArgs e){return AcceptCapturedKey(e.Key==Key.System?e.SystemKey:e.Key,Keyboard.Modifiers,e.IsRepeat);}
  internal bool AcceptCapturedKey(Key key,ModifierKeys mods,bool repeated){
   if(captureIndex<0)return false;if(repeated)return true;
   if(key==Key.Escape){CancelCapture();return true;}
   if(key==Key.LeftCtrl||key==Key.RightCtrl||key==Key.LeftAlt||key==Key.RightAlt||key==Key.LeftShift||key==Key.RightShift)return true;
   int index=captureIndex;if((mods&ModifierKeys.Windows)!=0||!ValidKey(KeyInterop.VirtualKeyFromKey(key))){notices[index/2].Text=T("buildingKeysInvalid");return true;}
   if(ApplyBinding(index,KeyInterop.VirtualKeyFromKey(key),ModifierValue(mods)))CancelCapture();return true;
  }
  bool ApplyBinding(int index,int key,int mods){
   var candidate=Resources.Json.Deserialize<BuildingShortcutPreferences>(Resources.Json.Serialize(prefs));candidate.Shortcuts[index]=new BuildingShortcut{Key=key,Modifiers=mods};string error=Validate(candidate.Shortcuts,characterKeys());
   if(error!=null){notices[index/2].Text=T(error);status(T(error));return false;}
   bool probe=global&&!preview&&hwnd!=IntPtr.Zero;
   if(!RegisterSet(hwnd,candidate.Shortcuts,probe,register,unregister)){registered=false;notices[index/2].Text=T("buildingKeysSystemConflict");status(T("buildingKeysSystemConflict"));UpdateForeground(foreground);return false;}
   try{if(!preview)candidate.Save();prefs=candidate;validBindings=true;registered=probe;for(int i=0;i<4;i++){keys[i].SelectedValue=prefs.Shortcuts[i].Key;modifiers[i].SelectedValue=prefs.Shortcuts[i].Modifiers;}Labels();notices[index/2].Text=T("buildingKeysSaved");status(T("buildingKeysSaved"));UpdateForeground(foreground);return true;}
   catch(Exception ex){Files.Log(ex.ToString());registered=false;RegisterSet(hwnd,prefs.Shortcuts,false,register,unregister);UpdateForeground(foreground);notices[index/2].Text=T("buildingKeysSaveError");return false;}
  }
  internal static bool InputBlocks(bool active,IInputElement focused){return active&&(focused is TextBoxBase||focused is ComboBox||focused is ComboBoxItem);}
  bool TextInputFocused(){return InputBlocks(window.IsActive,Keyboard.FocusedElement);}
  void Invoke(int index){
   try{if(index==0)building.CaptureAndEnableMaterial();else if(index==1)building.DisableMaterialReplacement();else if(index==2)building.CaptureAndEnableObject();else building.DisableObjectReplacement();}
   catch(Exception ex){Files.Log("Bau-Hotkey: "+ex.Message);status(ex.Message);}
  }
  string Label(BuildingShortcut value){
   if(value.Key==0)return T("buildingKeysUnassigned");string key=value.Key>=0x70&&value.Key<=0x7b?"F"+(value.Key-0x6f):value.Key>=0x30&&value.Key<=0x5a?((char)value.Key).ToString():value.Key==0x2d?"Einfg":value.Key==0x2e?"Entf":value.Key==0x24?"Pos1":value.Key==0x23?"Ende":value.Key==0x21?"Bild ↑":"Bild ↓";
   return ((value.Modifiers&2)!=0?"Strg + ":"")+((value.Modifiers&1)!=0?"Alt + ":"")+((value.Modifiers&4)!=0?"Umschalt + ":"")+key;
  }
  void Labels(){building.ShowHotkeyLabels(Label(prefs.Shortcuts[0]),Label(prefs.Shortcuts[1]),Label(prefs.Shortcuts[2]),Label(prefs.Shortcuts[3]));for(int i=0;i<4;i++)if(captureButtons[i]!=null)captureButtons[i].Content=i==captureIndex?T("buildingKeysListening"):Label(prefs.Shortcuts[i]);foreach(var box in globalBoxes)if(box!=null)box.IsChecked=global;}
  public void Dispose(){Unregister();}
 }
}
