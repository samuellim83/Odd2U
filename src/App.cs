using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using System.Xml.Linq;
using Forms=System.Windows.Forms;

namespace Odd2U {
 public static class Program {
  [STAThread] public static int Main(string[] args) {
   try {
    var app=new Application();app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
    if(args.Length>0&&args[0]=="--verify"){Verification.Run(args.Length>1?args[1]:"output");return 0;}
    if(args.Length>0&&args[0]=="--verify-ui"){Verification.RunInterface(args.Length>1?args[1]:"output");return 0;}
    using(var mutex=new System.Threading.Mutex(false,"Local\\Odd2U.Desktop.v1")) {
     bool owned=false;try {owned=mutex.WaitOne(0);}catch(System.Threading.AbandonedMutexException){owned=true;}
     if(!owned){MessageBox.Show("Odd is already running. Look for her cat icon in the system tray.","Odd2U");return 0;}
     var pet=new PetWindow();app.Run(pet);mutex.ReleaseMutex();
    }return 0;
   } catch(Exception e) {File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"odd-error.log"),e.ToString());return 1;}
  }
 }
 public sealed class Settings {
  public double Size=320,Speed=65;public bool Wander=true,Follow=false,Edges=false,Sound=true;
  static string FilePath{get{return Path.Combine(AppData.Root,"settings.xml");}}
  public static Settings Load(){var s=new Settings();try{if(File.Exists(FilePath)){var x=XElement.Load(FilePath);s.Size=Limit((double?)x.Element("Size")??320,220,480);s.Speed=Limit((double?)x.Element("Speed")??65,20,150);s.Wander=(bool?)x.Element("Wander")??true;s.Follow=(bool?)x.Element("Follow")??false;s.Edges=(bool?)x.Element("Edges")??false;s.Sound=(bool?)x.Element("Sound")??true;}}catch{}return s;}
  static double Limit(double n,double min,double max){return double.IsNaN(n)||double.IsInfinity(n)?min:Math.Max(min,Math.Min(max,n));}
  public void Save(){try{Directory.CreateDirectory(Path.GetDirectoryName(FilePath));new XElement("Odd2U",new XElement("Size",Size),new XElement("Speed",Speed),new XElement("Wander",Wander),new XElement("Follow",Follow),new XElement("Edges",Edges),new XElement("Sound",Sound)).Save(FilePath);}catch{}}
 }
 public sealed class Scene {
  public readonly Viewport3D View=new Viewport3D();public readonly OddModel Cat=new OddModel();
  readonly PerspectiveCamera camera=new PerspectiveCamera();
  public Scene(){
   camera.FieldOfView=35;View.Camera=camera;Camera(22,8);
   var root=new Model3DGroup();root.Children.Add(new AmbientLight(Color.FromRgb(100,99,96)));root.Children.Add(new DirectionalLight(Color.FromRgb(225,214,191),new Vector3D(-2,-3,-4)));root.Children.Add(new DirectionalLight(Color.FromRgb(116,130,145),new Vector3D(3,-1,-1)));root.Children.Add(new DirectionalLight(Color.FromRgb(170,159,133),new Vector3D(0,-1,3)));root.Children.Add(Cat.Root);
   View.Children.Add(new ModelVisual3D{Content=root});
  }
  public void Camera(double yaw,double pitch){double y=yaw*Math.PI/180,p=pitch*Math.PI/180;var target=new Point3D(0,1.44,.0);var pos=target+new Vector3D(Math.Sin(y)*Math.Cos(p)*6.5,Math.Sin(p)*6.5,Math.Cos(y)*Math.Cos(p)*6.5);camera.Position=pos;camera.LookDirection=target-pos;camera.UpDirection=new Vector3D(0,1,0);}
 }
 public sealed class PetWindow:Window {
  public readonly Settings Preferences=Settings.Load();
  public readonly Scene Scene=new Scene();
  public Mood CurrentMood=Mood.Idle;
  public bool Paused,Hidden;
  public double Yaw=22,Pitch=8;
  readonly DispatcherTimer timer=new DispatcherTimer();readonly Stopwatch watch=Stopwatch.StartNew();readonly Random random=new Random();
  Forms.NotifyIcon tray;ControlsWindow controls;TextBlock bubble;Border bubbleBorder;TextBlock microphone;
  double last,time,until=5,sayUntil,direction=1,heading;bool dragging,rotating,manual;Point lastMouse;IntPtr perch=IntPtr.Zero;int frames;
  public PetWindow(){
   Title="Odd • desktop companion";Width=Preferences.Size;Height=Preferences.Size;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;Topmost=true;ShowInTaskbar=false;
   var grid=new Grid();Content=grid;grid.Children.Add(Scene.View);
   bubble=new TextBlock{Text="Hello, I'm Odd.",Foreground=Brushes.White,FontSize=13,TextWrapping=TextWrapping.Wrap,MaxWidth=190,MaxHeight=160,Margin=new Thickness(12,7,12,7)};
   bubbleBorder=new Border{Child=bubble,CornerRadius=new CornerRadius(12),Background=new SolidColorBrush(Color.FromArgb(235,31,38,48)),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Top,Visibility=Visibility.Collapsed};grid.Children.Add(bubbleBorder);
   microphone=new TextBlock{Text="● MIC ON",FontSize=12,Background=Brushes.DarkRed,Foreground=Brushes.White,Padding=new Thickness(6),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Bottom,Visibility=Visibility.Collapsed};grid.Children.Add(microphone);
   Loaded+=delegate{Desktop.ToolWindow(this);var a=SystemParameters.WorkArea;Left=a.Right-Width-40;Top=a.Bottom-Height*.89;Clamp();CreateTray();timer.Start();ShowControls();Say("Hello, I'm Odd. Drag me to say hello.",5);};
   MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){if(e.ClickCount==2){ShowControls();return;}dragging=true;perch=IntPtr.Zero;lastMouse=Desktop.Cursor(this);CaptureMouse();e.Handled=true;};
   MouseMove+=delegate{
    if(dragging){Point p=Desktop.Cursor(this);Left+=p.X-lastMouse.X;Top+=p.Y-lastMouse.Y;lastMouse=p;}
    if(rotating){Point p=Desktop.Cursor(this);Yaw+=(p.X-lastMouse.X)*.55;Pitch=Math.Max(-20,Math.Min(50,Pitch+(p.Y-lastMouse.Y)*.35));lastMouse=p;Scene.Camera(Yaw,Pitch);}
   };
   MouseLeftButtonUp+=delegate{if(dragging){dragging=false;ReleaseMouseCapture();Clamp();if(Preferences.Edges)perch=Desktop.FindPerch(this);SetMood(Mood.Play,false);Say("Prrr.",2);if(Preferences.Sound)sound.Meow();}};
   MouseRightButtonDown+=delegate{rotating=true;lastMouse=Desktop.Cursor(this);CaptureMouse();};
   MouseRightButtonUp+=delegate{rotating=false;ReleaseMouseCapture();};
   LostMouseCapture+=delegate{dragging=false;rotating=false;};
   MouseWheel+=delegate(object s,MouseWheelEventArgs e){SetSize(Preferences.Size+Math.Sign(e.Delta)*20);};
   KeyDown+=delegate(object s,KeyEventArgs e){if(e.Key==Key.Escape){Paused=true;ShowControls();}if(e.Key==Key.Space)Paused=!Paused;};
   timer.Interval=TimeSpan.FromMilliseconds(33);timer.Tick+=Tick;
   Closed+=delegate{sound.Dispose();if(controls!=null)controls.Shutdown();timer.Stop();Preferences.Save();if(tray!=null)tray.Dispose();Application.Current.Shutdown();};
  }
  void CreateTray(){
   tray=new Forms.NotifyIcon();tray.Text="Odd2U • your 3D desktop cat";tray.Icon=System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);tray.Visible=true;
   var menu=new Forms.ContextMenuStrip();menu.Items.Add("Odd's controls",null,delegate{Dispatcher.Invoke(new Action(ShowControls));});
   menu.Items.Add("Talk to Odd",null,delegate{Dispatcher.Invoke(new Action(ShowChat));});
   menu.Items.Add("Voice commands",null,delegate{Dispatcher.Invoke(new Action(ShowVoice));});
   menu.Items.Add("Stop listening",null,delegate{Dispatcher.Invoke(new Action(delegate{if(controls!=null)controls.StopListening();}));});
   menu.Items.Add("Show / hide Odd",null,delegate{Dispatcher.Invoke(new Action(ToggleHidden));});
   menu.Items.Add("Pause / resume",null,delegate{Dispatcher.Invoke(new Action(delegate{Paused=!Paused;}));});
   menu.Items.Add("Bring Odd home",null,delegate{Dispatcher.Invoke(new Action(Home));});menu.Items.Add(new Forms.ToolStripSeparator());
   menu.Items.Add("Quit Odd",null,delegate{Dispatcher.Invoke(new Action(Close));});tray.ContextMenuStrip=menu;tray.DoubleClick+=delegate{Dispatcher.Invoke(new Action(ShowControls));};
  }
  public void ShowControls(){if(controls==null){controls=new ControlsWindow(this);controls.Closed+=delegate{controls=null;};}controls.Show();controls.Activate();}
  public void ShowChat(){ShowControls();controls.Navigate("Chat");}
  public void ShowVoice(){ShowControls();controls.Navigate("Voice");}
  public bool MicrophoneOn {get;private set;}
  public void SetListeningIndicator(bool on){MicrophoneOn=on;microphone.Visibility=on?Visibility.Visible:Visibility.Collapsed;if(tray!=null)tray.Text=on?"Odd2U • microphone ON":"Odd2U • your 3D desktop cat";}
  readonly PetSound sound=new PetSound();
  public void SetSound(bool enabled){Preferences.Sound=enabled;if(!enabled)sound.Stop();Preferences.Save();}
  public void ToggleHidden(){Hidden=!Hidden;if(Hidden)Hide();else Show();}
  public void Home(){Hidden=false;Show();perch=IntPtr.Zero;var a=SystemParameters.WorkArea;Left=a.Right-Width-30;Top=a.Bottom-Height*.89;Clamp();SetMood(Mood.Idle,false);}
  public void SetSize(double size){double bottom=Top+Height*.89;Preferences.Size=Math.Max(220,Math.Min(480,size));Width=Height=Preferences.Size;Top=bottom-Height*.89;Clamp();}
  public void SetMood(Mood mood,bool locked){CurrentMood=mood;manual=locked;Scene.Cat.BeginAction();until=time+(mood==Mood.Jump?1.15:mood==Mood.Play?3:5+random.NextDouble()*5);}
  public void Auto(){manual=false;SetMood(Mood.Idle,false);}
  public void Say(string message,double seconds){bubble.Text=message;sayUntil=watch.Elapsed.TotalSeconds+seconds;bubbleBorder.Visibility=Visibility.Visible;}
  public void ClearBubble(){sayUntil=0;bubbleBorder.Visibility=Visibility.Collapsed;}
  void Clamp(){Rect r=Desktop.Clamp(new Rect(Left,Top,Width,Height),Desktop.WorkArea(this));Left=r.Left;Top=r.Top;}
  void Tick(object sender,EventArgs e){
   double now=watch.Elapsed.TotalSeconds,dt=Math.Min(.08,now-last);last=now;
   bubbleBorder.Visibility=now<sayUntil?Visibility.Visible:Visibility.Collapsed;
   if(Hidden)return;
   if(++frames%30==0){Clamp();if(controls==null||!controls.IsVisible)Opacity=Desktop.FullscreenOther(this)?.12:1;else Opacity=1;}
   if(Paused||rotating||dragging)return;time+=dt;
   var work=Desktop.WorkArea(this);double left=work.Left,right=work.Right-Width,floor=work.Bottom-Height*.89;
   Rect surface;
   if(!Preferences.Edges)perch=IntPtr.Zero;
   if(perch!=IntPtr.Zero){if(Desktop.Surface(this,perch,out surface)&&surface.Top>=work.Top+Height*.75){left=Math.Max(left,surface.Left-Width*.2);right=Math.Min(right,surface.Right-Width*.8);floor=surface.Top-Height*.89;}else perch=IntPtr.Zero;}
   if(CurrentMood==Mood.Jump&&time>=until)SetMood(Mood.Idle,true);
   if(!manual){
    if(Desktop.IdleSeconds()>90){CurrentMood=Mood.Sleep;until=time+2;}
    else if(CurrentMood==Mood.Sleep){CurrentMood=Mood.Idle;until=time+3;}
    else if(time>until){CurrentMood=Preferences.Wander?(random.NextDouble()<.6?Mood.Walk:Mood.Sit):Mood.Sit;direction=random.Next(2)==0?-1:1;until=time+4+random.NextDouble()*7;}
   }
   Point cursor=Desktop.Cursor(this);double attention=(cursor.X-Left-Width/2)/20;
   if(Preferences.Follow&&!manual){double delta=cursor.X-(Left+Width*.5);if(Math.Abs(delta)>65){direction=Math.Sign(delta);CurrentMood=Mood.Walk;}else CurrentMood=Mood.Idle;}
   double moving=CurrentMood==Mood.Walk?1:CurrentMood==Mood.Run?1.8:0;
   if(moving>0){Left+=direction*Preferences.Speed*moving*dt;if(Left<=left){Left=left;direction=1;}if(Left>=right){Left=Math.Max(left,right);direction=-1;}}
   Top+=(Math.Max(work.Top,Math.Min(work.Bottom-Height,floor))-Top)*Math.Min(1,dt*8);
   double target=moving>0?direction*67:0;heading+=(target-heading)*Math.Min(1,dt*5);
   Scene.Cat.Animate(CurrentMood,time,dt,heading,attention);
   if(controls!=null)controls.Status(CurrentMood,Paused);
  }
 }
}


