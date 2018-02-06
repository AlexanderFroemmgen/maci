using System;

namespace Backend.WorkerHost
{
    public class WorkerHostException : Exception
    {
        public WorkerHostException(Exception e)
            : base($"{e.GetType().Name}: {e.Message}", e)
        {
        }
    }
}