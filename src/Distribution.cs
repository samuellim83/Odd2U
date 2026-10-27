using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Odd2U {
 public static class AppData {
  public static string Root {get{return File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"portable.flag"))?Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Data"):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Odd2U");}}
 }
 public static class BundledAI {
  static Process process;static IntPtr job;static string endpoint;static readonly SemaphoreSlim gate=new SemaphoreSlim(1,1);
  public static bool Available {get{return File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ai","ollama.exe"));}}
  public static async Task<string> Ensure(CancellationToken token){
   if(!Available)return "http://127.0.0.1:11434";
   await gate.WaitAsync(token);
   try{
    if(process==null||process.HasExited){
     Stop();var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();int port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
     string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ai");string state=Path.Combine(AppData.Root,"AI");Directory.CreateDirectory(state);
     var start=new ProcessStartInfo(Path.Combine(root,"ollama.exe"),"serve"){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=root,RedirectStandardOutput=true,RedirectStandardError=true};
     start.EnvironmentVariables["OLLAMA_HOST"]="127.0.0.1:"+port;start.EnvironmentVariables["OLLAMA_MODELS"]=Path.Combine(root,"models");start.EnvironmentVariables["OLLAMA_NO_CLOUD"]="1";start.EnvironmentVariables["OLLAMA_KEEP_ALIVE"]="30s";start.EnvironmentVariables["OLLAMA_MAX_LOADED_MODELS"]="1";start.EnvironmentVariables["OLLAMA_NUM_PARALLEL"]="1";
     start.EnvironmentVariables["USERPROFILE"]=state;start.EnvironmentVariables["HOME"]=state;
     job=CreateJobObject(IntPtr.Zero,null);var limits=new ExtendedLimits();limits.Basic.LimitFlags=0x2000;
     if(job==IntPtr.Zero||!SetInformationJobObject(job,9,ref limits,(uint)Marshal.SizeOf(typeof(ExtendedLimits)))){Stop();throw new IOException("Cannot isolate AI process.");}
     process=Process.Start(start);if(!AssignProcessToJobObject(job,process.Handle)){process.Kill();Stop();throw new IOException("Cannot contain AI process.");}
     process.OutputDataReceived+=delegate{};process.ErrorDataReceived+=delegate{};process.BeginOutputReadLine();process.BeginErrorReadLine();endpoint="http://127.0.0.1:"+port;
    }
    for(int i=0;i<100;i++){
     token.ThrowIfCancellationRequested();if(process.HasExited)throw new IOException("Bundled AI exited.");
     try{var req=(HttpWebRequest)WebRequest.Create(endpoint+"/api/tags");req.Proxy=null;using(token.Register(req.Abort))using(await req.GetResponseAsync()){return endpoint;}}catch(WebException){token.ThrowIfCancellationRequested();}
     await Task.Delay(200,token);
    }
    throw new IOException("AI startup timed out.");
   }catch{Stop();throw;}finally{gate.Release();}
  }
  public static void Stop(){if(job!=IntPtr.Zero){CloseHandle(job);job=IntPtr.Zero;}if(process!=null){process.Dispose();process=null;}endpoint=null;}
  [StructLayout(LayoutKind.Sequential)] struct BasicLimits {public long PerProcess,PerJob;public uint LimitFlags;public UIntPtr Min,Max;public uint Active;public UIntPtr Affinity;public uint Priority,Scheduling;}
  [StructLayout(LayoutKind.Sequential)] struct IoCounters {public ulong A,B,C,D,E,F;}
  [StructLayout(LayoutKind.Sequential)] struct ExtendedLimits {public BasicLimits Basic;public IoCounters Io;public UIntPtr ProcessMemory,JobMemory,PeakProcess,PeakJob;}
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr CreateJobObject(IntPtr attrs,string name);
  [DllImport("kernel32.dll")] static extern bool SetInformationJobObject(IntPtr job,int info,ref ExtendedLimits limits,uint length);
  [DllImport("kernel32.dll")] static extern bool AssignProcessToJobObject(IntPtr job,IntPtr process);
  [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
 }
}
