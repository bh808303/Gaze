using System.Text.Json;
using Gaze.App;
using Gaze.Fluent;
using Gaze.Core;
internal static class Program
{
    [STAThread] static int Main()
    {
        try {
            if ((int)CaptureMode.Window != 0 || (int)CaptureMode.AllMonitors != 1 || (int)CaptureMode.CurrentMonitor != 2 || (int)CaptureMode.Transcribe != 3) throw new Exception("Capture mode serialization changed");
            var transcribe = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(new Settings { Mode = CaptureMode.Transcribe }))!;
            if (transcribe.Mode != CaptureMode.Transcribe) throw new Exception("Transcribe mode did not round-trip");
            var property = typeof(Settings).GetProperty("RecordTimelineMetadata");
            if (property == null) throw new Exception("Settings has no optional metadata toggle");
            var legacy = JsonSerializer.Deserialize<Settings>("{\"KeyboardTimeline\":true}")!;
            if ((bool)property.GetValue(legacy)! || !legacy.KeyboardTimeline) throw new Exception("Migration altered opt-ins");
            property.SetValue(legacy,true);
            var loaded=JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(legacy))!;
            if (!(bool)property.GetValue(loaded)!) throw new Exception("Toggle persistence failed");
            var options = loaded.CollectionOptions;
            var timeline = new RecordingTimeline(5, options);
            property.SetValue(loaded,false); options.RecordTimelineMetadata=false;
            timeline.Frame(DateTimeOffset.UtcNow);
            if (timeline.Snapshot(1).Slice(0,1).Collection is not { RecordTimelineMetadata:true, KeyboardTimeline:true }) throw new Exception("Recording collection options were not frozen at startup");
            property.SetValue(loaded,true);
            var panel = JsonSerializer.Deserialize<PanelOptions>(JsonSerializer.Serialize(loaded))!;
            if (!(bool)typeof(PanelOptions).GetProperty("RecordTimelineMetadata")!.GetValue(panel)!) throw new Exception("Panel options lost toggle");
            using var form = new SettingsForm(loaded, _ => {});
            IEnumerable<Control> Controls(Control c) => c.Controls.Cast<Control>().SelectMany(x => new[]{x}.Concat(Controls(x)));
            var toggle = Controls(form).OfType<CheckBox>().Single(c => c.Text == "Record timeline metadata");
            var modeChoices = Controls(form).OfType<ComboBox>().Single(c => c.Items.Contains("Transcribe"));
            if (modeChoices.Items.Count != 4 || modeChoices.Items[3]?.ToString() != "Transcribe") throw new Exception("Capture mode dropdown mapping changed");
            if (!toggle.Checked) throw new Exception("Settings UI lost persisted toggle");
            Console.WriteLine("PASS actual Settings migration/persistence, panel DTO and settings toggle"); return 0;
        } catch(Exception e) { Console.WriteLine("FAIL " + e.Message); return 1; }
    }
}
