﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Umbraco.Core;
using Umbraco.Web.Scheduling;

namespace Umbraco.Tests.Scheduling
{
    [TestFixture]
    public class BackgroundTaskRunnerTests
    {


        [Test]
        public void Startup_And_Shutdown()
        {
            BackgroundTaskRunner<IBackgroundTask> tManager;
            using (tManager = new BackgroundTaskRunner<IBackgroundTask>(true, true))
            {
                tManager.StartUp();
            }

            NUnit.Framework.Assert.IsFalse(tManager.IsRunning);
        }

        [Test]
        public void Startup_Starts_Automatically()
        {
            BackgroundTaskRunner<BaseTask> tManager;
            using (tManager = new BackgroundTaskRunner<BaseTask>(true, true))
            {
                tManager.Add(new MyTask());
                NUnit.Framework.Assert.IsTrue(tManager.IsRunning);
            }
        }

        [Test]
        public void Task_Runs()
        {
            var myTask = new MyTask();
            var waitHandle = new ManualResetEvent(false);
            BackgroundTaskRunner<BaseTask> tManager;
            using (tManager = new BackgroundTaskRunner<BaseTask>(true, true))
            {
                tManager.TaskCompleted += (sender, task) => waitHandle.Set();

                tManager.Add(myTask);

                //wait for ITasks to complete
                waitHandle.WaitOne();

                NUnit.Framework.Assert.IsTrue(myTask.Ended != default(DateTime));
            }
        }

        [Test]
        public void Many_Tasks_Run()
        {
            var tasks = new Dictionary<BaseTask, ManualResetEvent>();
            for (var i = 0; i < 10; i++)
            {
                tasks.Add(new MyTask(), new ManualResetEvent(false));
            }

            BackgroundTaskRunner<BaseTask> tManager;
            using (tManager = new BackgroundTaskRunner<BaseTask>(true, true))
            {
                tManager.TaskCompleted += (sender, task) => tasks[task.Task].Set();

                tasks.ForEach(t => tManager.Add(t.Key));

                //wait for all ITasks to complete
                WaitHandle.WaitAll(tasks.Values.Select(x => (WaitHandle)x).ToArray());

                foreach (var task in tasks)
                {
                    NUnit.Framework.Assert.IsTrue(task.Key.Ended != default(DateTime));
                }
            }
        }

        [Test]
        public void Tasks_Can_Keep_Being_Added_And_Will_Execute()
        {
            Func<Dictionary<BaseTask, ManualResetEvent>> getTasks = () =>
            {
                var newTasks = new Dictionary<BaseTask, ManualResetEvent>();
                for (var i = 0; i < 10; i++)
                {
                    newTasks.Add(new MyTask(), new ManualResetEvent(false));
                }
                return newTasks;
            };

            IDictionary<BaseTask, ManualResetEvent> tasks = getTasks();

            BackgroundTaskRunner<BaseTask> tManager;
            using (tManager = new BackgroundTaskRunner<BaseTask>(true, true))
            {
                tManager.TaskCompleted += (sender, task) => tasks[task.Task].Set();

                //execute first batch
                tasks.ForEach(t => tManager.Add(t.Key));

                //wait for all ITasks to complete
                WaitHandle.WaitAll(tasks.Values.Select(x => (WaitHandle)x).ToArray());

                foreach (var task in tasks)
                {
                    NUnit.Framework.Assert.IsTrue(task.Key.Ended != default(DateTime));
                }

                //execute another batch after a bit
                Thread.Sleep(2000);

                tasks = getTasks();
                tasks.ForEach(t => tManager.Add(t.Key));

                //wait for all ITasks to complete
                WaitHandle.WaitAll(tasks.Values.Select(x => (WaitHandle)x).ToArray());

                foreach (var task in tasks)
                {
                    NUnit.Framework.Assert.IsTrue(task.Key.Ended != default(DateTime));
                }
            }
        }

        [Test]
        public void Task_Queue_Will_Be_Completed_Before_Shutdown()
        {
            var tasks = new Dictionary<BaseTask, ManualResetEvent>();
            for (var i = 0; i < 10; i++)
            {
                tasks.Add(new MyTask(), new ManualResetEvent(false));
            }

            BackgroundTaskRunner<BaseTask> tManager;
            using (tManager = new BackgroundTaskRunner<BaseTask>(true, true))
            {
                tManager.TaskCompleted += (sender, task) => tasks[task.Task].Set();

                tasks.ForEach(t => tManager.Add(t.Key));

                ////wait for all ITasks to complete
                //WaitHandle.WaitAll(tasks.Values.Select(x => (WaitHandle)x).ToArray());

                tManager.Stop(false);
                //immediate stop will block until complete - but since we are running on 
                // a single thread this doesn't really matter as the above will just process
                // until complete.
                tManager.Stop(true);

                NUnit.Framework.Assert.AreEqual(0, tManager.TaskCount);
            }
        }

      

       
        private class MyTask : BaseTask
        {
            public MyTask()
            {
            }

            public override void Run()
            {
                Thread.Sleep(500);
            }

            public override void Cancel()
            {

            }
        }

        public abstract class BaseTask : IBackgroundTask
        {
            public Guid UniqueId { get; protected set; }

            public abstract void Run();
            public abstract void Cancel();

            public DateTime Queued { get; set; }
            public DateTime Started { get; set; }
            public DateTime Ended { get; set; }

            public virtual void Dispose()
            {
                Ended = DateTime.Now;
            }
        }

    }
}
