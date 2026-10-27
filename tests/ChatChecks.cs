using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Odd2U;
class ChatChecks {
 static void Assert(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);}
 static int Main(string[] args){try{Run(args[0]).GetAwaiter().GetResult();return 0;}catch(Exception e){Console.WriteLine(e);return 1;}}
 static async Task Run(string root){
  var store=new ChatStore(Path.Combine(root,Guid.NewGuid().ToString("N")));var c=new Conversation();c.Title="Synthetic test";c.Messages.Add(new ChatMessage{Role="user",Text="What is 17 times 6?"});store.Save(c);c.Messages.Add(new ChatMessage{Role="assistant",Text="partial",Complete=false});store.Save(c);int skipped;var loaded=store.Load(out skipped);Assert(loaded.Count==1&&loaded[0].Messages.Count==2&&!loaded[0].Messages[1].Complete,"atomic save and reload incomplete history");
  Assert(LocalChat.Context(c,"Be concise").Length==2,"exclude incomplete reply from context");
  string reply="";using(var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(60))){await LocalChat.Stream(LocalChat.Context(c,"Answer briefly."),s=>reply+=s,timeout.Token);}Assert(reply.Contains("102"),"real local model streaming reply");
  using(var cancel=new CancellationTokenSource()){cancel.Cancel();bool stopped=false;try{await LocalChat.Stream(LocalChat.Context(c,"Answer briefly."),s=>{},cancel.Token);}catch{stopped=true;}Assert(stopped,"cancelled network request terminates");}
  store.Delete(c);Assert(store.Load(out skipped).Count==0,"delete individual history");store.Save(c);var other=new Conversation();store.Save(other);store.DeleteAll();Assert(store.Load(out skipped).Count==0,"delete all history");
 }
}
