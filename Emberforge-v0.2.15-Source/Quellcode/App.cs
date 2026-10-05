using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml;
using System.ComponentModel;

namespace Emberforge {
 public sealed class FutureCategory {public string Title{get;set;}public string Detail{get;set;}}
 public sealed class Preferences {
  public int[] Keys{get;set;} public bool[] Favorites{get;set;} public bool Global{get;set;} public bool Topmost{get;set;} public string Language{get;set;}
  internal static Preferences Default(){return new Preferences{Keys=new[]{0,1,2,4,5,6,3},Favorites=new bool[10],Global=true,Language="de"};}
  internal static Preferences Load(){try{return Normalize(Resources.Json.Deserialize<Preferences>(File.ReadAllText(Files.PathOf("Einstellungen.json"))));}catch{return Default();}}
  internal static Preferences Normalize(Preferences p){if(p==null||p.Keys==null||p.Favorites==null)return Default();
   if(p.Keys.Length==4&&p.Favorites.Length==3){var old=p.Keys;var used=new HashSet<int>(old);var extra=Enumerable.Range(0,12).Where(k=>!used.Contains(k)).Take(3).ToArray();p.Keys=new[]{old[0],old[1],old[2],extra[0],extra[1],extra[2],old[3]};p.Favorites=p.Favorites.Concat(new bool[3]).ToArray();}
   if(p.Favorites.Length==6)p.Favorites=p.Favorites.Concat(new bool[2]).ToArray();
   if(p.Favorites.Length==8)p.Favorites=p.Favorites.Concat(new bool[1]).ToArray();
   if(p.Favorites.Length==9)p.Favorites=p.Favorites.Concat(new bool[1]).ToArray();
   if(p.Keys.Length!=7||p.Keys.Any(k=>k<0||k>11)||p.Keys.Distinct().Count()!=7||p.Favorites.Length!=10)return Default();p.Language=string.Equals(p.Language,"en",StringComparison.OrdinalIgnoreCase)?"en":"de";return p;}
  internal void Save(){File.WriteAllText(Files.PathOf("Einstellungen.json"),Resources.Json.Serialize(this));}
 }
 internal sealed class MainView {
  internal Window Window;Preferences prefs;GameSession session=new GameSession();DispatcherTimer timer;BuildingView building;BuildingHotkeys buildingKeys;ProgressionView progression;MovementView movement;
  ToggleButton[] toggles,stars;TextBlock[] states;Button[] keyButtons;Border[] cards;ComboBox[] combos;ComboBox languageChoice;Button[] nav;ResourceDictionary languageResources;bool changingLanguage;
  IntPtr hwnd,foregroundHook;WinEventCallback foregroundCallback;bool closing,changing,busy,preview;uint[] lastCaptures=new uint[5];long[] seen=new long[5];string page="character";
  internal T Find<T>(string name) where T:class {return Window.FindName(name) as T;}
  internal string T(string key){return (string)Window.FindResource(key);}
  internal string CurrentLanguage{get{return prefs.Language;}}
  internal BuildingHotkeys BuildingKeyController{get{return buildingKeys;}}
  internal Task TestAutoCatalog(){return building.AutoCatalog(true);}
  internal void PreviewCatalog(GameCatalogData data){if(!preview)throw new InvalidOperationException("Nur für Vorschauen.");building.ShowCatalog(data);}
  void Say(string message){Find<TextBlock>("Footer").Text=message;}
  internal MainView(bool previewOnly=false) {
   preview=previewOnly;prefs=preview?Preferences.Default():Preferences.Load();
   Window=(Window)XamlReader.Parse(Resources.Text("MainWindow.xaml"));
   ApplyLanguage(prefs.Language);Window.Topmost=prefs.Topmost;
   Find<ItemsControl>("FutureRoadmap").ItemsSource=Resources.Json.Deserialize<FutureCategory[]>(Resources.Text("FutureRoadmapDeutsch.json"));
   var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=new MemoryStream(Resources.Read("Emberforge-Logo.png"));bitmap.EndInit();bitmap.Freeze();Find<Image>("Hero").Source=bitmap;
   toggles=new[]{Find<ToggleButton>("HealthToggle"),Find<ToggleButton>("FallToggle"),Find<ToggleButton>("StaminaToggle"),Find<ToggleButton>("ManaToggle"),Find<ToggleButton>("ColdToggle"),Find<ToggleButton>("ShroudToggle")};
   stars=new[]{Find<ToggleButton>("HealthStar"),Find<ToggleButton>("FallStar"),Find<ToggleButton>("StaminaStar"),Find<ToggleButton>("ManaStar"),Find<ToggleButton>("ColdStar"),Find<ToggleButton>("ShroudStar")};
   states=new[]{Find<TextBlock>("HealthState"),Find<TextBlock>("FallState"),Find<TextBlock>("StaminaState"),Find<TextBlock>("ManaState"),Find<TextBlock>("ColdState"),Find<TextBlock>("ShroudState")};
   keyButtons=new[]{Find<Button>("HealthKey"),Find<Button>("FallKey"),Find<Button>("StaminaKey"),Find<Button>("ManaKey"),Find<Button>("ColdKey"),Find<Button>("ShroudKey")};
   cards=new[]{Find<Border>("HealthCard"),Find<Border>("FallCard"),Find<Border>("StaminaCard"),Find<Border>("ManaCard"),Find<Border>("ColdCard"),Find<Border>("ShroudCard")};
   combos=new[]{Find<ComboBox>("HealthCombo"),Find<ComboBox>("FallCombo"),Find<ComboBox>("StaminaCombo"),Find<ComboBox>("ManaCombo"),Find<ComboBox>("ColdCombo"),Find<ComboBox>("ShroudCombo"),Find<ComboBox>("AllOffCombo")};
   nav=new[]{Find<Button>("NavCharacter"),Find<Button>("NavBlocks"),Find<Button>("NavObjects"),Find<Button>("NavEquipment"),Find<Button>("NavCraft"),Find<Button>("NavFuture"),Find<Button>("NavSettings")};
   for(int i=0;i<7;i++){for(int k=1;k<=12;k++)combos[i].Items.Add("F"+k);combos[i].SelectedIndex=prefs.Keys[i];}
   Find<CheckBox>("GlobalHotkeys").IsChecked=prefs.Global;Find<CheckBox>("TopmostSetting").IsChecked=prefs.Topmost;
   languageChoice=Find<ComboBox>("LanguageChoice");languageChoice.SelectedValue=prefs.Language;
   languageChoice.SelectionChanged+=(s,e)=>{if(!changingLanguage&&languageChoice.SelectedValue!=null)ChangeLanguage((string)languageChoice.SelectedValue);};
   for(int i=0;i<6;i++){
    int index=i;stars[i].IsChecked=prefs.Favorites[i];keyButtons[i].Content="F"+(prefs.Keys[i]+1);
    stars[i].Click+=(s,e)=>{prefs.Favorites[index]=stars[index].IsChecked==true;SavePreferences();Filter();};
    keyButtons[i].Click+=(s,e)=>Page("settings");toggles[i].Click+=(s,e)=>Toggle(index);
   }
   nav[0].Click+=(s,e)=>Page("character");nav[1].Click+=(s,e)=>Page("blocks");nav[2].Click+=(s,e)=>Page("objects");nav[3].Click+=(s,e)=>Page("equipment");nav[4].Click+=(s,e)=>Page("craft");nav[5].Click+=(s,e)=>Page("future");nav[6].Click+=(s,e)=>Page("settings");
   Find<Button>("BackToCharacter").Click+=(s,e)=>Page("character");
   Find<Button>("Minimize").Click+=(s,e)=>Window.WindowState=WindowState.Minimized;
   Find<Button>("Maximize").Click+=(s,e)=>Window.WindowState=Window.WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;
   Find<Button>("Close").Click+=(s,e)=>Window.Close();
   Find<Grid>("TitleBar").MouseLeftButtonDown+=(s,e)=>{if(e.ClickCount==2)Window.WindowState=Window.WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;else Window.DragMove();};
   Find<Button>("Connect").Click+=async(s,e)=>await ConnectOrDisconnect();
   Find<Button>("AllOff").Click+=(s,e)=>Off();
   Find<ToggleButton>("FavoritesFilter").Click+=(s,e)=>Filter();
   Find<Button>("AllFilter").Click+=(s,e)=>{Find<ToggleButton>("FavoritesFilter").IsChecked=false;Filter();};
   Find<Button>("SaveSettings").Click+=(s,e)=>SaveSettings();Find<Button>("AdminStart").Click+=(s,e)=>Elevate();
   Window.SourceInitialized+=(s,e)=>{hwnd=new WindowInteropHelper(Window).Handle;HwndSource.FromHwnd(hwnd).AddHook(Hotkey);RegisterKeys();if(!preview){foregroundCallback=(h,eventType,w,o,c,t,time)=>{if(!closing)Window.Dispatcher.BeginInvoke(new System.Action(RefreshBuildingKeyScope));};foregroundHook=SetWinEventHook(3,3,IntPtr.Zero,foregroundCallback,0,0,0);RefreshBuildingKeyScope();}};
   Window.GotKeyboardFocus+=(s,e)=>RefreshBuildingKeyScope();Window.LostKeyboardFocus+=(s,e)=>Window.Dispatcher.BeginInvoke(new System.Action(RefreshBuildingKeyScope));
   Window.PreviewKeyDown+=(s,e)=>{if(buildingKeys!=null&&buildingKeys.CaptureKey(e)){e.Handled=true;return;}if(prefs.Global)return;if(buildingKeys!=null&&buildingKeys.Local(e,!busy)){e.Handled=true;return;}if(e.IsRepeat||Keyboard.Modifiers!=ModifierKeys.None)return;for(int i=0;i<7;i++)if(e.Key==(Key)((int)Key.F1+prefs.Keys[i])){Action(i);e.Handled=true;}};
   Window.Closing+=(s,e)=>{
    if(busy){e.Cancel=true;return;}
    try{session.Dispose();UnregisterKeys();closing=true;if(foregroundHook!=IntPtr.Zero){UnhookWinEvent(foregroundHook);foregroundHook=IntPtr.Zero;}if(timer!=null)timer.Stop();}
    catch(Exception ex){Files.Log(ex.ToString());Say(T("closeError"));e.Cancel=true;}
   };
   Window.Loaded+=async(s,e)=>{if(!preview){var area=SystemParameters.WorkArea;if(Window.Height>area.Height)Window.Height=Math.Max(Window.MinHeight,area.Height-24);timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(350)};timer.Tick+=(a,b)=>Poll();timer.Start();await building.AutoCatalog();}};
   Window.SizeChanged+=(s,e)=>Layout(e.NewSize.Width,e.NewSize.Height);
   building=new BuildingView(this,session,preview,ConnectOrDisconnect);Find<Grid>("MainContent").Children.Add(building.Root);building.Reset();
   movement=new MovementView(this,session,preview,building.Root);var flightStar=Find<ToggleButton>("FlightStar");flightStar.IsChecked=prefs.Favorites[9];flightStar.Click+=(s,e)=>{prefs.Favorites[9]=flightStar.IsChecked==true;SavePreferences();Filter();};var breathStar=Find<ToggleButton>("BreathStar");breathStar.IsChecked=prefs.Favorites[8];breathStar.Click+=(s,e)=>{prefs.Favorites[8]=breathStar.IsChecked==true;SavePreferences();Filter();};
   progression=new ProgressionView(this,session,building.Root,preview);
   foreach(string feature in new[]{"Xp","Skill"}){int index=feature=="Xp"?6:7;var star=Find<ToggleButton>(feature+"Star");star.IsChecked=prefs.Favorites[index];star.Click+=(s,e)=>{prefs.Favorites[index]=star.IsChecked==true;SavePreferences();Filter();};}
   buildingKeys=new BuildingHotkeys(Window,building,Find<StackPanel>("BuildingHotkeySettings"),preview,Say,()=>prefs.Keys,on=>{if(on)UnregisterKeys();else RegisterKeys();},()=>prefs.Global,on=>{prefs.Global=on;Find<CheckBox>("GlobalHotkeys").IsChecked=on;SavePreferences();RegisterKeys();});
   ApplyLanguage(prefs.Language);
   WheelScrolling.Attach(Window,Find<ScrollViewer>("PageScroll"));Page("character");Filter();
  }
  void ApplyLanguage(string language){
   string code=string.Equals(language,"en",StringComparison.OrdinalIgnoreCase)?"en":"de";prefs.Language=code;
   if(languageResources!=null)Window.Resources.MergedDictionaries.Remove(languageResources);
   languageResources=(ResourceDictionary)XamlReader.Parse(Resources.Text(code=="en"?"English.xaml":"Deutsch.xaml"));Window.Resources.MergedDictionaries.Add(languageResources);Window.Title=T("appTitle");
   if(Find<ComboBox>("LanguageChoice")!=null){changingLanguage=true;Find<ComboBox>("LanguageChoice").SelectedValue=code;changingLanguage=false;}
   if(Find<ItemsControl>("FutureRoadmap")!=null)Find<ItemsControl>("FutureRoadmap").ItemsSource=Resources.Json.Deserialize<FutureCategory[]>(Resources.Text(code=="en"?"FutureRoadmapEnglish.json":"FutureRoadmapDeutsch.json"));
   if(progression!=null)progression.LanguageChanged();if(movement!=null)movement.LanguageChanged();
   if(building!=null)building.SetLanguage(code);if(buildingKeys!=null)buildingKeys.SetLanguage();
   if(toggles!=null){StateLabels();Filter();}
  }
  void ChangeLanguage(string language){ApplyLanguage(language);SavePreferences();Say(T("settingsSaved"));}
  internal void Page(string target) {
   bool different=page!=target;if(buildingKeys!=null)buildingKeys.CancelCapture();page=target;
   Find<StackPanel>("CharacterPage").Visibility=target=="character"?Visibility.Visible:Visibility.Collapsed;
   Find<StackPanel>("SettingsPage").Visibility=target=="settings"?Visibility.Visible:Visibility.Collapsed;
   Find<StackPanel>("FuturePage").Visibility=target=="future"?Visibility.Visible:Visibility.Collapsed;
   Find<StackPanel>("PlannedPage").Visibility=Visibility.Collapsed;
   if(building!=null)building.Page(target);
   if(target=="blocks"||target=="objects"){Find<TextBlock>("PlannedCategory").Text=T(target);Find<TextBlock>("PlannedFeatures").Text=T(target=="blocks"?"blocksPlan":"objectsPlan");}
   var ids=new[]{"character","blocks","objects","equipment","craft","future","settings"};
   for(int i=0;i<nav.Length;i++){nav[i].Background=Brush(ids[i]==target?"#33281F":"Transparent");nav[i].BorderBrush=Brush(ids[i]==target?"#75573B":"Transparent");}if(different)Find<ScrollViewer>("PageScroll").ScrollToTop();
  }
  static Brush Brush(string color){return (Brush)new BrushConverter().ConvertFromString(color);}
  void Filter(){
   bool favorite=Find<ToggleButton>("FavoritesFilter").IsChecked==true;int count=0;
   string[] names={"health","fall","stamina","mana","cold","shroud"};
   for(int i=0;i<6;i++){bool visible=(!favorite||prefs.Favorites[i]);cards[i].Visibility=visible?Visibility.Visible:Visibility.Collapsed;if(visible)count++;}
   for(int i=6;i<8;i++){bool visible=!favorite||prefs.Favorites[i];Find<Border>(i==6?"XpCard":"SkillCard").Visibility=visible?Visibility.Visible:Visibility.Collapsed;if(visible)count++;}
   bool breathVisible=!favorite||prefs.Favorites[8];Find<Border>("BreathCard").Visibility=breathVisible?Visibility.Visible:Visibility.Collapsed;if(breathVisible)count++;
   bool flightVisible=!favorite||prefs.Favorites[9];Find<Border>("FlightCard").Visibility=flightVisible?Visibility.Visible:Visibility.Collapsed;if(flightVisible)count++;
   Find<TextBlock>("FunctionCount").Text=count+" "+T("functionCountSuffix");Find<TextBlock>("NoMatches").Visibility=count==0?Visibility.Visible:Visibility.Collapsed;
  }
  void SavePreferences(){try{if(!preview)prefs.Save();}catch(Exception ex){Files.Log(ex.ToString());Say(T("unexpectedError"));}}
  void SaveSettings(){
   int[] keys=combos.Select(c=>c.SelectedIndex).ToArray();if(keys.Any(k=>k<0)||keys.Distinct().Count()!=7){Say(T("hotkeyDuplicate"));return;}
   bool useGlobal=Find<CheckBox>("GlobalHotkeys").IsChecked==true;UnregisterKeys();if(buildingKeys!=null&&!buildingKeys.TrySave(keys,useGlobal)){RegisterKeys();return;}
   prefs.Keys=keys;prefs.Global=useGlobal;prefs.Topmost=Find<CheckBox>("TopmostSetting").IsChecked==true;Window.Topmost=prefs.Topmost;
   for(int i=0;i<6;i++)keyButtons[i].Content="F"+(prefs.Keys[i]+1);SavePreferences();Say(T("settingsSaved"));RegisterKeys();
  }
  void UnregisterKeys(){if(buildingKeys!=null)buildingKeys.Unregister();if(hwnd!=IntPtr.Zero)for(int i=0;i<7;i++)Native.UnregisterHotKey(hwnd,100+i);}
  void RegisterKeys(){UnregisterKeys();if(hwnd==IntPtr.Zero||preview){if(buildingKeys!=null)buildingKeys.Register(hwnd,prefs.Global,prefs.Keys);return;}if(prefs.Global)for(int i=0;i<7;i++)if(!Native.RegisterHotKey(hwnd,100+i,0x4000,(uint)(0x70+prefs.Keys[i])))Say(T("hotkeyConflict"));if(buildingKeys!=null)buildingKeys.Register(hwnd,prefs.Global,prefs.Keys);RefreshBuildingKeyScope();}
  IntPtr Hotkey(IntPtr handle,int msg,IntPtr wParam,IntPtr lParam,ref bool handled){if(msg==0x312){if(buildingKeys!=null&&buildingKeys.Handle(wParam.ToInt32(),!busy&&IsRelevantForeground())){handled=true;return IntPtr.Zero;}int id=wParam.ToInt32()-100;if(id>=0&&id<7&&IsRelevantForeground()){Action(id);handled=true;}}return IntPtr.Zero;}
  [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
  [System.Runtime.InteropServices.DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
  delegate void WinEventCallback(IntPtr hook,uint evt,IntPtr window,int obj,int child,uint thread,uint time);
  [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr SetWinEventHook(uint min,uint max,IntPtr module,WinEventCallback callback,uint process,uint thread,uint flags);
  [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool UnhookWinEvent(IntPtr hook);
  void RefreshBuildingKeyScope(){if(!closing&&buildingKeys!=null)buildingKeys.UpdateForeground(IsRelevantForeground());}
  bool IsRelevantForeground(){uint pid;GetWindowThreadProcessId(GetForegroundWindow(),out pid);return pid==(uint)Process.GetCurrentProcess().Id||(session.Game!=null&&pid==(uint)session.Game.Id);}
  void Action(int index){if(busy)return;if(index==6){Off();return;}if(toggles[index].IsEnabled){toggles[index].IsChecked=toggles[index].IsChecked!=true;Toggle(index);}}
  void Toggle(int index){
   if(changing||busy)return;
   try{session.Set(index,toggles[index].IsChecked==true);StateLabels();Say(T(new[]{"health","fall","stamina","mana","cold","shroud"}[index])+": "+T(toggles[index].IsChecked==true?"active":"off"));}
   catch(Exception ex){Files.Log(ex.ToString());changing=true;toggles[index].IsChecked=false;changing=false;Say(ex.Message);StateLabels();}
  }
  void Off(){if(busy)return;try{session.AllOff();ResetToggles();Say(T("allOffDone"));}catch(Exception ex){Files.Log(ex.ToString());Say(ex.Message);}}
  void ResetToggles(){changing=true;foreach(var toggle in toggles)toggle.IsChecked=false;changing=false;if(building!=null)building.Reset();if(progression!=null)progression.Reset();if(movement!=null)movement.Reset();StateLabels();}
  void StateLabels(){for(int i=0;i<6;i++){bool active=toggles[i].IsChecked==true;states[i].Text=T(active?"active":"off");states[i].Foreground=Brush(active?"#E8B778":"#81979F");}Find<TextBlock>("ActiveCount").Text=(toggles.Count(t=>t.IsChecked==true)+(building==null?0:building.ActiveCount)+(progression==null?0:progression.ActiveCount)+(movement==null?0:movement.ActiveCount))+" "+T("activeCountSuffix");}
  async Task ConnectOrDisconnect(){
   if(busy||preview)return;busy=true;if(movement!=null)movement.Reset();if(progression!=null)progression.Reset();if(building!=null)building.Root.IsEnabled=false;Find<Button>("Connect").IsEnabled=false;Find<Button>("Connect").Content=T("connecting");foreach(var toggle in toggles)toggle.IsEnabled=false;
   try {
    if(session.IsConnected){await Task.Run(()=>session.Dispose());ResetToggles();Find<TextBlock>("ConnectionStatus").Text=T("connectionIntro");Find<TextBlock>("SidebarConnection").Text=T("notConnected");Find<TextBlock>("Values").Visibility=Visibility.Collapsed;Say(T("allOffDone"));}
    else {await Task.Run(()=>session.Connect());Array.Clear(lastCaptures,0,5);Array.Clear(seen,0,5);Find<TextBlock>("ConnectionStatus").Text=T("connectionWaiting");Find<TextBlock>("SidebarConnection").Text=T("waitingWorld");Say(T("connectionWaiting"));}
   }catch(Win32Exception ex){Files.Log(ex.ToString());Say(ex.NativeErrorCode==5?T("adminExplain"):ex.Message);if(ex.NativeErrorCode==5)Page("settings");}
   catch(Exception ex){Files.Log(ex.ToString());Say(ex.Message);}
   finally{busy=false;if(building!=null)building.Root.IsEnabled=true;Find<Button>("Connect").IsEnabled=true;Find<Button>("Connect").Content=T(session.IsConnected?"disconnect":"connect");Poll();}
  }
  void Poll(){
   if(busy||preview)return;RefreshBuildingKeyScope();
   if(session.Game!=null&&!session.IsAlive){try{session.Dispose();}catch(Exception ex){Files.Log(ex.ToString());}ResetToggles();Find<TextBlock>("SidebarConnection").Text=T("notConnected");Find<TextBlock>("ConnectionStatus").Text=T("gameClosed");Find<Button>("Connect").Content=T("connect");Find<TextBlock>("Values").Visibility=Visibility.Collapsed;Say(T("gameClosed"));}
   if(!session.IsConnected){foreach(var toggle in toggles)toggle.IsEnabled=false;if(building!=null)building.Poll(false);if(progression!=null)progression.Reset();if(movement!=null)movement.Reset();return;}
   try {
    byte[] b=session.Sample();long now=DateTime.UtcNow.Ticks;
    int[] counters={0x50,0x54,0xa0,0xd0,0x100};int[] pointers={0x18,0x38,0x88,0xb8,0xe8};int[] maxOffsets={0x2c,0x4c,0x9c,0xcc,0xfc};bool[] fresh=new bool[5];
    for(int i=0;i<5;i++){uint capture=BitConverter.ToUInt32(b,counters[i]);if(capture!=lastCaptures[i]){seen[i]=now;lastCaptures[i]=capture;}fresh[i]=seen[i]!=0&&now-seen[i]<TimeSpan.FromSeconds(6).Ticks&&BitConverter.ToInt64(b,pointers[i])>0x10000&&BitConverter.ToInt32(b,maxOffsets[i])>0;}
    bool[] ready={fresh[0],fresh[0],fresh[1],fresh[2],fresh[3]&&fresh[0],fresh[4]};
    // Clear replacements on world loss, or a stale capture for an enabled function.
    if(Enumerable.Range(0,6).Any(i=>!ready[i]&&BitConverter.ToInt32(b,GameSession.FlagOffsets[i])!=0)){session.AllOff();foreach(int offset in GameSession.FlagOffsets)Array.Clear(b,offset,4);}
    changing=true;for(int i=0;i<6;i++){toggles[i].IsEnabled=ready[i];toggles[i].IsChecked=BitConverter.ToInt32(b,GameSession.FlagOffsets[i])!=0;}changing=false;StateLabels();
    bool world=fresh[0];if(movement!=null)movement.Poll(world);if(progression!=null)progression.Poll(world);if(building!=null)building.Poll(world);StateLabels();
    Find<TextBlock>("SidebarConnection").Text=T(world?"connected":"waitingWorld");Find<TextBlock>("SidebarConnection").Foreground=Brush(world?"#8BC8BA":"#D4A371");
    Find<TextBlock>("ConnectionStatus").Text=T(world?"connectionReady":"connectionWaiting");
    Find<TextBlock>("Values").Visibility=world?Visibility.Visible:Visibility.Collapsed;
    Find<TextBlock>("Values").Text=T("healthValues")+": "+BitConverter.ToInt32(b,0x28)+" / "+BitConverter.ToInt32(b,0x2c)+"   ·   "+T("staminaValues")+": "+BitConverter.ToInt32(b,0x48)+" / "+BitConverter.ToInt32(b,0x4c)+"   ·   "+T("manaValues")+": "+BitConverter.ToInt32(b,0x98)+" / "+BitConverter.ToInt32(b,0x9c);

   }catch(Exception ex){Files.Log(ex.ToString());Say(ex.Message);foreach(var toggle in toggles)toggle.IsEnabled=false;}
  }
  void Elevate(){
   if(busy||preview)return;
   try{session.Dispose();ResetToggles();var start=new ProcessStartInfo(Assembly.GetExecutingAssembly().Location,"--wait-for "+Process.GetCurrentProcess().Id){UseShellExecute=true,Verb="runas"};Process.Start(start);Window.Close();}
   catch(Exception ex){Files.Log(ex.ToString());Say(T("elevationError"));}
  }
  void Layout(double width,double height){Find<RowDefinition>("HeroRow").Height=new GridLength(width/5.4);Find<TextBlock>("CharacterHeading").FontSize=width<1180?24:30;foreach(var card in cards)card.Padding=new Thickness(height<840?14:20);foreach(var button in nav)button.Padding=new Thickness(14,height<840?6:9,14,height<840?6:9);}
  internal void Render(string path,int width=1280,int height=910){
   Layout(width,height);
   Window.Width=width;Window.Height=height;var root=(FrameworkElement)Window.Content;root.Measure(new Size(width,height));root.Arrange(new Rect(0,0,width,height));root.UpdateLayout();
   var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(root);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(path))encoder.Save(file);
  }
 }
 internal static class Program {
  [STAThread] static int Main(string[] args){
   try {
    if(args.Length==2&&args[0]=="--wait-for"){try{using(var previous=Process.GetProcessById(int.Parse(args[1]))){if(!previous.WaitForExit(30000))return 1;}}catch(ArgumentException){}args=new string[0];}
    if(args.Length==2&&args[0]=="--watchdog"){GameSession.Watchdog(args[1]);return 0;}
    if(args.Length==2&&args[0]=="--memory-test-host"){SelfTest.MemoryHost(args[1]);return 0;}
    if(args.Length==2&&args[0]=="--self-test"){File.WriteAllText(args[1],SelfTest.Run());return 0;}
    if(args.Length==2&&args[0]=="--catalog-test"){File.WriteAllText(args[1],CatalogTests.Run(GameCatalog.GuessPath()));return 0;}
    var app=new Application();
    if(args.Length==2&&args[0]=="--auto-catalog-test"){
     SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(app.Dispatcher));var view=new MainView(true);var task=view.TestAutoCatalog();var concurrent=view.TestAutoCatalog();if(!concurrent.IsCompleted)throw new Exception("Concurrent catalog read was not suppressed.");
     var frame=new DispatcherFrame();task.ContinueWith(done=>app.Dispatcher.BeginInvoke(new System.Action(()=>frame.Continue=false)));if(!task.IsCompleted)Dispatcher.PushFrame(frame);task.GetAwaiter().GetResult();
     var root=view.Find<Grid>("MainContent").Children.OfType<UserControl>().Single();string source=((TextBlock)root.FindName("CatalogSource")).Text;if(!source.Contains("10745"))throw new Exception(source+" / "+((TextBlock)root.FindName("ObjectStatus")).Text);
     if(!view.TestAutoCatalog().IsCompleted)throw new Exception("Loaded catalog was needlessly re-read.");view.Page("objects");view.Render(args[1]+".png");view.Window.Close();File.WriteAllText(args[1],"PASS automatic loader reads all 10745 game definitions without button click\r\nPASS concurrent load is suppressed\r\nPASS loaded catalog avoids a second read\r\nPASS automatic import updates catalog source and renders correctly\r\nNo cache or game memory was written by this test.");return 0;
    }
    if(args.Length==2&&args[0]=="--hotkey-test"){File.WriteAllText(args[1],BuildingHotkeyTests.Run());return 0;}
    if(args.Length==2&&args[0]=="--scroll-test"){File.WriteAllText(args[1],ScrollTests.Run());return 0;}
    if(args.Length==2&&args[0]=="--preview-catalog"){var view=new MainView(true);view.PreviewCatalog(GameCatalog.Read(GameCatalog.GuessPath(),null));view.Page("objects");view.Render(args[1]);return 0;}
    app.DispatcherUnhandledException+=(s,e)=>{Files.Log(e.Exception.ToString());MessageBox.Show("Emberforge hat einen Fehler festgestellt. Details stehen im Protokoll.","Emberforge");e.Handled=true;};
    if(args.Length>=2&&args[0]=="--preview") {var view=new MainView(true);if(args.Length>2)view.Page(args[2]);view.Render(args[1]);return 0;}
    if(args.Length==2&&args[0]=="--preview-all") {Directory.CreateDirectory(args[1]);var view=new MainView(true);foreach(string page in new[]{"character","settings","future","objects","blocks","craft","equipment"}){view.Page(page);view.Render(Path.Combine(args[1],page+".png"));if(page=="settings"){view.Find<ScrollViewer>("PageScroll").ScrollToVerticalOffset(500);view.Render(Path.Combine(args[1],"building-hotkeys.png"));}}view.Page("character");view.Render(Path.Combine(args[1],"character-small.png"),1040,760);view.Render(Path.Combine(args[1],"character-wide.png"),1920,1080);return 0;}
    bool first;using(var mutex=new Mutex(true,"Local\\Emberforge-Standalone",out first)){
     if(!first){MessageBox.Show("Emberforge läuft bereits.","Emberforge");return 0;}
     var view=new MainView();app.Run(view.Window);
    }
    return 0;
   }catch(Exception ex){Files.Log(ex.ToString());if(args.Length>1){File.WriteAllText(args[1]+".error.txt",ex.ToString());return 1;}MessageBox.Show(ex.Message,"Emberforge");return 1;}
  }
 }
}
