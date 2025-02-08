using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace TopGil;

internal class BackgroundTimer : IDisposable
{
	private readonly Timer timer;

    public bool IsRunning { get { return timer.Enabled; } }

    public BackgroundTimer(double intervalMilliseconds, ElapsedEventHandler onElapsed, bool autoStart = false)
	{
		timer = new Timer(intervalMilliseconds);
		timer.Elapsed += onElapsed;
		timer.AutoReset = true;
		timer.Enabled = autoStart;
	}

	public void ChangeInterval(double interval)
	{
		bool wasRunning = timer.Enabled;
		timer.Stop();
		timer.Interval = interval;
		if (wasRunning)
		{
			timer.Start();
		}
	}

	public void Start()
	{
		timer.Start();
	}

	public void Stop()
	{
		timer.Stop();
	}

    public void Dispose()
	{
		timer?.Dispose();
	}
}
