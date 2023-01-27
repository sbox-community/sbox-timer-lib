# sbox-timer-lib
Timer library like Garry's Mod for s&amp;box

```csharp
void Timer.Simple( float delay, Action func, bool threaded = false )

void Timer.Create( string id, float delay, int repetitions, Action func, bool threaded = false )

bool Timer.Exists( string id )

bool Timer.Remove( string id )

float Timer.TimeLeft( string id )

int Timer.RepsLeft( string id )

bool Timer.Adjust( string id, float? delay = null, int? repetitions = null, Action? func = null )

bool Timer.Pause( string id )

bool Timer.UnPause( string id )

bool Timer.Toggle( string id )

bool Timer.Start( string id )

bool Timer.Stop( string id )

Dictionary<string, Timer> Timer.All()

void Timer.PrintAll()

```
