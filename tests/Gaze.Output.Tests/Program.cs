using Gaze.Core;
using Gaze.App;
using System.IO.Compression;
var root = Path.Combine(Path.GetTempPath(), "gaze-output-test-" + Guid.NewGuid());
void Check(bool ok, string message) { if (!ok) throw new Exception(message); Console.WriteLine("PASS " + message); }
try {
 Directory.CreateDirectory(root); File.WriteAllText(Path.Combine(root,"old.mp4"),"legacy");
 var session = new TranscribeSession(); var store = new OutputStore(root); bool fail = true;
 var flow = new TranscribeBatchFlow(session, _ => { if (fail) throw new IOException("injected clipboard failure"); }, _ => { if (fail) throw new IOException("injected ZIP publication failure"); }, _ => {}, store);
 flow.Accept(new byte[]{1,2,3}, "first", flow.Generation);
 var shots = Path.Combine(root,"Screenshots");
 Check(Directory.Exists(shots) && Directory.GetFiles(shots,"*.png").Length == 1, "accepted PNG persists even when clipboard fails");
 Check(flow.NeedsRetry && session.Count == 1,"publication failure retains accepted capture and Retry");
 flow.Retry(); Check(Directory.GetFiles(shots,"*.png").Length == 1,"retry does not duplicate PNG");
 fail=false; flow.Retry(); flow.Undo(); Check(session.Count == 0 && Directory.GetFiles(shots,"*.png").Length == 1,"Undo retains persistent history");
 flow.Accept(new byte[]{4},"second",flow.Generation); flow.NextBatch();
 Check(Directory.GetFiles(shots,"*.png").Length == 2,"Next Batch retains history");
 flow.Accept(new byte[]{5},"third",flow.Generation); fail=true; flow.Zip();
 Check(flow.NeedsRetry && !flow.Closed && Directory.GetFiles(shots,"*.zip").Length == 1,"failed ZIP publication retains batch and one saved ZIP");
 fail=false; flow.Retry(); Check(flow.Closed && Directory.GetFiles(shots,"*.zip").Length == 1,"ZIP retry saves exactly once");
 using(var zip=ZipFile.OpenRead(Directory.GetFiles(shots,"*.zip").Single())) Check(zip.Entries.Count == 2,"ZIP includes current batch PNG and instructions");
 Check(!flow.Accept(new byte[]{9},"late",flow.Generation),"closed flow rejects late capture");
 var limited = new TranscribeSession(); var limits = new TranscribeBatchFlow(limited,_=>{},_=>{},_=>{},new OutputStore(root));
 for(int i=0;i<20;i++) limits.Accept(new byte[]{7},"limit",limits.Generation);
 Check(!limits.Accept(new byte[]{8},"rejected",limits.Generation) && Directory.GetFiles(shots,"*.png").Length==23,"count rejection never saves output");
 var byteSession = new TranscribeSession(); var byteFlow = new TranscribeBatchFlow(byteSession,_=>{},_=>{},_=>{},new OutputStore(root));
 Check(!byteFlow.Accept(new byte[29_000_001],"large",0) && byteSession.LastCaptureRejection==TranscribeCaptureRejection.ByteLimit && Directory.GetFiles(shots,"*.png").Length==23,"byte rejection never saves output");
 Check(!byteFlow.Accept(new byte[]{1},"stale",-1) && Directory.GetFiles(shots,"*.png").Length==23,"stale asynchronous capture never saves output");
 Check(OutputStore.Folder(root,OutputCategory.Recordings)==Path.Combine(root,"Videos"),"video category routes to Videos");
 Check(File.ReadAllText(Path.Combine(root,"old.mp4"))=="legacy","legacy files untouched");
 var blocked=Path.Combine(root,"blocked"); File.WriteAllText(blocked,"file blocks directory");
 var retrySession=new TranscribeSession(); var retryFlow=new TranscribeBatchFlow(retrySession,_=>{},_=>{},_=>{},new OutputStore(blocked));
 retryFlow.Accept(new byte[]{1},"retry",0); Check(retryFlow.NeedsRetry && retrySession.Count==1,"disk failure visible with retained capture");
 retryFlow.Undo(); retryFlow.NextBatch(); retryFlow.Close();
 Check(!retryFlow.Closed && retrySession.Count==1 && !retryFlow.Accept(new byte[]{2},"blocked",0),"failed persistence cannot be discarded or capture duplicated");
 File.Delete(blocked); retryFlow.Retry(); Check(!retryFlow.NeedsRetry && Directory.GetFiles(Path.Combine(blocked,"Screenshots"),"*.png").Length==1,"disk Retry persists exact retained capture");
 var separate = new TranscribeSession(); separate.AddCapture(new byte[]{6},"same sequence"); new OutputStore(root).SaveCaptures(separate.Captures);
 Check(Directory.GetFiles(shots,"*.png").Length==24,"new session sequence collisions save unique history");
 var empty = new TranscribeBatchFlow(new TranscribeSession(),_=>{},_=>{},_=>{},new OutputStore(root)); empty.Zip(); empty.Close();
 Check(Directory.GetFiles(shots,"*.zip").Length==1,"empty finish and close create no ZIP");
 var swapRoot=Path.Combine(root,"swap"); var category=Path.Combine(swapRoot,"Screenshots");
 Directory.CreateDirectory(category); File.WriteAllText(Path.Combine(category,"capture.png"),"original");
 var callbacks=0; var swapBlocked=false; var confirmationReached=false;
 try { OutputFiles.Clean(swapRoot,OutputCategory.Screenshots,_=> {
     confirmationReached=true;
     try { Directory.Move(category,category+"-original"); }
     catch(IOException) { swapBlocked=true; return false; }
     Directory.CreateDirectory(category); File.WriteAllText(Path.Combine(category,"capture.png"),"replacement");
     return true;
 },_=>callbacks++); } catch(IOException) { }
 Check(confirmationReached,"swap regression reaches confirmation before replacement attempt");
 Check(callbacks==0,"confirmation directory replacement invokes no recycle callback");
 Console.WriteLine("PASS directory swap " + (swapBlocked ? "blocked by held handle" : "detected before recycle"));
 if(OperatingSystem.IsWindows()) {
     var junctionCategory=Path.Combine(root,"junction","Screenshots");
     Directory.CreateDirectory(junctionCategory); File.WriteAllText(Path.Combine(junctionCategory,"capture.png"),"original");
     var target=Path.Combine(root,"junction-target"); Directory.CreateDirectory(target);
     File.WriteAllText(Path.Combine(target,"capture.png"),"outside category");
     var junctionReached=false; var junctionCreated=false; callbacks=0;
     try { OutputFiles.Clean(Path.GetDirectoryName(junctionCategory)!,OutputCategory.Screenshots,_=> {
         junctionReached=true;
         try { Directory.Move(junctionCategory,junctionCategory+"-original"); }
         catch(IOException) { return false; }
         using var process=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe",
             $"/c mklink /J \"{junctionCategory}\" \"{target}\"") { UseShellExecute=false, CreateNoWindow=true, RedirectStandardOutput=true, RedirectStandardError=true })!;
         process.WaitForExit();
         if(process.ExitCode!=0) throw new Exception("contained junction creation failed: " + process.StandardError.ReadToEnd());
         junctionCreated=true; return true;
     },_=>callbacks++); } catch(IOException) { }
     finally { if(junctionCreated) Directory.Delete(junctionCategory); }
     Check(junctionReached && callbacks==0 && File.ReadAllText(Path.Combine(target,"capture.png"))=="outside category",
         "confirmation junction replacement invokes no recycle callback and preserves target");
 }
 Console.WriteLine("PASS output increment"); return 0;
} catch(Exception e) { Console.Error.WriteLine(e); return 1; }
finally { if(Directory.Exists(root)) Directory.Delete(root,true); }
