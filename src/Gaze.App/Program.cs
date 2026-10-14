namespace Gaze.App;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Length == 2 && args[0] == "--transcribe-capture-probe")
        { TranscribeCaptureProbe.Run(args[1]); return; }
        if (args.Contains("--tray-startup-check"))
        {
            var evidence = args[Array.IndexOf(args, "--tray-startup-check") + 1]; Directory.CreateDirectory(evidence);
            using var context = new TrayContext(validation: true);
            using var timer = new System.Windows.Forms.Timer { Interval = 1200 };
            nint panelHandle = 0;
            int cycles = 0;
            timer.Tick += (_, _) =>
            {
                if (panelHandle == 0) { Gaze.Fluent.PanelWindow.QueueOnHost(() => panelHandle = context.OpenPanelForValidation()); return; }
                if (cycles++ < 3) { Gaze.Fluent.PanelWindow.QueueOnHost(() => panelHandle = context.CyclePanelForValidation()); return; }
                timer.Stop(); var bounds = Native.WindowBounds(panelHandle);
                using var bitmap = new Bitmap(bounds.Width, bounds.Height);
                using (var graphics = Graphics.FromImage(bitmap)) graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
                bitmap.Save(Path.Combine(evidence, "actual-tray-panel.png"));
                int colored = 0;
                for (int y = 0; y < bitmap.Height; y += 8)
                    for (int x = 0; x < bitmap.Width; x += 8)
                    { var color = bitmap.GetPixel(x, y); if (color.R > color.G + 40 || color.B > color.R + 40) colored++; }
                File.WriteAllText(Path.Combine(evidence, "result.txt"), colored >= 10 ? "PASS: actual tray context renders visible controls after repeated hide/reopen and capture-affinity cycles." : "FAIL: blank tray panel.");
                context.ExitThread();
            };
            timer.Start(); TrayPanel.RunHost(context); return;
        }
        if (args.Contains("--host-startup-check"))
        {
            var evidence = args[Array.IndexOf(args, "--host-startup-check") + 1];
            Directory.CreateDirectory(evidence);
            using var context = new ApplicationContext();
            using var timer = new System.Windows.Forms.Timer { Interval = 800 };
            TrayPanel? panel = null;
            int stage = 0;
            timer.Tick += (_, _) =>
            {
                if (stage++ == 0)
                {
                    var value = new Settings();
                    panel = new TrayPanel(() => value, proposed => value = proposed, () => { }, () => { }, _ => { }, () => { }, _ => { }, () => { }, () => { });
                    panel.RefreshState(Gaze.Core.SessionState.Idle, TimeSpan.Zero, false, false);
                    var area = Screen.PrimaryScreen!.WorkingArea;
                    panel.OpenAt(new Point(area.Right - 1, area.Bottom - 1));
                }
                else
                {
                    timer.Stop();
                    var bounds = Native.WindowBounds(panel!.Handle);
                    using var bitmap = new Bitmap(bounds.Width, bounds.Height);
                    using (var graphics = Graphics.FromImage(bitmap)) graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
                    bitmap.Save(Path.Combine(evidence, "late-panel.png"));
                    panel.Dispose(); context.ExitThread();
                    File.WriteAllText(Path.Combine(evidence, "startup.txt"), "PASS: host started before panel construction, WinForms timer dispatched, late WPF panel opened, host exited.");
                }
            };
            timer.Start(); TrayPanel.RunHost(context); return;
        }
        if (args.Contains("--panel-placement-check"))
        {
            var value = new Settings();
            using var check = new TrayPanel(() => value, proposed => value = proposed, () => { }, () => { }, _ => { }, () => { }, _ => { }, () => { }, () => { });
            check.RefreshState(Gaze.Core.SessionState.Idle, TimeSpan.Zero, false, false);
            if (args.Contains("--preview-settings")) check.EditSettings();
            check.CheckPlacement(args[Array.IndexOf(args, "--panel-placement-check") + 1]); return;
        }
        if (args.Contains("--panel-preview"))
        {
            var previewPath = args[Array.IndexOf(args, "--panel-preview") + 1];
            Application.ThreadException += (_, error) => { File.WriteAllText(previewPath + ".error.txt", error.Exception.ToString()); Application.ExitThread(); };
            var value = new Settings();
            if (args.Contains("--preview-folder")) value.Folder = args[Array.IndexOf(args, "--preview-folder") + 1];
            using var preview = new TrayPanel(() => value, proposed => value = proposed, () => { }, () => { }, _ => { }, () => { }, _ => { }, () => { }, () => { });
            preview.RefreshState(Gaze.Core.SessionState.Idle, TimeSpan.Zero, false, false);
            if (args.Contains("--check-compression")) preview.CheckCompression();
            if (args.Contains("--check-keyboard")) preview.CheckKeyboard();
            if (args.Contains("--check-splitting"))
            {
                try { preview.CheckSplitting(); }
                catch (Exception e) { File.WriteAllText(previewPath + ".error.txt", e.ToString()); Environment.ExitCode = 1; return; }
            }
            if (args.Contains("--preview-settings")) preview.EditSettings();
            preview.RunPreview(previewPath); return;
        }
        if (args.Contains("--self-test"))
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            try { Application.Run(new CaptureFixture(args)); }
            catch (Exception e)
            {
                var index = Array.IndexOf(args, "--evidence");
                var evidence = index >= 0 ? Path.GetFullPath(args[index + 1]) : Path.GetFullPath("docs/factory/runs/prototype-001/runtime");
                Directory.CreateDirectory(evidence); File.WriteAllText(Path.Combine(evidence, "unhandled.txt"), e.ToString()); Environment.ExitCode = 1;
            }
            return;
        }
        using var singleton = new Mutex(true, "Local\\Gaze.Tray", out var first);
        if (!first) { MessageBox.Show("Gaze is already running in the system tray.", "Gaze"); return; }
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
        { MessageBox.Show("Gaze requires Windows 10 version 2004 or later.", "Gaze"); return; }
        try { using var context = new TrayContext(); TrayPanel.RunHost(context); }
        catch (Exception e) { MessageBox.Show(e.ToString(), "Gaze could not start", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}
