using System.Drawing;
using System.Windows.Forms;

namespace GameNetManager.Client;

public sealed class AgentLockController : IDisposable
{
    private readonly object sync = new();
    private GameNetLockForm? form;
    private Thread? uiThread;
    private bool desiredLocked;
    private bool disposed;

    public bool IsLocked
    {
        get
        {
            lock (sync)
                return desiredLocked;
        }
    }

    public Task LockAsync()
    {
        lock (sync)
        {
            ThrowIfDisposed();
            if (desiredLocked)
                return Task.CompletedTask;

            desiredLocked = true;
            if (uiThread is not null)
                return Task.CompletedTask;

            uiThread = new Thread(RunLockScreen)
            {
                IsBackground = true,
                Name = "GameNet-LockScreen"
            };
            uiThread.SetApartmentState(ApartmentState.STA);
            uiThread.Start();
        }

        return Task.CompletedTask;
    }

    public Task UnlockAsync()
    {
        GameNetLockForm? currentForm;
        lock (sync)
        {
            ThrowIfDisposed();
            desiredLocked = false;
            currentForm = form;
        }

        if (currentForm is not null && !currentForm.IsDisposed && currentForm.IsHandleCreated)
        {
            try
            {
                currentForm.BeginInvoke(currentForm.AllowClose);
            }
            catch (InvalidOperationException)
            {
                // The form is already shutting down.
            }
        }

        return Task.CompletedTask;
    }

    private void RunLockScreen()
    {
        GameNetLockForm? created = null;
        try
        {
            lock (sync)
            {
                if (!desiredLocked || disposed)
                {
                    uiThread = null;
                    return;
                }

                created = new GameNetLockForm();
                form = created;
            }

            if (!IsLocked)
                created.AllowClose();
            else
                Application.Run(created);
        }
        catch
        {
            // Never terminate the Agent because the local lock UI could not start.
            // The Server-side lock state remains authoritative.
        }
        finally
        {
            lock (sync)
            {
                form = null;
                uiThread = null;
            }
        }
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
            throw new ObjectDisposedException(nameof(AgentLockController));
    }

    public void Dispose()
    {
        GameNetLockForm? currentForm;
        lock (sync)
        {
            if (disposed)
                return;

            disposed = true;
            desiredLocked = false;
            currentForm = form;
        }

        if (currentForm is not null && !currentForm.IsDisposed && currentForm.IsHandleCreated)
        {
            try
            {
                currentForm.BeginInvoke(currentForm.AllowClose);
            }
            catch (InvalidOperationException)
            {
                // Form is already closed.
            }
        }
    }

    private sealed class GameNetLockForm : Form
    {
        private bool allowClose;

        public GameNetLockForm()
        {
            Text = "GameNet";
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            TopMost = true;
            ShowInTaskbar = false;
            KeyPreview = true;
            BackColor = Color.Black;
            ForeColor = Color.White;
            StartPosition = FormStartPosition.Manual;
            Bounds = Screen.PrimaryScreen?.Bounds ?? Screen.AllScreens[0].Bounds;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.Black,
                Padding = new Padding(40),
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 35));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 35));

            var title = new Label
            {
                Dock = DockStyle.Fill,
                Text = "این دستگاه موقتاً قفل است",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 30, FontStyle.Bold),
                ForeColor = Color.White,
            };

            var detail = new Label
            {
                Dock = DockStyle.Fill,
                Text = "برای ادامه، از اپراتور درخواست آزادسازی دستگاه کنید.",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 18, FontStyle.Regular),
                ForeColor = Color.Gainsboro,
            };

            var footer = new Label
            {
                Dock = DockStyle.Fill,
                Text = "GameNet Manager",
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 14, FontStyle.Regular),
                ForeColor = Color.Gray,
            };

            layout.Controls.Add(title, 0, 0);
            layout.Controls.Add(detail, 0, 1);
            layout.Controls.Add(footer, 0, 2);
            Controls.Add(layout);

            KeyDown += (_, e) =>
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
            };

            FormClosing += (_, e) =>
            {
                if (!allowClose)
                    e.Cancel = true;
            };
        }

        public void AllowClose()
        {
            if (IsDisposed)
                return;

            allowClose = true;
            Close();
        }
    }
}
