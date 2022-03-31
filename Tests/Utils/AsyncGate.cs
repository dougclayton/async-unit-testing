using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Tests.Utils
{
    /// <summary>
    /// This class helps tests ensure that code reaches a given point, without race conditions or sleep statements.
    /// This lets you write code that checks potential sequencing issues via a lockstep execution.
    ///
    /// Create a gate for each "checkpoint" that you want to validate is reached. A gate starts closed, and
    /// the test code can wait for the gate to be reached by the code in question. That code can then wait for the gate
    /// to be opened before it advances. Each "checkpoint" needs its own gate.
    ///
    /// Once a gate has been reached and opened, it can be shut again, but this should never be used as a "second gate"
    /// because that is inherently racy. Instead, each logical point in your code needs a separate gate instance.
    /// A gate should only ever be shut when you know both the code that goes through the gate and the code that opens
    /// it are completed.
    ///
    /// Note that you cannot validate that code has gone "through" the gate with this class, because that would never
    /// capture that your code did anything useful once it went through (just that the Task from this class is completed).
    /// To do that, create a second gate and wait for the code to reach that, or use some other domain-specific means
    /// of validating it (like waiting for an overall async operation to complete).
    /// </summary>
    public class AsyncGate
    {
        // for inspecting this object in a debugger
        private readonly string _name;

        private readonly object _lock = new();
        private TaskCompletionSource _reached;
        private TaskCompletionSource _opened;

        public AsyncGate([CallerMemberName]string? name = null, [CallerLineNumber]int lineNumber = 0)
        {
            _name = $"{name}:{lineNumber}";

            _reached = NewTaskCompletionSource();
            _opened = NewTaskCompletionSource();

        }

        /// <summary>
        /// Lets code block until the gate is reached. Typically this is called by test code.
        /// This method automatically times out and fails if the gate is not reached.
        /// </summary>
        /// <returns></returns>
        public Task WaitToBeReached()
        {
            lock (_lock)
            {
                return _reached.Task.WaitBounded();
            }
        }

        /// <summary>
        /// Signals that code has reached the gate, and waits for it to be opened. Typically this is called by code injected
        /// into the system being tested.
        ///
        /// Note you should always block on this function, because if you do something after calling this but before
        /// waiting on the task, that will be a race condition.
        /// </summary>
        public Task ReachAndWait(CancellationToken cancellationToken = default)
        {
            Task task;

            lock (_lock)
            {
                // capture the current task BEFORE signaling that we have reached the gate
                task = _opened.Task;
                _reached.TrySetResult();
            }

            return task.WaitBounded();
        }

        /// <summary>
        /// Lets the code waiting at the gate continue execution. Typically this is called by test code.
        /// </summary>
        public void Open()
        {
            lock (_lock)
            {
                _opened.SetResult();
            }
        }

        /// <summary>
        /// Synchronously opens the gate and shuts it. Any code already waiting is guaranteed to be released,
        /// but no code not yet waiting will be able to slip through the open gate.
        /// </summary>
        public void OpenAndShut()
        {
            lock (_lock)
            {
                _opened.SetResult();

                _reached = NewTaskCompletionSource();
                _opened = NewTaskCompletionSource();
            }
        }

        /// <summary>
        /// Makes sure the gate is reached, lets the code through, and shuts the gate.
        ///
        /// In general, tests should not assert anything about the code at a reusable gate if they call this function,
        /// because the gate is opened and the code may go through and come back to wait at the gate again. It is only safe
        /// to use this function if there is some other aspect of the system controlling when the code comes back to this gate.
        /// </summary>
        public async Task WaitToBeReachedAndAllowThrough()
        {
            Task reachedTask;

            lock (_lock)
            {
                reachedTask = _reached.Task;
            }

            await reachedTask.WaitBounded();

            OpenAndShut();
        }

        public bool IsOpened()
        {
            lock (_lock)
            {
                return _opened.Task.IsCompleted;
            }
        }

        /// <summary>
        /// This can be used to validate that the gate has NOT been reached yet, but this is a race condition.
        /// EnsureGateNotReached() reduces the race condition but makes the tests take longer.
        ///
        /// It really does not make sense to use this function to validate that the gate HAS been reached. Use WaitToBeReached() for that.
        /// </summary>
        /// <returns></returns>
        public bool IsReached()
        {
            lock (_lock)
            {
                return _reached.Task.IsCompleted;
            }
        }

        /// <summary>
        /// Validates that the gate does not get reached in a short time. See the WaitForNonOccurrence extension method on Task
        /// for the issues with this method.
        /// </summary>
        /// <returns></returns>
        public Task EnsureGateNotReached()
        {
            Task task;
            lock (_lock)
            {
                task = _reached.Task;
            }

            return task.WaitForNonOccurrence();
        }

        /// <summary>
        /// Shuts the gate so it can be opened again.
        /// </summary>
        /// <param name="force">If true, the gate is shut even if it has not been opened. This will almost certainly
        /// cause race conditions!</param>
        public void Shut(bool force = false)
        {
            lock (_lock)
            {
                if (!force)
                {
                    if (!_reached.Task.IsCompleted)
                    {
                        throw new InvalidOperationException("The gate has not been reached yet");
                    }

                    if (!_opened.Task.IsCompleted)
                    {
                        throw new InvalidOperationException("The gate has not been opened yet");
                    }
                }

                _reached = NewTaskCompletionSource();
                _opened = NewTaskCompletionSource();
            }
        }

        private static TaskCompletionSource NewTaskCompletionSource()
        {
            return new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    public static class TaskExtensions
    {
        public static Task WaitBounded(this Task task, int timeout = 30000)
        {
            return task.WaitBounded(TimeSpan.FromMilliseconds(timeout));
        }
        
        public static async Task WaitBounded(this Task task, TimeSpan? timeout)
        {
            timeout ??= TimeSpan.FromMilliseconds(30000);

            Task<Task> waitTask = Task.WhenAny(task, Task.Delay(timeout.Value));
            await waitTask;

            // unwrap our exception
            if (task.Exception != null)
            {
                task.GetAwaiter().GetResult();
            }

            if (waitTask.Result != task)
            {
                throw new TimeoutException();
            }
        }
        
        public static Task WaitForNonOccurrence(this Task task)
        {
            return WaitForNonOccurrence(20, task);
        }

        public static async Task WaitForNonOccurrence(int timeout, params Task[] tasks)
        {
            await Task.Delay(timeout);
            foreach (var task in tasks)
            {
                if (task.IsCompletedSuccessfully)
                {
                    throw new InvalidOperationException("Task succeeded unexpectedly");
                }

                if (task.IsFaulted)
                {
                    throw task.Exception!.GetBaseException();
                }

                if (task.IsCanceled)
                {
                    throw new InvalidOperationException("Task was canceled unexpectedly");
                }
            }
        }
    }
    
}
