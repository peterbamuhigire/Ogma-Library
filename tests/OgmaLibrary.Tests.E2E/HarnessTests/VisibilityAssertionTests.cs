using System.Drawing;
using System.Windows.Forms;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using OgmaLibrary.Tests.E2E.Harness;

namespace OgmaLibrary.Tests.E2E.HarnessTests;

/// <summary>
/// T01.6 acceptance: the visibility helper fails on a covered element, a clipped element and an
/// unpainted panel, and passes on a plain visible control. The synthetic window is a WinForms
/// form (the E2E project references no Avalonia or production assembly); the same checks are
/// proven against the real Avalonia window by G1/G2 and the K10 regression build.
/// </summary>
[Collection(RealWindowTests.Name)]
public sealed class VisibilityAssertionTests
{
    /// <summary>The four synthetic cases produce the expected verdicts.</summary>
    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Journey", "Harness")]
    [Trait("Tag", "Harness")]
    public void AssertVisiblyPainted_DetectsCoveredClippedAndUnpaintedElements()
    {
        using var synthetic = new SyntheticWindow(form =>
        {
            var visible = new System.Windows.Forms.Button { Name = "Visible", Text = "Visible target", Location = new Point(20, 20), Size = new Size(200, 40) };
            var covered = new System.Windows.Forms.Button { Name = "Covered", Text = "Covered target", Location = new Point(20, 110), Size = new Size(200, 40) };
            var overlay = new Panel { Name = "Overlay", Location = new Point(10, 100), Size = new Size(260, 70), BackColor = Color.SteelBlue };
            var clipped = new System.Windows.Forms.Button { Name = "Clipped", Text = "Clipped target", Location = new Point(480, 220), Size = new Size(200, 40) };
            var blank = new Panel { Name = "Blank", Location = new Point(300, 20), Size = new Size(150, 100), BackColor = form.BackColor };
            form.Controls.AddRange(new Control[] { visible, covered, overlay, clipped, blank });
            overlay.BringToFront();
        });
        using var automation = new UIA3Automation();
        AutomationElement root = automation.FromHandle(synthetic.Handle);
        var window = new UiWindow(automation, root, synthetic.Handle);

        VisibilityReport visible = Visibility.Measure(window, Uia.WaitFor(root, "Visible"));
        VisibilityReport covered = Visibility.Measure(window, Uia.WaitFor(root, "Covered"));
        VisibilityReport clipped = Visibility.Measure(window, Uia.WaitFor(root, "Clipped"));
        VisibilityReport blank = Visibility.Measure(window, Uia.WaitFor(root, "Blank"));

        Assert.True(visible.Passed, visible.ToString());
        Assert.False(covered.Passed, covered.ToString());
        Assert.True(covered.ForeignHits > 0, "The overlay was not detected by hit testing: " + covered);
        Assert.False(clipped.Passed, clipped.ToString());
        Assert.False(clipped.InsideClient, clipped.ToString());
        Assert.False(blank.Passed, blank.ToString());
        Assert.True(blank.PaintedFraction < Visibility.DefaultMinimumPainted, blank.ToString());
        Assert.Throws<Xunit.Sdk.TrueException>(() => Visibility.AssertVisiblyPainted(window, Uia.WaitFor(root, "Covered"), "Covered"));
    }

    /// <summary>The record-dump detector used by the UIA audit (T01.7).</summary>
    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Journey", "Harness")]
    [Trait("Tag", "Harness")]
    public void RecordDumpDetector_FlagsRecordToStringNames()
    {
        Assert.True(UiaAudit.IsRecordDump("BookSummaryProjection { BookId = 01M3, Title = A }"));
        Assert.True(UiaAudit.IsRecordDump("SearchResultItem { BookId = x }"));
        Assert.False(UiaAudit.IsRecordDump("Algorithms Explained"));
        Assert.False(UiaAudit.IsRecordDump("{ not a record"));
    }

    /// <summary>T01.12: sizes below the shell's minimum width are rejected by the wrapper; the parser is strict.</summary>
    [Fact]
    [Trait("Category", "E2E")]
    [Trait("Journey", "Harness")]
    [Trait("Tag", "Harness")]
    public void WindowSize_ParsesStrictly()
    {
        Assert.Equal(new WindowSize(1280, 800), WindowSize.Parse("1280x800"));
        Assert.Throws<FormatException>(() => WindowSize.Parse("1280*800"));
    }

    private sealed class SyntheticWindow : IDisposable
    {
        private readonly Thread _thread;
        private Form? _form;

        public SyntheticWindow(Action<Form> build)
        {
            using var ready = new ManualResetEventSlim();
            _thread = new Thread(() =>
            {
                _form = new Form
                {
                    Text = "Ogma E2E synthetic window",
                    StartPosition = FormStartPosition.Manual,
                    Location = new Point(40, 40),
                    ClientSize = new Size(600, 400),
                    TopMost = true,
                    ShowInTaskbar = false,
                };
                build(_form);
                _form.Shown += (_, _) => ready.Set();
                Application.Run(_form);
            });
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.IsBackground = true;
            _thread.Start();
            Assert.True(ready.Wait(TimeSpan.FromSeconds(15)), "The synthetic window did not show.");
            Thread.Sleep(500);
            Handle = (nint)_form!.Invoke(() => _form.Handle);
        }

        public nint Handle { get; }

        public void Dispose()
        {
            _form?.Invoke(_form.Close);
            _thread.Join(TimeSpan.FromSeconds(5));
            _form?.Dispose();
        }
    }
}
