// BL and DAL keep their state in static singletons, so tests must not run in parallel.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
