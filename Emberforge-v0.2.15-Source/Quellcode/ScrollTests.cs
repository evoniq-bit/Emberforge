using System;
using System.Text;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Emberforge {
 internal static class ScrollTests {
  static T Child<T>(DependencyObject node)where T:DependencyObject {
   for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++){var c=VisualTreeHelper.GetChild(node,i);if(c is T)return (T)c;var found=Child<T>(c);if(found!=null)return found;}return null;
  }
  static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);}
  static void Wheel(UIElement source,int delta){source.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,delta){RoutedEvent=Mouse.PreviewMouseWheelEvent});Pump();}
  internal static string Run(){
   var report=new StringBuilder();var view=new MainView(true);var page=view.Find<ScrollViewer>("PageScroll");
   Action<bool,string> check=(ok,label)=>{report.AppendLine((ok?"PASS ":"FAIL ")+label);if(!ok)throw new Exception(report.ToString());};
   view.Window.ShowInTaskbar=false;view.Window.Left=-20000;view.Window.Top=-20000;view.Window.Width=1040;view.Window.Height=760;view.Window.Show();Pump();
   try {
    foreach(var name in new[]{"character","settings","objects","blocks","future"}){
     view.Page(name);Pump();page.ScrollToTop();Pump();check(page.ScrollableHeight>0,name+" page has scrollable content");
     var source= name=="character"?(UIElement)view.Find<Border>("HealthCard"):name=="settings"?(UIElement)view.Find<StackPanel>("SettingsPage"):name=="future"?(UIElement)view.Find<StackPanel>("FuturePage"):(UIElement)view.Find<Grid>("MainContent").Children.OfType<UserControl>().Single().FindName(name=="objects"?"ObjectAim":"MaterialAim");
     Wheel(source,-120);check(page.VerticalOffset>0,name+" wheel over content scrolls page");Wheel(source,120);check(page.VerticalOffset<0.001,name+" wheel up returns page to top");
    }
    view.PreviewCatalog(GameCatalog.Read(GameCatalog.GuessPath(),null));view.Page("objects");Pump();page.ScrollToTop();Pump();var root=view.Find<Grid>("MainContent").Children.OfType<UserControl>().Single();var list=(ListBox)root.FindName("ObjectList");var inner=Child<ScrollViewer>(list);
    check(((TextBlock)root.FindName("CatalogSource")).Text.Contains("10745"),"UI identifies complete game file catalog");
    var technical=(CheckBox)root.FindName("TechnicalObjects");technical.IsChecked=true;technical.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Pump();check(((TextBlock)root.FindName("ObjectCount")).Text.StartsWith("10745 Treffer"),"all 10745 definitions are searchable when technical filter is enabled");
    var search=(TextBox)root.FindName("ObjectSearch");search.Text="zzzzzz-unmatched";Pump();check(list.Items.Count==0,"full catalog search filters list");search.Text="";Pump();
    list.SelectedIndex=0;((Button)root.FindName("UseSelectedObject")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Pump();check(((TextBlock)root.FindName("ObjectReplacement")).Text!="Noch keine Auswahl","imported definition can be selected as replacement without connection");
    check(inner!=null&&inner.ScrollableHeight>0,"catalog has independent scroll range");inner.ScrollToTop();Pump();
    var row=(UIElement)list.ItemContainerGenerator.ContainerFromIndex(0);Wheel(row,-120);check(inner.VerticalOffset>0&&page.VerticalOffset<0.001,"wheel over catalog scrolls list only");
    inner.ScrollToBottom();Pump();row=(UIElement)list.ItemContainerGenerator.ContainerFromIndex(list.Items.Count-1);Wheel(row,-120);check(page.VerticalOffset>0&&inner.VerticalOffset==inner.ScrollableHeight,"catalog bottom continues with page");
    page.ScrollToBottom();inner.ScrollToTop();Pump();double before=page.VerticalOffset;row=(UIElement)list.ItemContainerGenerator.ContainerFromIndex(0);Wheel(row,120);check(page.VerticalOffset<before&&inner.VerticalOffset<0.001,"catalog top continues upward with page");
   } finally {view.Window.Close();}
   return report.ToString();
  }
 }
}
