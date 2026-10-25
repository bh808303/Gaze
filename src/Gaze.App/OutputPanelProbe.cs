using Gaze.Core;
using Gaze.Fluent;
using System.Windows;
using System.Windows.Controls;
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using ListBox = System.Windows.Controls.ListBox;
namespace Gaze.App;

internal static class OutputPanelProbe
{
    internal static void Run(string evidence)
    {
        Directory.CreateDirectory(evidence);
        var root = Path.Combine(Path.GetFullPath(evidence), "fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root,"Screenshots")); Directory.CreateDirectory(Path.Combine(root,"Videos"));
        foreach(var file in new[]{"Screenshots/capture.png","Screenshots/batch.zip","Screenshots/ignore.mp4","Screenshots/keep.txt","Videos/recording.mp4","Videos/ignore.png","old.mp4"}) File.WriteAllText(Path.Combine(root,file),"fixture");
        Directory.CreateDirectory(Path.Combine(root,"Screenshots","nested")); File.WriteAllText(Path.Combine(root,"Screenshots","nested","keep.png"),"nested");
        var options = new PanelOptions { Folder=root, Mode=CaptureMode.Transcribe }; var log = new List<string>();
        PanelWindow panel = null!; string? opened=null, copied=null, folder=null; int clean=0; bool confirm=false; string? confirmation=null; var recycled=new List<string>();
        void Check(bool value,string message) { if(!value) throw new Exception(message); log.Add("PASS " + message); }
        void Click(string name) => ((ButtonBase)panel.FindName(name)).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        try {
            panel = new PanelWindow(()=>options,_=>throw new Exception("Category must not save settings"),()=>{},()=>{},p=>opened=p,()=>folder=panel.ViewedFolder,p=>copied=p,()=>{},()=> { clean++; OutputFiles.Clean(root,panel.ViewedCategory,message=>{confirmation=message; return confirm;},recycled.Add); },()=>false);
            panel.Show(); panel.ShowRecordings(); panel.UpdateLayout();
            var list=(ListBox)panel.FindName("Recordings");
            Check(list.Items.Count==2 && list.Items.Cast<PanelWindow.Recording>().All(f=>Path.GetDirectoryName(f.Path)==Path.Combine(root,"Screenshots")),"Screenshot mode initially lists PNG and ZIP from Screenshots only");
            Check(panel.ViewedCategory==OutputCategory.Screenshots,"default category follows Screenshot mode");
            Click("OpenFile"); Click("CopyFile"); Check(opened==copied && opened!=null && opened.StartsWith(Path.Combine(root,"Screenshots")),"Open and Clipboard use selected screenshot");
            panel.Render(Path.Combine(evidence,"screenshots.png"));
            Click("RecordingsCategory"); Check(list.Items.Count==1 && panel.ViewedCategory==OutputCategory.Recordings,"Recordings tab lists only Videos MP4");
            Click("OpenFile"); Click("CopyFile"); Check(opened==copied && opened==Path.Combine(root,"Videos","recording.mp4"),"Recordings Open/Clipboard use viewed selection");
            // Exercise actual WPF routed handlers, with no shell or clipboard boundary.
            ((ButtonBase)panel.FindName("OpenCategoryFolder")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            Click("Cleanup"); Check(folder==Path.Combine(root,"Videos") && clean==1,"folder and cleanup handlers use viewed category");
            Check(recycled.Count==0 && confirmation!.Contains("Recordings") && confirmation.Contains(Path.Combine(root,"Videos")) && confirmation.Contains("1"),"cleanup cancellation preserves files and names exact category/path/count");
            confirm=true; Click("Cleanup"); Check(recycled.SequenceEqual(new[]{Path.Combine(root,"Videos","recording.mp4")}),"confirmed video cleanup recycles only scoped top-level MP4 via injected fixture");
            panel.Render(Path.Combine(evidence,"recordings.png"));
            Check(options.Mode==CaptureMode.Transcribe && options.Folder==root,"switching categories preserves capture settings");
            Click("ScreenshotsCategory"); Click("OpenCategoryFolder"); Check(folder==Path.Combine(root,"Screenshots"),"Screenshot folder action uses Screenshots");
            recycled.Clear(); Click("Cleanup"); Check(recycled.Count==2 && recycled.All(p=>Path.GetDirectoryName(p)==Path.Combine(root,"Screenshots") && (p.EndsWith(".png") || p.EndsWith(".zip"))),"confirmed screenshot cleanup targets top-level PNG/ZIP only");
            Check(File.Exists(Path.Combine(root,"old.mp4")) && File.Exists(Path.Combine(root,"Screenshots","nested","keep.png")) && File.Exists(Path.Combine(root,"Screenshots","keep.txt")),"cleanup preserves legacy, nested and unrelated files");
            options.Mode=CaptureMode.Window; panel.Home(); panel.ShowRecordings(); Check(panel.ViewedCategory==OutputCategory.Recordings,"video mode defaults to Recordings on reopening");
            panel.CheckNavigation();
            File.WriteAllLines(Path.Combine(evidence,"result.txt"),log);
        } catch(Exception e) { File.WriteAllLines(Path.Combine(evidence,"result.txt"),log.Append(e.ToString())); Environment.ExitCode=1; }
        finally { panel?.DisposePanel(); Directory.Delete(root,true); }
    }
}

