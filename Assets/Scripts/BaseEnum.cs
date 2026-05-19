public enum OptimizationType
{
    None,                // Instantiate / Destroy
    ObjectPool,          // GameObject object pool
    ECS,                 // ECS main-thread systems
    ECSWithJobs,         // ECS + Jobs
    ECSWithJobsAndBurst, // ECS + Jobs + Burst
    ECSPool              // ECS prewarmed entity pool + Jobs + Burst
}
