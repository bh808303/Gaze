using System.Text.Json;
using Gaze.Core;
internal static class OptionalMetadataChecks
{
    internal static void Run(Action<string, Action> test)
    {
        test("Monitor mapping removal excludes input and emits only a neutral leave", () =>
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root != null && !File.Exists(Path.Combine(root.FullName,"src/Gaze.App/Capture.cs"))) root=root.Parent;
            var capture=File.ReadAllText(Path.Combine(root!.FullName,"src/Gaze.App/Capture.cs"));
            if (capture.Contains("mode != CaptureMode.Window && mappings.Count > 0")) throw new Exception("RecordMouse falls back to desktopBounds after last mapping removal");
            var method=typeof(Gaze.App.TimelineBoundary).GetMethod("TryMapMonitorPoint",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            if(method==null || !capture.Contains("TimelineBoundary.TryMapMonitorPoint")) throw new Exception("Shared production mapping path missing");
            var point=new System.Drawing.Point(25,25);
            var areas=new[]{new System.Drawing.Rectangle(0,0,100,100)};
            var mappings=new List<(System.Drawing.Rectangle Source,System.Drawing.Rectangle Destination)>{(areas[0],new(10,20,50,50))};
            bool Map(out int x,out int y) {
                object?[] args={point,areas,mappings,0,0}; bool included=(bool)method.Invoke(null,args)!;
                x=(int)args[3]!;y=(int)args[4]!;return included;
            }
            var sampler=new PointerSampler();var time=DateTimeOffset.UtcNow;
            if(!Map(out int x,out int y)||x!=22||y!=32) throw new Exception("Valid mapping lost");
            sampler.Sample(true,point.X,point.Y,x,y,time,0,false);mappings.Clear();
            foreach(var kind in new[]{"pointer","click","button-release","wheel"}) {
                bool included=Map(out x,out y);if(included) throw new Exception(kind+" accepted without source");
                var events=sampler.Sample(included,point.X,point.Y,x,y,time,100,false,kind=="pointer");
                if(kind=="pointer") {
                    var leave=events.Single();
                    if(leave.Boundary!="leave"||leave.ScreenX!=null||leave.ScreenY!=null||leave.X!=null||leave.Y!=null||leave.Process!=null) throw new Exception("Leave leaked data");
                } else if(events.Count!=0) throw new Exception("Excluded discrete input emitted event");
            }
            mappings.Add((areas[0],System.Drawing.Rectangle.Empty));if(Map(out x,out y)) throw new Exception("Invalid destination accepted");
            mappings.Clear();mappings.Add((new(200,0,100,100),areas[0]));if(Map(out x,out y)) throw new Exception("Unmapped point accepted");
            mappings.Clear();mappings.Add((areas[0],areas[0]));areas[0]=new(200,0,100,100);if(Map(out x,out y)) throw new Exception("Outside armed area accepted");
        });
        test("Split parts carry prior geometry and pointer boundary without foreign coordinates", () =>
        {
            var time = DateTimeOffset.Parse("2026-10-13T12:00:00Z");
            var timeline = new RecordingTimeline(5,new() {RecordTimelineMetadata=true}); timeline.Frame(time);
            timeline.Geometry(new(0,0,10,10,0,0,10,10,10,10,"captured"),time);
            timeline.Metadata(new(0,0,time,"pointer-boundary",Boundary:"leave"));
            timeline.Frame(time.AddSeconds(1));
            var part = timeline.Snapshot(2).Slice(1,1);
            if (!part.Events.Any(e=>e.Kind=="geometry" && e.CarryOver)) throw new Exception("Split mapping context missing");
            var boundary=part.Events.SingleOrDefault(e=>e.Kind=="pointer-boundary");
            if (boundary is not {CarryOver:true,Boundary:"leave",ScreenX:null,X:null}) throw new Exception("Split pointer context missing");
        });
        test("Mouse queue bounds pointer backlog and declares discrete overflow without blocking a consumer", () =>
        {
            var queue = new Gaze.App.MouseQueue(); var time = DateTimeOffset.UtcNow;
            for (int i=0; i<300; i++) queue.Enqueue(new("pointer",null,new(i,0),time,i*100));
            if (queue.Lost != 44) throw new Exception("Pointer backlog was not bounded or declared");
            var values = new List<Gaze.App.RawMouse>(); queue.Drain(values.Add);
            if (values.Count != 256) throw new Exception("Pointer queue cap failed");
            for (int i=0; i<4100; i++) queue.Enqueue(new("wheel",null,new(i,0),time.AddTicks(i),i,Delta:i));
            if (queue.Lost != 48) throw new Exception("Discrete overflow not counted");
            values.Clear(); queue.Drain(values.Add);
            if (!values.Select(e => e.Delta).SequenceEqual(Enumerable.Range(0,4096))) throw new Exception("Discrete order or raw delta lost");
            using var entered = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
            queue.Enqueue(new("click","left",new(0,0),time,0));
            var drain = Task.Run(() => queue.Drain(_ => { entered.Set(); release.Wait(); }));
            try {
                if (!entered.Wait(5000)) throw new Exception("Consumer did not enter");
                var producer = Task.Run(() => queue.Enqueue(new("button-release","left",new(1,0),time,1)));
                if (!producer.Wait(1000)) throw new Exception("Hook producer blocked behind drawing");
            } finally { release.Set(); drain.GetAwaiter().GetResult(); }
            values.Clear(); queue.Drain(values.Add);
            if (values.Single().Kind != "button-release" || values.Single().Time != time) throw new Exception("Release lost");
        });
        test("Optional events are ignored when disabled or paused, retain raw values and resume clocks, and deduplicate geometry", () =>
        {
            var time = DateTimeOffset.Parse("2026-10-13T12:00:00Z");
            var disabled = new RecordingTimeline(5); disabled.Frame(time);
            disabled.Metadata(new(0,0,time,"wheel", Delta: -35, Axis: "horizontal"));
            if (disabled.Snapshot(1).Events.Any(e => e.Kind == "wheel")) throw new Exception("Disabled collection recorded wheel");
            var timeline = new RecordingTimeline(5, new() { RecordTimelineMetadata = true }); timeline.Frame(time);
            var geometry = new CaptureGeometry(-10,20,100,200,0,10,50,100,100,120,"captured");
            timeline.Geometry(geometry,time); timeline.Geometry(geometry,time.AddMilliseconds(1));
            timeline.Metadata(new(0,0,time.AddMilliseconds(2),"button-release", Button:"left", ScreenX:-1, ScreenY:30, X:4,Y:5));
            timeline.Pause(time.AddMilliseconds(3));
            timeline.Metadata(new(0,0,time.AddMilliseconds(4),"wheel",Delta:999));
            timeline.Resume(time.AddSeconds(1));
            timeline.Metadata(new(0,0,time.AddSeconds(1).AddMilliseconds(1),"wheel",Delta:-35, Axis:"horizontal",ScreenX:-1,ScreenY:30,X:4,Y:5));
            timeline.Frame(time.AddSeconds(2));
            timeline.Geometry(geometry with { SourceWidth=101 },time.AddSeconds(2));
            timeline.Status("mouse","loss",2);
            var part = timeline.Snapshot(2).Slice(1,1);
            var wheel = part.Events.Single(e => e.Kind == "wheel");
            if (wheel.Frame != 0 || wheel.RecordingFrame != 1 || wheel.ClockTime != time.AddSeconds(1).AddMilliseconds(1) || wheel.Delta != -35) throw new Exception("Resume raw event association changed");
            if (timeline.Snapshot(2).Events.Count(e => e.Kind == "geometry") != 2) throw new Exception("Geometry not deduplicated");
            if (part.CollectionStatus!.Single().Lost != 2) throw new Exception("Known loss missing");
        });
        test("Dense frame events are batched without loss inside timed-text limits", () =>
        {
            var time = DateTimeOffset.Parse("2026-10-13T12:00:00Z");
            var timeline = new RecordingTimeline(1); timeline.Frame(time);
            for (int i=0; i<1000; i++) timeline.Click("left", i, i, i, i, time.AddTicks(i));
            var srt = Gaze.App.RecordingMetadata.Subtitles(timeline.Snapshot(1).Slice(0,1));
            var cues = srt.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
            int count = 0;
            foreach (var cue in cues)
            {
                var payload = cue[(cue.IndexOf("gaze-base64-v1:") + 15)..];
                if (System.Text.Encoding.UTF8.GetByteCount(payload) > 60000) throw new Exception("Oversized cue");
                using var data = JsonDocument.Parse(Convert.FromBase64String(payload));
                count += data.RootElement.GetProperty("events").GetArrayLength();
            }
            if (count != 1001 || cues.Length < 2) throw new Exception("Dense events lost or not batched");
        });
        test("Pointer collector caps unchanged/moving samples and emits private boundary transitions", () =>
        {
            var sampler = new PointerSampler();
            var time = DateTimeOffset.Parse("2026-10-13T12:00:00Z");
            var first = sampler.Sample(true, 10, 20, 1, 2, time, 0, false);
            if (!first.Select(e => e.Kind).SequenceEqual(new[] { "pointer-boundary", "pointer" })) throw new Exception("Entry sample missing");
            if (sampler.Sample(true, 11, 20, 2, 2, time, 99, false).Count != 0) throw new Exception("Exceeded 10 Hz");
            if (sampler.Sample(true, 11, 20, 2, 2, time, 100, false).Count != 1) throw new Exception("Changed position missing");
            if (sampler.Sample(true, 11, 20, 2, 2, time, 200, false).Count != 0) throw new Exception("Redundant sample");
            if (sampler.Sample(true, 12, 20, 3, 2, time, 133, true).Count != 0) throw new Exception("Exceeded 30 Hz");
            if (sampler.Sample(true, 12, 20, 3, 2, time, 134, true).Count != 1) throw new Exception("Held-button sample missing");
            var outside = sampler.Sample(false, 9999, 9999, 9999, 9999, time, 135, true).Single();
            if (outside.Boundary != "leave" || outside.ScreenX != null || outside.X != null || outside.Process != null) throw new Exception("Outside data leaked");
            if (sampler.Sample(false, 123, 456, 1, 2, time, 300, false).Count != 0) throw new Exception("Repeated outside boundary");
            sampler.Reset();
            if (sampler.Sample(true, 12, 20, 3, 2, time, 301, false).Count != 2) throw new Exception("Resume rate/boundary state did not reset");
        });
        test("Optional recordings retain actual sample gaps and collection context across parts", () =>
        {
            var timeline = new RecordingTimeline(5, new() { RecordTimelineMetadata = true });
            var time = DateTimeOffset.Parse("2026-10-13T12:00:00+02:00");
            timeline.Frame(time); timeline.Frame(time.AddMilliseconds(750));
            var part = timeline.Snapshot(2).Slice(1, 1);
            using var data = JsonDocument.Parse(JsonSerializer.Serialize(part));
            if (!data.RootElement.TryGetProperty("Collection", out var options) || !options.GetProperty("RecordTimelineMetadata").GetBoolean()) throw new Exception("Collection context missing");
            if (!data.RootElement.TryGetProperty("SampleOffsetsTicks", out var times) || times.GetArrayLength() != 1 || times[0].GetInt64() != 0) throw new Exception("Actual part sample timing missing");
            var whole = JsonSerializer.Serialize(timeline.Snapshot(2).Slice(0,2));
            if (!whole.Contains("7500000")) throw new Exception("Capture gap was replaced by nominal FPS");
        });
        test("Optional metadata defaults off for old settings and persists when enabled", () =>
        {
            var legacy = JsonSerializer.Deserialize<TimelineCollectionOptions>("{}")!;
            if (legacy.RecordTimelineMetadata || legacy.KeyboardTimeline) throw new Exception("Legacy opt-ins must default off");
            legacy.RecordTimelineMetadata = true;
            var saved = JsonSerializer.Deserialize<TimelineCollectionOptions>(JsonSerializer.Serialize(legacy))!;
            if (!saved.RecordTimelineMetadata || saved.KeyboardTimeline) throw new Exception("Metadata toggle changed another opt-in");
        });
    }
}
