using System;
using System.IO;
using System.Threading;
using Odd2U;
class BundleCheck {
 static int Main(string[] args){try{
  if(!BundledAI.Available)throw new Exception("Bundled runtime missing");
  string reply="";var c=new Conversation();c.Messages.Add(new ChatMessage{Role="user",Text="What is 17 times 6? Reply with the number only."});
  using(var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(90)))LocalChat.Stream(LocalChat.Context(c,"Answer briefly."),s=>reply+=s,timeout.Token).GetAwaiter().GetResult();
  if(!reply.Contains("102"))throw new Exception("Unexpected answer: "+reply);
  File.WriteAllText(args[0],"PASS: bundled CPU runtime + bundled Qwen3 1.7B, isolated loopback endpoint, reply: "+reply+"\n");return 0;
 }catch(Exception e){File.WriteAllText(args[0],e.ToString());return 1;}finally{BundledAI.Stop();}}
}
