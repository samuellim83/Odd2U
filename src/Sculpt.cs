using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Odd2U {
 // Smooth implicit surfaces and swept curves keep silhouettes continuous at any view angle.
 public static class Sculpt {
  public sealed class Volume {
   public Point3D Center;public Vector3D Radius;
   public Volume(double x,double y,double z,double rx,double ry,double rz){Center=new Point3D(x,y,z);Radius=new Vector3D(rx,ry,rz);}
   public double Distance(Point3D p){var q=p-Center;double a=Math.Sqrt(q.X*q.X/(Radius.X*Radius.X)+q.Y*q.Y/(Radius.Y*Radius.Y)+q.Z*q.Z/(Radius.Z*Radius.Z));return (a-1)*Math.Min(Radius.X,Math.Min(Radius.Y,Radius.Z));}
  }
  public static double Field(Point3D p,Volume[] shapes,double blend){double d=shapes[0].Distance(p);for(int i=1;i<shapes.Length;i++){double b=shapes[i].Distance(p),h=Math.Max(blend-Math.Abs(d-b),0)/blend;d=Math.Min(d,b)-h*h*blend*.25;}return d;}
  public static MeshGeometry3D Blend(Volume[] shapes,Point3D min,Point3D max,double step,double smoothing){
   int nx=(int)Math.Ceiling((max.X-min.X)/step)+1,ny=(int)Math.Ceiling((max.Y-min.Y)/step)+1,nz=(int)Math.Ceiling((max.Z-min.Z)/step)+1;
   var positions=new Point3D[nx*ny*nz];var values=new double[positions.Length];
   for(int z=0;z<nz;z++)for(int y=0;y<ny;y++)for(int x=0;x<nx;x++){int id=x+nx*(y+ny*z);positions[id]=new Point3D(min.X+x*step,min.Y+y*step,min.Z+z*step);values[id]=Field(positions[id],shapes,smoothing);}
   var mesh=new MeshGeometry3D();var cache=new Dictionary<long,int>();
   int[,] tetra={{0,5,1,6},{0,1,2,6},{0,2,3,6},{0,3,7,6},{0,7,4,6},{0,4,5,6}};
   int[,] edges={{0,1},{0,2},{0,3},{1,2},{1,3},{2,3}};
   var cube=new int[8];var ids=new int[4];
   for(int z=0;z<nz-1;z++)for(int y=0;y<ny-1;y++)for(int x=0;x<nx-1;x++){
    int c=x+nx*(y+ny*z);cube[0]=c;cube[1]=c+1;cube[2]=c+1+nx;cube[3]=c+nx;cube[4]=c+nx*ny;cube[5]=cube[4]+1;cube[6]=cube[5]+nx;cube[7]=cube[4]+nx;
    int inside=0;for(int i=0;i<8;i++)if(values[cube[i]]<0)inside++;if(inside==0||inside==8)continue;
    for(int t=0;t<6;t++){
     for(int i=0;i<4;i++)ids[i]=cube[tetra[t,i]];
     var polygon=new List<int>(4);
     for(int e=0;e<6;e++){
      int a=ids[edges[e,0]],b=ids[edges[e,1]];if((values[a]<0)==(values[b]<0))continue;
      long key=((long)Math.Min(a,b)<<32)|(uint)Math.Max(a,b);int index;
      if(!cache.TryGetValue(key,out index)){
       double f=values[a]/(values[a]-values[b]);Point3D p=positions[a]+(positions[b]-positions[a])*f;
       Vector3D n=Normal(p,shapes,smoothing);index=mesh.Positions.Count;mesh.Positions.Add(p);mesh.Normals.Add(n);mesh.TextureCoordinates.Add(new Point(p.X*1.8,p.Y*1.8));cache.Add(key,index);
      }polygon.Add(index);
     }
     if(polygon.Count<3)continue;
     var center=new Point3D();var normal=new Vector3D();foreach(int i in polygon){center+=(Vector3D)mesh.Positions[i]/polygon.Count;normal+=mesh.Normals[i];}normal.Normalize();
     var u=mesh.Positions[polygon[0]]-center;u.Normalize();var v=Vector3D.CrossProduct(normal,u);
     polygon.Sort(delegate(int a,int b){var pa=mesh.Positions[a]-center;var pb=mesh.Positions[b]-center;return Math.Atan2(Vector3D.DotProduct(pa,v),Vector3D.DotProduct(pa,u)).CompareTo(Math.Atan2(Vector3D.DotProduct(pb,v),Vector3D.DotProduct(pb,u)));});
     for(int i=1;i<polygon.Count-1;i++)Face(mesh,polygon[0],polygon[i],polygon[i+1]);
    }
   }return mesh;
  }
  static Vector3D Normal(Point3D p,Volume[] s,double k){const double e=.001;var n=new Vector3D(Field(p+new Vector3D(e,0,0),s,k)-Field(p-new Vector3D(e,0,0),s,k),Field(p+new Vector3D(0,e,0),s,k)-Field(p-new Vector3D(0,e,0),s,k),Field(p+new Vector3D(0,0,e),s,k)-Field(p-new Vector3D(0,0,e),s,k));n.Normalize();return n;}
  public static MeshGeometry3D Curve(Point3D[] controls,double startRadius,double endRadius,int rings,int sides){
   var path=new List<Point3D>();
   for(int j=0;j<=rings;j++){
    double s=(double)j/rings*(controls.Length-1);int i=Math.Min(controls.Length-2,(int)s);double t=s-i,t2=t*t,t3=t2*t;
    Vector3D a=(Vector3D)controls[Math.Max(0,i-1)],b=(Vector3D)controls[i],c=(Vector3D)controls[i+1],d=(Vector3D)controls[Math.Min(controls.Length-1,i+2)];
    path.Add((Point3D)(.5*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t2+(-a+3*b-3*c+d)*t3)));
   }
   bool closed=(controls[0]-controls[controls.Length-1]).Length<1e-6;
   var mesh=new MeshGeometry3D();Vector3D previous=new Vector3D(1,0,0);
   for(int j=0;j<=rings;j++){
    var axis=closed&&(j==0||j==rings)?path[1]-path[rings-1]:path[Math.Min(rings,j+1)]-path[Math.Max(0,j-1)];axis.Normalize();
    var u=previous-axis*Vector3D.DotProduct(previous,axis);if(u.Length<.001)u=Vector3D.CrossProduct(axis,new Vector3D(0,1,0));u.Normalize();previous=u;var v=Vector3D.CrossProduct(axis,u);
    double t=(double)j/rings;double radius=startRadius+(endRadius-startRadius)*t;
    for(int i=0;i<=sides;i++){double a=i*2*Math.PI/sides;var n=u*Math.Cos(a)+v*Math.Sin(a);mesh.Positions.Add(path[j]+n*radius);mesh.Normals.Add(n);mesh.TextureCoordinates.Add(new Point((double)i/sides,t));}
   }
   for(int j=0;j<rings;j++)for(int i=0;i<sides;i++){int a=j*(sides+1)+i,b=a+sides+1;Face(mesh,a,b,a+1);Face(mesh,a+1,b,b+1);}
   if(!closed){Cap(mesh,path[0],path[0]-path[1],startRadius,sides);Cap(mesh,path[rings],path[rings]-path[rings-1],endRadius,sides);}return mesh;
  }
  static void Cap(MeshGeometry3D mesh,Point3D center,Vector3D axis,double radius,int sides){
   axis.Normalize();var u=Vector3D.CrossProduct(axis,Math.Abs(axis.Y)>.9?new Vector3D(1,0,0):new Vector3D(0,1,0));u.Normalize();var v=Vector3D.CrossProduct(axis,u);int first=mesh.Positions.Count;const int rows=8;
   for(int j=0;j<=rows;j++)for(int i=0;i<=sides;i++){double p=j*Math.PI/(2*rows),a=i*2*Math.PI/sides;var n=(u*Math.Cos(a)+v*Math.Sin(a))*Math.Cos(p)+axis*Math.Sin(p);mesh.Positions.Add(center+n*radius);mesh.Normals.Add(n);mesh.TextureCoordinates.Add(new Point((double)i/sides,(double)j/rows));}
   for(int j=0;j<rows;j++)for(int i=0;i<sides;i++){int a=first+j*(sides+1)+i,b=a+sides+1;Face(mesh,a,a+1,b);Face(mesh,a+1,b+1,b);}
  }
  public static void Face(MeshGeometry3D m,int a,int b,int c){var cross=Vector3D.CrossProduct(m.Positions[b]-m.Positions[a],m.Positions[c]-m.Positions[a]);if(cross.LengthSquared<1e-18)return;if(m.Normals.Count==m.Positions.Count&&Vector3D.DotProduct(cross,m.Normals[a])<0){int swap=b;b=c;c=swap;}m.TriangleIndices.Add(a);m.TriangleIndices.Add(b);m.TriangleIndices.Add(c);}
  public static void Add(Model3DGroup group,MeshGeometry3D mesh,Material material){mesh.Freeze();group.Children.Add(new GeometryModel3D(mesh,material){BackMaterial=material});}
 }
}
