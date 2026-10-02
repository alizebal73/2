using System.Windows.Forms;

namespace GameNetManager.Client;

public sealed class AgentLockScreenController : IDisposable
{
    private readonly object gate = new();
    private Thread? uiThread;
    private LockScreenForm? form;
    private TaskCompletionSource<bool>? shown;
    private bool disposed;
    private bool Headless => !Environment.UserInteractive || string.Equals(Environment.GetEnvironmentVariable("GAMENET_AGENT_HEADLESS"), "1", StringComparison.Ordinal);

    public async Task LockAsync(CancellationToken cancellationToken)
    {
        Task waitForShown;

        lock (gate)
        {
            ThrowIfDisposed();
            if (Headless)
                return;

            if (form is not null && !form.IsDisposed)
                return;

            shown = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            waitForShown = shown.Task;

            uiThread = new Thread(RunMessageLoop)
            {
                IsBackground = true,
                Name = "GameNet Agent Lock Screen"
            };
            uiThread.SetApartmentState(ApartmentState.STA);
            uiThread.Start();
        }

        await waitForShown.WaitAsync(cancellationToken);
    }

    public Task UnlockAsync()
    {
        LockScreenForm? current;

        lock (gate)
        {
            if (disposed)
                return Task.CompletedTask;

            current = form;
            if (current is null || current.IsDisposed)
                return Task.CompletedTask;

            current.AllowClose();
        }

        return Task.CompletedTask;
    }

    private void RunMessageLoop()
    {
        try
        {
            ApplicationConfiguration.Initialize();

            var created = new LockScreenForm();
            lock (gate)
            {
                if (disposed)
                {
                    created.Dispose();
                    return;
                }

                form = created;
                shown?.TrySetResult(true);
            }

            Application.Run(created);
        }
        catch (Exception)
        {
            lock (gate)
            {
                shown?.TrySetException(new InvalidOperationException("نمایش صفحه قفل Agent انجام نشد."));
            }
        }
        finally
        {
            lock (gate)
            {
                form = null;
                uiThread = null;
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(AgentLockScreenController));
    }

    public void Dispose()
    {
        lock (gate)
        {
            disposed = true;
            form?.AllowClose();
            form = null;
        }
    }

    private sealed class LockScreenForm : Form
    {
        private bool allowClosing;

        public LockScreenForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = SystemInformation.VirtualScreen;
            WindowState = FormWindowState.Normal;
            TopMost = true;
            ShowInTaskbar = false;
            ControlBox = false;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = System.Drawing.Color.FromArgb(10, 10, 14);
            KeyPreview = true;
            Cursor = Cursors.No;

            var title = new Label
            {
                Dock = DockStyle.Fill,
                Text = "این دستگاه موقتاً قفل است",
                ForeColor = System.Drawing.Color.White,
                BackColor = System.Drawing.Color.FromArgb(10, 10, 14),
                Font = new System.Drawing.Font("Segoe UI", 28, System.Drawing.FontStyle.Bold),
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter
            };

            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = System.Drawing.Color.FromArgb(10, 10, 14)
            };

            var subtitle = new Label
            {
                AutoSize = true,
                Text = "در انتظار فرمان اپراتور برای بازگشایی",
                ForeColor = System.Drawing.Color.Gainsboro,
                BackColor = System.Drawing.Color.Transparent,
                Font = new System.Drawing.Font("Segoe UI", 13, System.Drawing.FontStyle.Regular)
            };

            panel.Controls.Add(subtitle);
            panel.Controls.Add(title);
            panel.Layout += (_, _) =>
            {
                subtitle.Left = Math.Max(20, (panel.ClientSize.Width - subtitle.Width) / 2);
                subtitle.Top = panel.ClientSize.Height / 2 + 55;
            };

            Controls.Add(panel);
            Shown += (_, _) =>
            {
                Activate();
                BringToFront();
            };
            FormClosing += OnFormClosing;
            KeyDown += (_, e) => e.SuppressKeyPress = true;
        }

        public void AllowClose()
        {
            if (IsDisposed)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(AllowClose);
                return;
            }

            allowClosing = true;
            Close();
        }

        private void OnFormClosing(object? sender, FormClosingEventArgs e)
        {
            if (!allowClosing)
                e.Cancel = true;
        }
    }
}
