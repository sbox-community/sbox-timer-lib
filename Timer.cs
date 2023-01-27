
using Sandbox;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

public partial class Timer : IDisposable
{
	private static Dictionary<string, Timer> activeTimers = new Dictionary<string, Timer>();

	public CancellationTokenSource? CTS;
	private TaskCompletionSource<bool>? TCS;
	public Process status;
	private float delay;
	private string id;
	private int repetitions;
	private Action func;
	private float nextExecution;
	private bool isDisposed = false;
	private bool duplicated = false;

	public enum Process
	{
		Continue,
		Pause,
		UnPause,
		Stop,
		Start,
	}

	public Timer( float delay, string id, int repetitions, Action func, bool threaded = false )
	{
		Remove( id );
		CTS = new();
		TCS = new();
		status = Process.Continue;
		this.delay = delay;
		this.id = id;
		this.repetitions = repetitions;
		this.func = func;

		if ( threaded )
			GameTask.RunInThreadAsync( Loop );
		else
			Loop();
	}

	private async void Loop()
	{
		lock ( activeTimers )
		{
			if ( Exists( id ) )
				activeTimers[id].duplicated = true;

			activeTimers[id] = this;
		}

		var _repetitions = repetitions;
		var pausedDelay = 0f;

		while ( true )
		{
			if ( isDisposed || duplicated )
				return;

			nextExecution = Time.Now;

			try
			{
				if ( status == Process.Pause || status == Process.Stop )
					await TCS.Task.WaitAsync( CTS.Token );
				else
					await Task.Delay( TimeSpan.FromSeconds( pausedDelay != 0f ? pausedDelay : delay ), CTS.Token );
			}
			catch ( TaskCanceledException )
			{
				switch ( status )
				{
					case (Process.Continue):
						Dispose();
						return;
					case (Process.Pause):
						newCTS();
						pausedDelay = MathF.Max(delay - (Time.Now - nextExecution), 0f);
						continue;
					case (Process.UnPause):
						newCTS();
						status = Process.Continue;
						continue;
					case (Process.Stop):
						newCTS();
						repetitions = _repetitions;
						pausedDelay = 0f;
						continue;
					case (Process.Start):
						newCTS();
						repetitions = _repetitions;
						pausedDelay = 0f;
						status = Process.Continue;
						continue;
					default:
						Dispose();
						return;
				}
			}

			if( pausedDelay != 0f )
				pausedDelay = 0f;

			if ( !Game.InGame )
			{
				Dispose();
				return;
			}

			func();

			if ( repetitions != -1 && repetitions-- == 0 )
			{
				Dispose();
				return;
			}
		}
	}

	private void newCTS()
	{
		CTS?.Dispose();
		CTS = new();
	}

	public void Dispose()
	{
		if (!isDisposed && !duplicated )
			activeTimers.Remove( id );

		Dispose( true );
		GC.SuppressFinalize( this );
	}

	private void Dispose( bool isdisposing )
	{
		if ( !isDisposed )
		{
			if ( isdisposing )
			{
				CTS?.Cancel();
				CTS?.Dispose();
				CTS = null;
				
				_ = TCS?.TrySetCanceled();
				TCS = null;

				id = null;
				func = null;
			}
			isDisposed = true;
		}
	}

	~Timer()
	{
		Dispose( true );
	}

	public static bool Remove( string id )
	{
		lock ( activeTimers )
		{
			if ( Exists( id ) )
			{
				activeTimers[id].Dispose();
				return true;
			}
			return false;
		}
	}

	public static void Simple( float delay, Action func, bool threaded = false ) =>	new Timer( delay, Guid.NewGuid().ToString(), 0, func, threaded );

	public static void Create( string id, float delay, int repetitions, Action func, bool threaded = false ) => new Timer( delay, id, repetitions == 0 ? -1 : repetitions - 1, func, threaded );

	public static bool Exists( string id )
	{
		lock ( activeTimers )
		{
			return activeTimers.ContainsKey( id );
		}
	}

	public static float TimeLeft( string id )
	{
		lock ( activeTimers )
		{
			return Exists( id ) ? (activeTimers[id].nextExecution + activeTimers[id].delay) - Time.Now : 0f;
		}
	}

	public static bool Adjust( string id, float? delay = null, int? repetitions = null, Action? func = null )
	{
		lock ( activeTimers )
		{
			if ( Exists( id ) )
			{
				if( delay is not null )
					activeTimers[id].delay = delay.Value;

				if ( repetitions is not null )
					activeTimers[id].repetitions = repetitions.Value == 0 ? -1 : repetitions.Value;

				if ( func is not null )
					activeTimers[id].func = func;

				return true;
			}
			return false;
		}
	}

	public static int RepsLeft( string id )
	{
		lock ( activeTimers )
		{
			return Exists( id ) ? activeTimers[id].repetitions : -1;
		}
	}

	public static bool Pause( string id ) 
	{
		lock ( activeTimers )
		{
			if ( Exists( id ) )
			{
				activeTimers[id].status = Process.Pause;
				activeTimers[id].CTS.Cancel();

				return true;
			}
			return false;
		}
	}

	public static bool UnPause( string id )
	{
		lock ( activeTimers )
		{
			if ( Exists( id ) )
			{
				activeTimers[id].status = Process.UnPause;
				activeTimers[id].CTS.Cancel();

				return true;
			}
			return false;
		}
	}

	public static bool Toggle( string id ) {
		lock ( activeTimers )
		{
			if ( Exists( id ) )
			{
				var result = activeTimers[id].status;
				if ( result == Process.Continue || result == Process.Pause )
					activeTimers[id].status = activeTimers[id].status == Process.Continue ? Process.Pause : Process.UnPause;
				activeTimers[id].CTS.Cancel();
				return activeTimers[id].status == Process.Pause;
			}
			return false;
		}
	}

	public static bool Start( string id ) {

		lock ( activeTimers )
		{
			if ( Exists( id ) )
			{
				activeTimers[id].status = Process.Start;
				activeTimers[id].CTS.Cancel();

				return true;
			}
			return false;
		}
	}

	public static bool Stop( string id ) {
		lock ( activeTimers )
		{
			if ( Exists( id ) )
			{
				activeTimers[id].status = Process.Stop;
				activeTimers[id].CTS.Cancel();

				return true;
			}
			return false;
		}
	}

	public static Dictionary<string, Timer> All() => activeTimers;

	public static void PrintAll()
	{
		foreach( var timer in activeTimers )
			Log.Info($"{timer.Key}  =>  delay: {timer.Value.delay}, repetitions: {timer.Value.repetitions}, status: {timer.Value.status}" );
	}
}
