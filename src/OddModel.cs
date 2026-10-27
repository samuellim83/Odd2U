using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Odd2U {
 public enum Mood { Idle, Walk, Run, Sit, Sleep, Play, Jump, TongueOut, Lick, Smile, ScratchWall, WiggleTail }
 public sealed class Joint {
  public readonly Model3DGroup Group = new Model3DGroup();
  public readonly AxisAngleRotation3D Rotation;
  public readonly TranslateTransform3D Offset = new TranslateTransform3D();
  public readonly ScaleTransform3D Scale = new ScaleTransform3D();
  public Joint(Vector3D axis, Point3D pivot) {
   Rotation = new AxisAngleRotation3D(axis,0);
   var t=new Transform3DGroup(); t.Children.Add(Scale);t.Children.Add(new RotateTransform3D(Rotation,pivot)); t.Children.Add(Offset); Group.Transform=t;
  }
 }
 public sealed class OddModel {
  public readonly Model3DGroup Root=new Model3DGroup();
  public readonly Joint Body=new Joint(new Vector3D(1,0,0),new Point3D(0,1,0));
  public readonly Joint Head=new Joint(new Vector3D(0,1,0),new Point3D(0,1.65,.8));
  public readonly Joint[] Legs=new Joint[4];
  public readonly Joint[] Ears=new Joint[2];
  public readonly Joint Tail=new Joint(new Vector3D(0,0,1),new Point3D(.12,.88,-.90));
  readonly ScaleTransform3D[] eyeScales=new ScaleTransform3D[2];
  readonly ScaleTransform3D breathing=new ScaleTransform3D(1,1,1,0,.5,0);
  readonly AxisAngleRotation3D turn=new AxisAngleRotation3D(new Vector3D(0,1,0),0);
  readonly TranslateTransform3D bounce=new TranslateTransform3D();
  readonly ScaleTransform3D tongueScale=new ScaleTransform3D(0,0,0,0,1.48,1.58);
  readonly TranslateTransform3D tongueMove=new TranslateTransform3D();
  readonly ScaleTransform3D grinScale=new ScaleTransform3D(0,0,0,0,1.53,1.58);
  double actionClock,scratchWeight,wiggleWeight;
  public double JumpHeight {get{return bounce.OffsetY;}}
  public bool TongueVisible {get{return tongueScale.ScaleX>0;}}
  public bool SmileVisible {get{return grinScale.ScaleX>0;}}
  public void BeginAction(){actionClock=0;}
  readonly Material fur=Mat("#F4EEDF",false), pink=Mat("#D89087",false), dark=Mat("#55453D",false);
  double posture,rest;
  static readonly Sculpt.Volume[] faceVolumes={
   new Sculpt.Volume(0,1.95,.80,.74,.60,.51),new Sculpt.Volume(-.34,1.70,.98,.39,.35,.32),new Sculpt.Volume(.34,1.70,.98,.39,.35,.32),
   new Sculpt.Volume(0,1.71,1.18,.25,.23,.25),new Sculpt.Volume(-.16,1.61,1.38,.20,.145,.17),new Sculpt.Volume(.16,1.61,1.38,.20,.145,.17),
   new Sculpt.Volume(0,1.48,1.20,.34,.22,.25),new Sculpt.Volume(0,1.62,.77,.61,.40,.45)
  };
  public OddModel() {
   var transform=new Transform3DGroup();transform.Children.Add(breathing);transform.Children.Add(new RotateTransform3D(turn));transform.Children.Add(bounce);Root.Transform=transform;
   Root.Children.Add(Body.Group);Root.Children.Add(Head.Group);Root.Children.Add(Tail.Group);
   Body.Scale.ScaleY=.86;Body.Scale.ScaleZ=.90;
   Head.Scale.CenterY=1.95;Head.Scale.CenterZ=.80;Head.Scale.ScaleX=1.09;Head.Scale.ScaleY=1.04;
   Sculpt.Add(Body.Group,Sculpt.Blend(new [] {
    new Sculpt.Volume(0,.92,-.20,.81,.74,.86),new Sculpt.Volume(0,1.22,.40,.73,.59,.59),
    new Sculpt.Volume(0,1.49,.61,.68,.49,.50),
    new Sculpt.Volume(-.50,.70,-.57,.47,.53,.47),new Sculpt.Volume(.50,.70,-.57,.47,.53,.47),
    new Sculpt.Volume(0,.57,.12,.68,.44,.63)
   },new Point3D(-1.16,-.04,-1.34),new Point3D(1.16,2.16,1.30),.039,.30),fur);
   Sculpt.Add(Head.Group,Sculpt.Blend(faceVolumes,new Point3D(-.86,1.12,.14),new Point3D(.86,2.62,1.65),.028,.13),fur);
   for(int side=0;side<2;side++) {
    double s=side==0?-1:1;
    var ear=new Joint(new Vector3D(0,0,1),new Point3D(s*.48,2.36,.84));Ears[side]=ear;Head.Group.Children.Add(ear.Group);
    SmoothEar(ear.Group,s,fur,pink);
    var eye=new Model3DGroup();eyeScales[side]=new ScaleTransform3D(1,1,1,s*.31,2.07,1.21);eye.Transform=eyeScales[side];Head.Group.Children.Add(eye);
    Eye(eye,s,side==0);
    Blush(Head.Group,s);
    for(int w=0;w<2;w++) {
     double y=1.69+w*.13;
     Sculpt.Add(Head.Group,Sculpt.Curve(new [] {new Point3D(s*.56,y,EyeZ(s*.56,y)+.015),new Point3D(s*.77,y+.035,1.28),new Point3D(s*.96,y+.055-w*.025,1.21)},.012,.007,32,12),dark);
    }
    // A fine lower whisker fans down gently, mirroring the other cheek.
    double lowerY=1.60;
    Sculpt.Add(Head.Group,Sculpt.Curve(new [] {new Point3D(s*.54,lowerY,EyeZ(s*.54,lowerY)+.018),new Point3D(s*.65,lowerY-.0075,(EyeZ(s*.54,lowerY)+.018+1.29)/2),new Point3D(s*.76,lowerY-.0325,(EyeZ(s*.54,lowerY)+.018+1.23)/2)},.006,.0025,32,10),dark);
    var smile=new List<Point3D>();for(int i=0;i<=16;i++){double t=i/16.0,x=s*.30*t,y=1.555-.065*Math.Sin(Math.PI*t)+.035*t;smile.Add(new Point3D(x,y,EyeZ(x,y)+.012));}
    Sculpt.Add(Head.Group,Sculpt.Curve(smile.ToArray(),.008,.004,32,8),dark);
    var brow=new List<Point3D>();for(int i=0;i<=12;i++){double t=i/12.0,x=s*(.23+.18*t),y=2.38+.035*Math.Sin(Math.PI*t);brow.Add(new Point3D(x,y,EyeZ(x,y)+.014));}
    Sculpt.Add(Head.Group,Sculpt.Curve(brow.ToArray(),.018,.006,24,10),Mat("#84766A",false));
   }
   // Small, rounded triangular feline nose instead of a spherical button.
   Sculpt.Add(Head.Group,Sculpt.Blend(new [] {new Sculpt.Volume(-.043,1.735,1.47,.058,.039,.042),new Sculpt.Volume(.043,1.735,1.47,.058,.039,.042),new Sculpt.Volume(0,1.70,1.485,.039,.043,.032)},new Point3D(-.13,1.64,1.39),new Point3D(.13,1.80,1.54),.009,.03),Mat("#CF7967",false));
   Sculpt.Add(Head.Group,Sculpt.Curve(new [] {new Point3D(0,1.69,EyeZ(0,1.69)+.013),new Point3D(0,1.62,EyeZ(0,1.62)+.013),new Point3D(0,1.555,EyeZ(0,1.555)+.013)},.006,.005,16,8),dark);
   for(int i=0;i<4;i++) {
    double s=i%2==0?-1:1,z=i<2?.60:-.35,x=s*(i<2?.33:.82);
    var leg=new Joint(new Vector3D(1,0,0),new Point3D(x,.97,z));Legs[i]=leg;Root.Children.Add(leg.Group);
    Sculpt.Add(leg.Group,Sculpt.Blend(new [] {
     new Sculpt.Volume(x,.64,z,i<2?.27:.30,.44,.29),new Sculpt.Volume(x,.31,z+.015,.20,.235,.22),
     new Sculpt.Volume(x,.14,z+.15,.24,.14,.29)
    },new Point3D(x-.38,-.025,z-.36),new Point3D(x+.38,1.20,z+.50),.032,.17),fur);
    for(int toe=-1;toe<=1;toe+=2){double tx=x+toe*.075;Sculpt.Add(leg.Group,Sculpt.Curve(new [] {new Point3D(tx,.095,z+.422),new Point3D(tx,.17,z+.410),new Point3D(tx,.205,z+.37)},.003,.001,14,6),Mat("#C2B6A3",false));}
   }
   // Upright feline tail: broad attached root, soft S-curve, rounded hooked tip.
   Sculpt.Add(Tail.Group,Sculpt.Curve(new [] {
    new Point3D(.12,.88,-.90),new Point3D(.29,1.08,-1.10),
    new Point3D(.47,1.42,-1.18),new Point3D(.57,1.79,-1.17),
    new Point3D(.59,2.12,-1.10),new Point3D(.51,2.34,-.99),
    new Point3D(.35,2.37,-.91),new Point3D(.25,2.27,-.89)
   },.185,.095,128,32),fur);
   var grin=new Model3DGroup{Transform=grinScale};Head.Group.Children.Add(grin);
   AddEllipsoid(grin,new Point3D(0,1.53,1.58),new Vector3D(.16,.045,.025),Mat("#583342",false),36,24);
   AddEllipsoid(grin,new Point3D(0,1.515,1.602),new Vector3D(.085,.014,.007),Mat("#ED9DAA",false),28,16);
   var tongue=new Model3DGroup();var tongueTransform=new Transform3DGroup();tongueTransform.Children.Add(tongueScale);tongueTransform.Children.Add(tongueMove);tongue.Transform=tongueTransform;Head.Group.Children.Add(tongue);
   AddEllipsoid(tongue,new Point3D(0,1.48,1.58),new Vector3D(.073,.115,.035),Mat("#EE8EAA",false),36,24);
   Sculpt.Add(tongue,Sculpt.Curve(new []{new Point3D(0,1.54,1.617),new Point3D(0,1.49,1.617),new Point3D(0,1.44,1.610)},.002,.0015,16,8),Mat("#C96888",false));
  }
  static void SmoothEar(Model3DGroup group,double side,Material outer,Material inner) {
   // Rounded triangular pinna: two curved surfaces meet at a shared rim.
   var corners=new [] {new Point(-.22,-.11),new Point(.22,-.11),new Point(.085,.48)};
   var outline=new List<Point>();
   for(int c=0;c<3;c++) {
    Point a=corners[(c+2)%3],b=corners[c],d=corners[(c+1)%3];
    Point start=new Point(a.X*.34+b.X*.66,a.Y*.34+b.Y*.66),end=new Point(b.X*.66+d.X*.34,b.Y*.66+d.Y*.34);
    for(int j=0;j<12;j++){double t=j/12.0;outline.Add(new Point((1-t)*(1-t)*start.X+2*(1-t)*t*b.X+t*t*end.X,(1-t)*(1-t)*start.Y+2*(1-t)*t*b.Y+t*t*end.Y));}
    Point next=new Point(b.X*.34+d.X*.66,b.Y*.34+d.Y*.66);
    for(int j=0;j<12;j++){double t=j/12.0;outline.Add(new Point(end.X*(1-t)+next.X*t,end.Y*(1-t)+next.Y*t));}
   }
   for(int layer=0;layer<3;layer++) {
    var mesh=new MeshGeometry3D();const int rings=16;int count=outline.Count;
    for(int j=0;j<=rings;j++)for(int i=0;i<=count;i++) {
     double r=(double)j/rings;if(layer==2)r*=.74;
     Point edge=outline[i%count];double xx=edge.X*r,yy=.08+(edge.Y-.08)*r;
     double z=layer==1?.98-.10*(1-r*r):.98-.025*(1-r*r);
     if(layer==2)z+=.003;
     mesh.Positions.Add(new Point3D(side*(.48+xx*1.10),2.40+yy*.90,z+.07*yy-.10));mesh.TextureCoordinates.Add(new Point((xx+.25)/.5,yy+.1));
    }
    for(int j=0;j<rings;j++)for(int i=0;i<count;i++){int a=j*(count+1)+i,b=a+count+1;Tri(mesh,a,a+1,b);Tri(mesh,a+1,b+1,b);}
    SmoothNormals(mesh,layer==1?-1:1);Sculpt.Add(group,mesh,layer==2?inner:outer);
   }
  }
  static void SmoothNormals(MeshGeometry3D mesh,int direction) {
   var normals=new Vector3D[mesh.Positions.Count];
   for(int t=0;t<mesh.TriangleIndices.Count;t+=3){int a=mesh.TriangleIndices[t],b=mesh.TriangleIndices[t+1],c=mesh.TriangleIndices[t+2];var n=Vector3D.CrossProduct(mesh.Positions[b]-mesh.Positions[a],mesh.Positions[c]-mesh.Positions[a]);if(n.Z*direction<0)n=-n;normals[a]+=n;normals[b]+=n;normals[c]+=n;}
   foreach(var n0 in normals){var n=n0;if(n.Length<1e-8)n=new Vector3D(0,0,direction);n.Normalize();mesh.Normals.Add(n);}
  }
  static void Eye(Model3DGroup group,double side,bool blue) {
   var mesh=new MeshGeometry3D();const int rings=16,sides=72;
   for(int j=0;j<=rings;j++)for(int i=0;i<=sides;i++) {
    double r=(double)j/rings,a=i*2*Math.PI/sides;
    double xx=.226*r*Math.Cos(a),yy=.225*r*Math.Sin(a);
    double x=side*.31+xx,y=2.07+yy,z=EyeZ(x,y)+.028*(1-r*r);
    mesh.Positions.Add(new Point3D(x,y,z));var n=new Vector3D(side*.3,0,1);n.Normalize();mesh.Normals.Add(n);mesh.TextureCoordinates.Add(new Point(.5+xx/.452,.5-yy/.450));
   }
   for(int j=0;j<rings;j++)for(int i=0;i<sides;i++){int a=j*(sides+1)+i,b=a+sides+1;Sculpt.Face(mesh,a,a+1,b);Sculpt.Face(mesh,a+1,b+1,b);}
   var drawing=new DrawingGroup();
   using(var dc=drawing.Open()) {
    dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(250,244,222)),null,new Rect(0,0,256,256));
    var iris=new RadialGradientBrush();iris.GradientStops.Add(new GradientStop(blue?Color.FromRgb(118,208,245):Color.FromRgb(121,221,171),0));iris.GradientStops.Add(new GradientStop(blue?Color.FromRgb(52,153,204):Color.FromRgb(45,159,115),.76));iris.GradientStops.Add(new GradientStop(blue?Color.FromRgb(20,53,68):Color.FromRgb(23,56,43),1));
    dc.DrawEllipse(iris,null,new Point(128,141),105,112);
    var rng=new Random(blue?24:29);for(int i=0;i<90;i++){double a=i*Math.PI*2/90,rr=64+rng.NextDouble()*12;var color=blue?Color.FromArgb(55,23,88,122):Color.FromArgb(55,24,99,58);dc.DrawLine(new Pen(new SolidColorBrush(color),1),new Point(128+Math.Cos(a)*rr,141+Math.Sin(a)*rr),new Point(128+Math.Cos(a)*100,141+Math.Sin(a)*105));}
    dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(13,27,31)),null,new Point(128,125),63,75);
    dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(245,255,255,245)),null,new Point(163,73),19,22);
    dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(135,213,242,235)),null,new Point(94,187),14,10); Star(dc,new Point(84,86),13); Star(dc,new Point(164,183),9);
   }
   var material=new MaterialGroup();material.Children.Add(new DiffuseMaterial(new DrawingBrush(drawing)));material.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromRgb(80,80,80)),90));material.Freeze();Sculpt.Add(group,mesh,material);
   var rim=new List<Point3D>();for(int i=0;i<=72;i++){double a=i*Math.PI*2/72,x=side*.31+.228*Math.Cos(a),y=2.07+.227*Math.Sin(a);rim.Add(new Point3D(x,y,EyeZ(x,y)+.006));}
   Sculpt.Add(group,Sculpt.Curve(rim.ToArray(),.014,.014,100,12),Mat("#40394B",false));
  }
  static void Star(DrawingContext dc,Point center,double radius){var geometry=new StreamGeometry();using(var c=geometry.Open()){c.BeginFigure(new Point(center.X,center.Y-radius),true,true);for(int i=1;i<8;i++){double a=i*Math.PI/4-Math.PI/2,r=i%2==0?radius:radius*.30;c.LineTo(new Point(center.X+Math.Cos(a)*r,center.Y+Math.Sin(a)*r),true,false);}}dc.DrawGeometry(Brushes.White,null,geometry);}
  static void Blush(Model3DGroup group,double side){
   var mesh=new MeshGeometry3D();const int rings=10,slices=48;
   for(int j=0;j<=rings;j++)for(int i=0;i<=slices;i++){double r=j/(double)rings,a=i*2*Math.PI/slices,x=side*.51+.15*r*Math.Cos(a),y=1.78+.072*r*Math.Sin(a);mesh.Positions.Add(new Point3D(x,y,EyeZ(x,y)+.006));mesh.Normals.Add(new Vector3D(side*.4,0,1));mesh.TextureCoordinates.Add(new Point(.5+.5*r*Math.Cos(a),.5+.5*r*Math.Sin(a)));}
   for(int j=0;j<rings;j++)for(int i=0;i<slices;i++){int a=j*(slices+1)+i,b=a+slices+1;Sculpt.Face(mesh,a,a+1,b);Sculpt.Face(mesh,a+1,b+1,b);}
   var blush=new RadialGradientBrush();blush.GradientStops.Add(new GradientStop(Color.FromArgb(155,244,150,169),0));blush.GradientStops.Add(new GradientStop(Color.FromArgb(85,244,150,169),.55));blush.GradientStops.Add(new GradientStop(Color.FromArgb(0,244,150,169),1));var mat=new DiffuseMaterial(blush);mat.Freeze();Sculpt.Add(group,mesh,mat);
  }
  static double EyeZ(double x,double y){double lo=.80,hi=1.75;for(int i=0;i<18;i++){double mid=(lo+hi)/2;if(Sculpt.Field(new Point3D(x,y,mid),faceVolumes,.13)<0)lo=mid;else hi=mid;}return (lo+hi)/2+.008;}
  public void Animate(Mood mood,double time,double dt,double heading,double attention) {
   actionClock+=dt;
   scratchWeight+=((mood==Mood.ScratchWall?1:0)-scratchWeight)*Math.Min(1,dt*8);
   wiggleWeight+=((mood==Mood.WiggleTail?1:0)-wiggleWeight)*Math.Min(1,dt*8);
   double desired=mood==Mood.Sleep?1:mood==Mood.Sit?.55:0;
   posture+=(desired-posture)*Math.Min(1,dt*5);
   rest+=((mood==Mood.Sleep?1:0)-rest)*Math.Min(1,dt*5);
   double locomotion=mood==Mood.Walk?1:mood==Mood.Run?1.7:0;
   double phase=time*(mood==Mood.Run?13:8);
   breathing.ScaleX=1+Math.Sin(time*2)*.008;breathing.ScaleY=1+Math.Sin(time*2)*.009;
   turn.Angle=heading;
   bounce.OffsetY=locomotion>0?Math.Abs(Math.Sin(phase))*.035*locomotion:0;
   if(mood==Mood.Jump)bounce.OffsetY=.46*Math.Sin(Math.PI*Math.Min(1,actionClock/1.1));
   bool tongueOut=mood==Mood.TongueOut||mood==Mood.Lick;
   tongueScale.ScaleX=tongueScale.ScaleZ=tongueOut?1:0;
   tongueScale.ScaleY=tongueOut?(mood==Mood.Lick?.55+.45*Math.Sin(actionClock*6)*Math.Sin(actionClock*6):1):0;
   tongueMove.OffsetY=mood==Mood.Lick?.22*Math.Max(0,Math.Sin(actionClock*6)):0;
   tongueMove.OffsetX=mood==Mood.Lick?.045*Math.Sin(actionClock*6):0;
   grinScale.ScaleX=grinScale.ScaleY=grinScale.ScaleZ=mood==Mood.Smile?1:0;
   Body.Rotation.Angle=-(posture-rest)*40;Body.Offset.OffsetY=-(posture-rest)*.20-rest*.40;
   Head.Offset.OffsetY=-.23-(posture-rest)*.12-rest*.76;
   Head.Rotation.Angle=Math.Max(-22,Math.Min(22,attention))+(mood==Mood.Play?Math.Sin(time*7)*13:Math.Sin(time*.8)*2);
   for(int i=0;i<4;i++) {
    Legs[i].Scale.ScaleY=.84-rest*.54-(i<2?0:(posture-rest)*.45);
    Legs[i].Rotation.Angle=Math.Sin(phase+(i==0||i==3?0:Math.PI))*18*locomotion+(posture-rest)*(i<2?0:35);
    if(i<2)Legs[i].Rotation.Angle=Legs[i].Rotation.Angle*(1-scratchWeight)+scratchWeight*(-85+16*Math.Sin(actionClock*9+i*Math.PI));
    Legs[i].Offset.OffsetY=0;
    // Keep each transformed paw on the ground instead of floating after folding.
    double floor=Legs[i].Group.Bounds.Y;
    Legs[i].Offset.OffsetY=-floor+(locomotion>0?Math.Max(0,Math.Sin(phase+(i==0||i==3?0:Math.PI)))*.045:0);
    if(i<2)Legs[i].Offset.OffsetY*=1-scratchWeight;
   }
   Tail.Rotation.Angle=Math.Sin(time*(mood==Mood.Play?5:1.8))*7*(1-wiggleWeight)+wiggleWeight*26*Math.Sin(actionClock*7);
   Tail.Offset.OffsetY=-posture*.25;
   Ears[0].Rotation.Angle=Math.Sin(time*.6)*3;Ears[1].Rotation.Angle=-Math.Sin(time*.7)*3;
   double blink=time%4.9;double open=blink<.18?Math.Max(.025,Math.Abs(blink-.09)/.09):1;
   if(mood==Mood.Sleep)open=.025;
   foreach(var scale in eyeScales) scale.ScaleY=open;
  }
  public static Material Mat(string hex,bool shiny) {
   var g=new MaterialGroup();g.Children.Add(new DiffuseMaterial(new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex))));
   g.Children.Add(new SpecularMaterial(new SolidColorBrush(shiny?Color.FromRgb(165,175,185):Color.FromRgb(8,8,8)),shiny?70:12));g.Freeze();return g;
  }
  public static void AddEllipsoid(Model3DGroup group,Point3D center,Vector3D radii,Material mat,int slices=26,int stacks=18) {
   var mesh=new MeshGeometry3D();
   for(int j=0;j<=stacks;j++) {double v=j*Math.PI/stacks;for(int i=0;i<=slices;i++) {
    double u=i*Math.PI*2/slices;var n=new Vector3D(Math.Sin(v)*Math.Cos(u),Math.Cos(v),Math.Sin(v)*Math.Sin(u));
    mesh.Positions.Add(center+new Vector3D(n.X*radii.X,n.Y*radii.Y,n.Z*radii.Z));var nn=new Vector3D(n.X/radii.X,n.Y/radii.Y,n.Z/radii.Z);nn.Normalize();mesh.Normals.Add(nn);
   }}
   for(int j=0;j<stacks;j++)for(int i=0;i<slices;i++){int a=j*(slices+1)+i,b=a+slices+1;Tri(mesh,a,a+1,b);Tri(mesh,a+1,b+1,b);}
   AddMesh(group,mesh,mat);
  }
  public static void Tube(Model3DGroup group,Point3D a,Point3D b,double radius,Material mat,int sides=8) {
   var axis=b-a;axis.Normalize();var x=Vector3D.CrossProduct(axis,Math.Abs(axis.Y)>.9?new Vector3D(1,0,0):new Vector3D(0,1,0));x.Normalize();var y=Vector3D.CrossProduct(axis,x);var m=new MeshGeometry3D();
   for(int j=0;j<2;j++)for(int i=0;i<sides;i++){double u=i*Math.PI*2/sides;var n=x*Math.Cos(u)+y*Math.Sin(u);m.Positions.Add((j==0?a:b)+n*radius);m.Normals.Add(n);}
   for(int i=0;i<sides;i++){int k=(i+1)%sides;Tri(m,i,k,i+sides);Tri(m,k,k+sides,i+sides);}AddMesh(group,m,mat);
  }
  static void Tri(MeshGeometry3D m,int a,int b,int c){m.TriangleIndices.Add(a);m.TriangleIndices.Add(b);m.TriangleIndices.Add(c);}
  static void AddMesh(Model3DGroup g,MeshGeometry3D mesh,Material mat){mesh.Freeze();var model=new GeometryModel3D(mesh,mat);model.BackMaterial=mat;g.Children.Add(model);}
  public void ExportObj(string path) {
   using(var writer=new StreamWriter(path)){writer.WriteLine("# Odd2U procedural 3D model. Pose snapshot; animation rig lives in source.");int vertex=1;Export(Root,Matrix3D.Identity,writer,ref vertex);}
  }
  static void Export(Model3D model,Matrix3D parent,StreamWriter w,ref int offset) {
   var matrix=model.Transform.Value;matrix.Append(parent);var group=model as Model3DGroup;
   if(group!=null){foreach(var child in group.Children)Export(child,matrix,w,ref offset);return;}
   var geo=model as GeometryModel3D;if(geo==null)return;var mesh=(MeshGeometry3D)geo.Geometry;
   foreach(var p in mesh.Positions){var q=matrix.Transform(p);w.WriteLine(string.Format(CultureInfo.InvariantCulture,"v {0:F6} {1:F6} {2:F6}",q.X,q.Y,q.Z));}
   for(int i=0;i<mesh.TriangleIndices.Count;i+=3)w.WriteLine("f "+(offset+mesh.TriangleIndices[i])+" "+(offset+mesh.TriangleIndices[i+1])+" "+(offset+mesh.TriangleIndices[i+2]));offset+=mesh.Positions.Count;
  }
 }
}







