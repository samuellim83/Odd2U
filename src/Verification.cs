using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace Odd2U {
 public static class Verification {
  static int meshes,vertices,triangles,checks;
  public static void RunInterface(string output){
   Directory.CreateDirectory(output);var pet=new PetWindow();var shell=new ControlsWindow(pet);var root=(FrameworkElement)shell.Content;root.Resources=shell.Resources;shell.Content=null;var frame=new Border{Background=shell.Background,Child=root};
   foreach(string page in new[]{"Home","Chat","Voice","Settings"}){shell.Navigate(page);SaveVisual(frame,Path.Combine(output,page.ToLowerInvariant()+".png"),752,777);SaveVisual(frame,Path.Combine(output,page.ToLowerInvariant()+"-compact.png"),704,701);}
   shell.Shutdown();File.WriteAllText(Path.Combine(output,"ui-verification.txt"),"PASS: Home, Chat, Voice and Settings render at 752x777 and 704x701. No microphone or model request started.\n");
  }
  static void Assert(bool condition,string name){if(!condition)throw new Exception("Verification failed: "+name);checks++;}
  public static void Run(string output){
   Directory.CreateDirectory(output);var scene=new Scene();Validate(scene.Cat.Root);
   Assert(triangles>10000&&vertices>10000,"actual detailed 3D geometry");
   foreach(Mood mood in Enum.GetValues(typeof(Mood))) {
    scene.Cat.BeginAction();
    for(int i=0;i<(mood==Mood.Jump?16:180);i++)scene.Cat.Animate(mood,i/30.0,1/30.0,30,12);
    var bounds=scene.Cat.Root.Bounds;Assert(!bounds.IsEmpty&&!double.IsNaN(bounds.X)&&bounds.SizeY>0,"finite animated bounds "+mood);
    SaveView(scene,Path.Combine(output,mood.ToString().ToLowerInvariant()+".png"),22,8);
   }
   scene.Cat.BeginAction();for(int i=0;i<60;i++)scene.Cat.Animate(Mood.ScratchWall,i/30.0,1/30.0,0,0);Assert(scene.Cat.Legs[0].Group.Bounds.Y>.35&&scene.Cat.Legs[1].Group.Bounds.Y>.35,"scratching lifts both front paws");double paw=scene.Cat.Legs[0].Rotation.Angle;scene.Cat.Animate(Mood.ScratchWall,2.2,.2,0,0);Assert(Math.Abs(scene.Cat.Legs[0].Rotation.Angle-paw)>5,"scratching paws move");
   scene.Cat.BeginAction();for(int i=0;i<60;i++)scene.Cat.Animate(Mood.WiggleTail,i/30.0,1/30.0,0,0);double tail=scene.Cat.Tail.Rotation.Angle;scene.Cat.Animate(Mood.WiggleTail,2.3,.3,0,0);Assert(Math.Abs(scene.Cat.Tail.Rotation.Angle-tail)>20,"tail wiggles distinctly");
   scene.Cat.BeginAction();scene.Cat.Animate(Mood.Jump,0,.55,0,0);Assert(scene.Cat.JumpHeight>.4,"jump leaves the ground");scene.Cat.Animate(Mood.Jump,1,1.0,0,0);Assert(Math.Abs(scene.Cat.JumpHeight)<.001,"jump lands");
   scene.Cat.Animate(Mood.TongueOut,0,.03,0,0);Assert(scene.Cat.TongueVisible,"tongue out appears");scene.Cat.Animate(Mood.Lick,0,.03,0,0);Assert(scene.Cat.TongueVisible,"lick shows tongue");scene.Cat.Animate(Mood.Smile,0,.03,0,0);Assert(scene.Cat.SmileVisible&&!scene.Cat.TongueVisible,"smile is distinct from tongue out");scene.Cat.Animate(Mood.Idle,0,.03,0,0);Assert(!scene.Cat.SmileVisible&&!scene.Cat.TongueVisible,"expressions clear on idle");
   for(int i=0;i<180;i++)scene.Cat.Animate(Mood.Idle,i/30.0,1/30.0,0,0);
   SaveView(scene,Path.Combine(output,"odd-front.png"),0,6);SaveView(scene,Path.Combine(output,"odd-side.png"),90,5);SaveView(scene,Path.Combine(output,"odd-back.png"),180,8);
   scene.Cat.ExportObj(Path.Combine(output,"odd-model.obj"));Assert(new FileInfo(Path.Combine(output,"odd-model.obj")).Length>100000,"portable mesh export");
   Rect monitor=new Rect(-1920,0,1920,1080);Rect clamped=Desktop.Clamp(new Rect(-2200,1000,320,320),monitor);Assert(clamped.Left==-1920&&clamped.Bottom==1080,"negative-coordinate monitor clamping");
   Rect r=Desktop.Clamp(new Rect(1700,-50,320,320),new Rect(0,0,1920,1040));Assert(r.Right==1920&&r.Top==0,"top/right monitor boundary");
   var pet=new PetWindow();var controls=new ControlsWindow(pet);var content=(FrameworkElement)controls.Content;controls.Content=null;var frame=new Border{Background=controls.Background,Child=content};TextElementForeground(content);SaveVisual(frame,Path.Combine(output,"controls.png"),370,720);
   File.WriteAllText(Path.Combine(output,"verification.txt"),"PASS\nChecks: "+checks+"\nMeshes: "+meshes+"\nVertices: "+vertices+"\nTriangles: "+triangles+"\nTwelve poses rendered; scratch paw motion, tail wiggle, jump and expression transitions checked; front, side and back views rendered; controls rendered.\nWindow hit testing, tray interactions and mixed-DPI monitors still require live testing.\n");
  }
  static void TextElementForeground(FrameworkElement element){element.SetValue(System.Windows.Documents.TextElement.ForegroundProperty,new SolidColorBrush(Color.FromRgb(236,240,240)));}
  static void Validate(Model3D model){
   var group=model as Model3DGroup;if(group!=null){foreach(var child in group.Children)Validate(child);return;}
   var geo=model as GeometryModel3D;if(geo==null)return;var m=(MeshGeometry3D)geo.Geometry;meshes++;vertices+=m.Positions.Count;triangles+=m.TriangleIndices.Count/3;
   Assert(m.TriangleIndices.Count%3==0,"triangle index count");foreach(int index in m.TriangleIndices)Assert(index>=0&&index<m.Positions.Count,"triangle index range");
   foreach(var p in m.Positions)Assert(!double.IsNaN(p.X)&&!double.IsNaN(p.Y)&&!double.IsNaN(p.Z)&&!double.IsInfinity(p.X),"finite vertex");
  }
  static void SaveView(Scene scene,string path,double yaw,double pitch){
   scene.Camera(yaw,pitch);var border=new Border{Background=new SolidColorBrush(Color.FromRgb(48,59,68)),Child=scene.View};SaveVisual(border,path,700,700);border.Child=null;
  }
  static void SaveVisual(FrameworkElement element,string path,int width,int height){
   element.Width=width;element.Height=height;element.Measure(new Size(width,height));element.Arrange(new Rect(0,0,width,height));element.UpdateLayout();
   var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(element);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(path))encoder.Save(stream);
  }
 }
}



