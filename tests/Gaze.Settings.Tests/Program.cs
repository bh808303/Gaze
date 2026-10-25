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
            var triggerProperty = typeof(Settings).GetProperty("TranscribeTrigger") ?? throw new Exception("Missing persisted Screenshot trigger: Print Screen cannot be selected");
            if ((TranscribeTrigger)triggerProperty.GetValue(JsonSerializer.Deserialize<Settings>("{}")!)! != TranscribeTrigger.MiddleMouse) throw new Exception("Legacy trigger must remain Middle mouse");
            foreach (var choice in new[] { TranscribeTrigger.PrintScreen, TranscribeTrigger.MiddleMouse })
            {
                var original = new Settings(); triggerProperty.SetValue(original, choice);
                var restored = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(original))!;
                if ((TranscribeTrigger)triggerProperty.GetValue(restored)! != choice) throw new Exception("Trigger roundtrip lost choice");
                Settings? applied = null;
                IEnumerable<Control> Descendants(Control c) => c.Controls.Cast<Control>().SelectMany(x => new[]{x}.Concat(Descendants(x)));
                using (var edit = new SettingsForm(restored, value => applied = value))
                {
                    edit.Show();
                    var choices = Descendants(edit).OfType<ComboBox>().Single(c => c.AccessibleName == "Screenshot trigger");
                    if (choices.Items.Count != 2 || choices.SelectedIndex != (int)choice) throw new Exception("Trigger UI choices/default mismatch");
                    choices.SelectedIndex = 1 - (int)choice;
                    Descendants(edit).OfType<Button>().Single(c => c.Text == "Cancel").PerformClick();
                    if (applied != null || (TranscribeTrigger)triggerProperty.GetValue(restored)! != choice) throw new Exception("Cancel changed trigger");
                }
                using (var edit = new SettingsForm(restored, value => applied = value))
                {
                    edit.Show();
                    Descendants(edit).OfType<ComboBox>().Single(c => c.AccessibleName == "Screenshot trigger").SelectedIndex = 1 - (int)choice;
                    Descendants(edit).OfType<Button>().Single(c => c.Text == "Save settings").PerformClick();
                    if (applied == null || (int)(TranscribeTrigger)triggerProperty.GetValue(applied)! != 1 - (int)choice) throw new Exception("Save lost trigger");
                }
            }
            IEnumerable<Control> ControlsFor(Control c) => c.Controls.Cast<Control>().SelectMany(x => new[]{x}.Concat(ControlsFor(x)));
            var invalidPath = Path.Combine(Path.GetTempPath(), "gaze-settings-" + Guid.NewGuid() + ".json");
            try
            {
                File.WriteAllText(invalidPath, "{\"TranscribeTrigger\":99}");
                try { Settings.Load(invalidPath); throw new Exception("Invalid trigger accepted"); }
                catch (InvalidDataException) { }
                foreach (var choice in new[] { TranscribeTrigger.MiddleMouse, TranscribeTrigger.PrintScreen })
                {
                    new Settings { TranscribeTrigger = choice }.Save(invalidPath);
                    var persisted = Settings.Load(invalidPath);
                    if (persisted.TranscribeTrigger != choice) throw new Exception("Actual file save/load lost trigger");
                    Settings? retained = null;
                    using var unrelated = new SettingsForm(persisted, value => retained = value);
                    unrelated.Show();
                    ControlsFor(unrelated).OfType<ComboBox>().Single(c => c.Items.Contains("Screenshot")).SelectedIndex = 3;
                    ControlsFor(unrelated).OfType<Button>().Single(c => c.Text == "Save settings").PerformClick();
                    if (retained?.TranscribeTrigger != choice) throw new Exception("Unrelated WinForms mode save lost trigger");
                }
                File.WriteAllText(invalidPath, "{}");
                if (Settings.Load(invalidPath).TranscribeTrigger != TranscribeTrigger.MiddleMouse) throw new Exception("Legacy loaded trigger changed");
            }
            finally { File.Delete(invalidPath); }
            var oldFolder = JsonSerializer.Deserialize<Settings>("{\"Folder\":\"C:/legacy-gaze\"}")!;
            if (oldFolder.Folder != "C:/legacy-gaze" || !JsonSerializer.Serialize(oldFolder).Contains("Folder")) throw new Exception("Output root Folder compatibility changed");
            if (Path.GetFileName(new Settings().Folder) != "Output") throw new Exception("New default is not Output");
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
            var modeChoices = Controls(form).OfType<ComboBox>().Single(c => c.Items.Contains("Screenshot"));
            if (modeChoices.Items.Count != 4 || modeChoices.Items[3]?.ToString() != "Screenshot") throw new Exception("Capture mode dropdown mapping changed");
            if (!toggle.Checked) throw new Exception("Settings UI lost persisted toggle");
            var compatible = JsonSerializer.Deserialize<Settings>("{\"SplitAt29Mb\":true,\"AutoCopy\":false}")!;
            var roundtrip = JsonSerializer.Serialize(compatible);
            if (!roundtrip.Contains("\"SplitAt29Mb\":true") || compatible.AutoCopy) throw new Exception("Legacy split key or AutoCopy compatibility lost");
            using var clipboardForm = new SettingsForm(compatible, _ => {});
            var clipboardToggle = Controls(clipboardForm).OfType<CheckBox>().SingleOrDefault(c => c.Text == "Claude Desktop Clipboard");
            if (clipboardToggle is not { Checked: true, AccessibleName: "Claude Desktop Clipboard" }) throw new Exception("WinForms Claude Desktop Clipboard label/accessibility/persisted value missing");
            const string help = "Video: 29 MB parts. Screenshot: 20 images / 29 MB per batch.";
            if (!Controls(clipboardForm).OfType<Label>().Any(c => c.Text.Contains(help))) throw new Exception("WinForms clipboard limits helper missing");
            foreach (bool enabled in new[] { false, true })
            {
                Settings? saved = null;
                using var saveForm = new SettingsForm(compatible, value => saved = value);
                saveForm.Show();
                Controls(saveForm).OfType<Button>().Single(c => c.Text == "Saving").PerformClick();
                Controls(saveForm).OfType<CheckBox>().Single(c => c.Text == "Claude Desktop Clipboard").Checked = enabled;
                Controls(saveForm).OfType<Button>().Single(c => c.Text == "Save settings").PerformClick();
                if (saved == null || saved.SplitAt29Mb != enabled || saved.AutoCopy) throw new Exception("Saving clipboard setting changed legacy key or independent AutoCopy");
                compatible = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(saved))!;
            }
            var xaml = System.Xml.Linq.XDocument.Load("src/Gaze.Panel/PanelWindow.xaml");
            var splitUi = xaml.Descendants().Single(e => (string?)e.Attribute(System.Xml.Linq.XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "SplitAt29Mb");
            if ((string?)splitUi.Attribute("AutomationProperties.Name") != "Claude Desktop Clipboard" || !xaml.Descendants().Any(e => (string?)e.Attribute("Text") == "Claude Desktop Clipboard") || !xaml.Descendants().Any(e => (string?)e.Attribute("Text") == help)) throw new Exception("WPF clipboard setting label, accessible name or helper missing");
            Console.WriteLine("PASS actual Settings migration/persistence, panel DTO and settings toggle"); return 0;
        } catch(Exception e) { Console.WriteLine("FAIL " + e.Message); return 1; }
    }
}
