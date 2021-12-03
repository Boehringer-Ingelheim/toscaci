using CIService.Contract;
using CIService.Enum;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CIService.Service
{
    public class ExecutionTrackerService
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ExecutionTrackerService));
        private static readonly object executionTrackerLockObject = new object();
        private static List<ExecutionTracking> executionTracker = new List<ExecutionTracking>();

        public static ExecutionTracking GetExecutionTracking(string id)
        {
            return executionTracker.Where(t => t.id == id).FirstOrDefault();
        }

        public static List<ExecutionTracking> HaveExecutionRunning()
        {
            lock (executionTrackerLockObject)
            {
                return executionTracker.Where(t => t.status != ExecutionStatus.Completed && t.status != ExecutionStatus.Failed && t.status != ExecutionStatus.Canceled).ToList();
            }
        }

        public static List<ExecutionTracking> HaveExecutionRunning(String workspaceID)
        {
            lock (executionTrackerLockObject)
            {
                return executionTracker.Where(t => t.status != ExecutionStatus.Completed && t.status != ExecutionStatus.Failed && t.status != ExecutionStatus.Canceled && t.workspaceID.Equals(workspaceID)).ToList();
            }
        }

        public static List<ExecutionTracking> GetExecutionsByWorkspace(String workspaceID)
        {
            lock (executionTrackerLockObject)
            {
                return executionTracker.Where(t => t.workspaceID.Equals(workspaceID)).ToList();
            }
        }
        public static ExecutionTracking CreateExecutionTracking(ExecutionRequest request)
        {
            ExecutionTracking executionTracking = null;
            lock (executionTrackerLockObject)
            {
                executionTracking = new ExecutionTracking(Guid.NewGuid().ToString(),request);
                executionTracking.status = ExecutionStatus.Pending;
                executionTracker.Add(executionTracking);
            }
            return executionTracking;
        }

        public static void SetExecutionTracking(string id, Action<ExecutionTracking> callable)
        {
            lock (executionTrackerLockObject)
            {                
                callable(GetExecutionTracking(id));
            }
        }

        public static void SetExecutionTrackingState(string id, ExecutionStatus status)
        {
            SetExecutionTracking(id, t =>
            {
                log.DebugFormat("Execution Tracking State {0} on {1} ", status, id);
                t.status = status;
            });
        }

        public static void FailExecutionTrackingStatus(string id, Exception ex)
        {
            SetExecutionTracking(id, t =>
            {
                log.DebugFormat("Execution Tracking State Failed on {0} ",  id);
                t.status = ExecutionStatus.Failed;
                t.error = ex;
            });
        }

        public static void CleanExecutionsByWorkspace(string workspaceID)
        {
            lock (executionTrackerLockObject)
            {                
                executionTracker.RemoveAll(t => t.workspaceID.Equals(workspaceID));
            }
        }
    }
}
