using Gaze.Core;
using System.Drawing;
internal static class TranscribeTriggerChecks
{
    public static void Run(Action<string, Action> test)
    {
        void Check(bool value) { if (!value) throw new Exception("Trigger behavior missing"); }
        test("PrintScreen exclusive down move up uses existing starting-target gesture", () => {
            var p = new TranscribeTriggerPolicy(TranscribeTrigger.PrintScreen);
            Check(!p.Mouse(0x207).Consume && p.Mouse(0x207).Message == 0);
            Check(p.Key(44, true).Message == 0x207);
            var g = new TranscribeGesture(new(-50, -50), new(4,4), 42, "start", new(-100,-100,100,100));
            Check(p.Key(44,true).Consume && p.Key(44,true).Message == 0);
            Check(p.Mouse(0x200).Message == 0x200);
            g.Preview(new(50,50));
            Check(p.Key(44,false,true).Message == 0x208);
            var s = g.Complete(new(50,50));
            Check(s is { IsWindow:false, Title:"start" } && s.Area == new Rectangle(-50,-50,50,50));
            Check(!p.Key(44,false).Consume);
            Check(p.Key(44,true).Message == 0x207);
            var tap = new TranscribeGesture(new(-50,-50),new(4,4),77,"tap",new(-100,-100,100,100));
            Check(p.Key(44,false).Message == 0x208 && tap.Complete(new(-49,-49)) is { IsWindow:true, Window:77 });
        });
        test("Default MMB and modified PrintScreen pass through exclusively", () => {
            var p = new TranscribeTriggerPolicy();
            Check(p.Mouse(0x207) == new TranscribeRoute(true,0x207));
            Check(!p.Key(44,true).Consume);
            Check(p.Mouse(0x208).Consume);
            p = new(TranscribeTrigger.PrintScreen);
            Check(!p.Key(44,true,true).Consume && !p.Key(44,true).Consume && !p.Key(44,false).Consume);
            Check(!p.Key(44,false).Consume);
        });
        test("Escape cancels once, owns paired release and suppresses stale completion", () => {
            var p = new TranscribeTriggerPolicy(TranscribeTrigger.PrintScreen);
            p.Key(44,true);
            Check(p.Key(27,true).Message == -1);
            Check(p.Key(27,true).Consume && p.Key(27,false).Consume);
            Check(p.Key(44,false).Consume && !p.Key(44,false).Consume);
            p.Key(44,true); p.Cancel();
            Check(p.Key(44,false).Message == 0);
        });
        test("Held startup and stopped policy cannot start captures; owned release drains once", () => {
            var p = new TranscribeTriggerPolicy(TranscribeTrigger.PrintScreen,true);
            Check(!p.Key(44,true).Consume && !p.Key(44,false).Consume);
            p.Key(44,true); p.Stop(); Check(p.NeedsRelease);
            Check(p.Key(44,true).Consume && p.Key(44,false).Consume && !p.NeedsRelease);
            Check(!p.Key(44,true).Consume && !p.Mouse(0x207).Consume);
        });
    }
}
