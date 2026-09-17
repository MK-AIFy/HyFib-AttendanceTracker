using Xunit;

// Program.cs assigns a bootstrap logger to the static Serilog.Log.Logger and later
// freezes it during host build (Log.Logger = ...CreateBootstrapLogger(); builder.Host
// .UseSerilog(...)). Every AttendTrackWebApplicationFactory-backed test class invokes
// Program.Main via reflection to build its own host. xUnit runs separate [Collection]
// groups in parallel by default, so two host builds racing on that shared static state
// can have one thread freeze a ReloadableLogger the other already froze, throwing
// "The logger is already frozen." Disabling collection parallelization serializes all
// host builds in this assembly and removes the race.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
