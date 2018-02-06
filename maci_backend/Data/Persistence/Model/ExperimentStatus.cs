namespace Backend.Data.Persistence.Model
{
    // Changing the order of items in this enum will break the calculation of the global experiment state.
    public enum ExperimentStatus
    {
        Pending,
        Finished,
        Running,
        Error,
        Aborted
    }
}