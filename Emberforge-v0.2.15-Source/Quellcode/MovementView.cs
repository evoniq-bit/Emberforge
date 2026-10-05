using System;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
namespace Emberforge {
 internal sealed class MovementView {
  readonly MainView parent;readonly GameSession session;readonly bool preview;readonly ProgressionEngine engine;
  readonly ToggleButton breath,flight,dismantle;readonly TextBlock status,flightStatus,dismantleStatus;bool changing,worldReady;
  internal int ActiveCount{get{return (breath.IsChecked==true?1:0)+(flight.IsChecked==true?1:0)+(dismantle.IsChecked==true?1:0);}}
  internal MovementView(MainView view,GameSession game,bool onlyPreview,UserControl objects){
   parent=view;session=game;preview=onlyPreview;engine=new ProgressionEngine(game);
   breath=parent.Find<ToggleButton>("BreathToggle");status=parent.Find<TextBlock>("BreathState");flight=parent.Find<ToggleButton>("FlightToggle");flightStatus=parent.Find<TextBlock>("FlightState");
   dismantle=(ToggleButton)objects.FindName("DismantleToggle");dismantleStatus=(TextBlock)objects.FindName("DismantleState");
   breath.Click+=(s,e)=>Toggle(breath,0x180);flight.Click+=(s,e)=>Toggle(flight,0x184);dismantle.Click+=(s,e)=>Toggle(dismantle,0x188);Reset();
  }
  void Toggle(ToggleButton button,int offset){if(changing||preview)return;try{if(!worldReady)throw new InvalidOperationException("Player not ready");Set(offset,button.IsChecked==true);Labels();}catch(Exception ex){Files.Log("Movement option: "+ex);try{Set(offset,false);}catch{}button.IsChecked=false;Labels();}}
  void Set(int offset,bool on){if(!session.IsConnected)throw new InvalidOperationException("Not connected");GameSession.Write(session.Handle,session.Receipt.Data+offset,BitConverter.GetBytes(on?1:0));}
  void Labels(){status.Text=parent.T(breath.IsChecked==true?"active":"off");flightStatus.Text=parent.T(flight.IsChecked==true?"active":"off");dismantleStatus.Text=parent.T(dismantle.IsChecked==true?"active":"off");}
  internal void LanguageChanged(){Labels();}
  internal void Reset(){worldReady=false;changing=true;foreach(var button in new[]{breath,flight,dismantle}){button.IsChecked=false;button.IsEnabled=false;}changing=false;Labels();}
  internal void Poll(bool world){
   if(!world||!session.IsConnected){if(session.IsConnected){Set(0x180,false);Set(0x184,false);Set(0x188,false);GameSession.Write(session.Handle,session.Receipt.Data+0x1a0,new byte[8]);}Reset();return;}
   try{long player=BitConverter.ToInt64(GameSession.Read(session.Handle,session.Receipt.Data+0x1710,8),0);long actor=0;if(ProgressionEngine.Pointer(player)&&BitConverter.ToInt32(GameSession.Read(session.Handle,player+0x10,4),0)==1)actor=engine.Component(player,3,3600);GameSession.Write(session.Handle,session.Receipt.Data+0x1a0,BitConverter.GetBytes(actor));worldReady=ProgressionEngine.Pointer(actor);if(!worldReady)Set(0x184,false);
    byte[] b=GameSession.Read(session.Handle,session.Receipt.Data+0x180,12);changing=true;breath.IsEnabled=!preview&&worldReady;breath.IsChecked=BitConverter.ToInt32(b,0)!=0;flight.IsEnabled=!preview&&worldReady;flight.IsChecked=BitConverter.ToInt32(b,4)!=0;dismantle.IsEnabled=!preview&&worldReady;dismantle.IsChecked=BitConverter.ToInt32(b,8)!=0;changing=false;Labels();}
   catch(Exception ex){Files.Log("Movement poll: "+ex);try{Set(0x180,false);Set(0x184,false);Set(0x188,false);}catch{}Reset();}
  }
 }
}
