namespace OpenGitBase.ComputeAgent;

public sealed class FirecrackerResourceLimits
{
    public int CpuLimit { get; init; } = 1;

    public int MemoryMiB { get; init; } = 2048;

    public int DiskGiB { get; init; } = 20;

    public static FirecrackerResourceLimits FromEnvironment(IReadOnlyDictionary<string, string> environment)
    {
        return new FirecrackerResourceLimits
        {
            CpuLimit = ReadInt(environment, "OGB_CPU_LIMIT", 1),
            MemoryMiB = ReadInt(environment, "OGB_MEMORY_MIB", 2048),
            DiskGiB = ReadInt(environment, "OGB_DISK_GIB", 20),
        };
    }

    public static FirecrackerResourceLimits FromJob(int cpuLimit, int memoryMiB, int diskGiB) =>
        new()
        {
            CpuLimit = cpuLimit > 0 ? cpuLimit : 1,
            MemoryMiB = memoryMiB > 0 ? memoryMiB : 2048,
            DiskGiB = diskGiB > 0 ? diskGiB : 20,
        };

    private static int ReadInt(IReadOnlyDictionary<string, string> environment, string key, int fallback)
    {
        if (environment.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) && parsed > 0)
        {
            return parsed;
        }

        return fallback;
    }
}
