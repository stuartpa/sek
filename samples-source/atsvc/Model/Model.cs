using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

using Microsoft.Modeling;

using ATSvc.Adapter;


namespace ATSvc.Model
{
    public static class ModelProgram
    {

        #region Parameters

        /// <summary>
        /// The time density of the server.
        /// </summary>
        public static int serverTimeDensity = 60000; // set this to one to observe a model/requirement bug
        //public static int serverTimeDensity = 1;

        /// <summary>
        /// The maximal value of milliseconds-after-midnight.
        /// </summary>
        public static int maxJobTime = 24 * 60 * 60 * 1000 - 1;

        /// <summary>
        /// A bound on jobs if greater zero.
        /// </summary>
        public static int JobBound = 0;

        #endregion

        #region State

        /// <summary>
        /// A container mapping job identifiers to job information.
        /// </summary>
        public static MapContainer<int, JobInfo> jobs = new MapContainer<int,JobInfo>();

       
        /// <summary>
        /// A property delivering the job identifiers currently in use.
        /// </summary>
        public static IEnumerable<int> JobIdsInUse
        {
            get { return jobs.Keys; }
        }

        /// <summary>
        /// A method to mark that a requirement has been captured.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="description"></param>
        static void Capture(int id, string description)
        {
            Requirement.Capture("MS-ATSVC_R" + id);
        }

        /// <summary>
        /// The condition which makes a state accepting.
        /// </summary>
        [AcceptingStateCondition]
        static bool JobCountIsZero
        {
            get { return jobs.Count == 0; }
        }

        /// <summary>
        /// A filter on states. Filters out states where job count exceeds the bound.
        /// </summary>
        [StateFilter]
        static bool JobCountBound
        {
            get { return JobBound == 0 || jobs.Count <= JobBound; }
        }

        [Probe]
        static int JobCount()
        {
            return jobs.Count;
        }

        #endregion

        #region Rule Methods

        [Rule]
        static bool AddJob(JobInfo info, out int jobId)
        {
            #region ErrorManagement
            if (info.Time < 0 || info.Time > maxJobTime)
            {
                jobId = 0;
                Capture(3, "Adding a job with an invalid time MUST result in an error");
                return false;
            } 
            #endregion
            jobId = AllocateUniqueId();
            info.Time = info.Time / serverTimeDensity * serverTimeDensity;
            jobs[jobId] = info;
            Capture(1, "When adding a job, the server MUST return a job identifier which is not associated with any previously added job.");
            return true;
        }

        [Rule]
        static bool DeleteJob([Domain("JobIdsInUse")]int id)
        {
            Condition.IsTrue(jobs.ContainsKey(id));
            jobs.Remove(id);
            DeallocateId(id);
            return true;
        }

                 
        [Rule]
        static bool GetJobInfo([Domain("JobIdsInUse")]int id, out JobInfo info)
        {
            Condition.IsTrue(jobs.ContainsKey(id));
            Capture(2, "When querying information about a job, the server MUST return the same information as specified at job creation time.");
            info = jobs[id];
            return true;
        }

        #endregion

        #region Helpers

        static int maxIdInUse = 0;

        /// <summary>
        /// Allocate a unique job identifier.
        /// </summary>
        /// <returns></returns>
        static int AllocateUniqueId()
        {
            int id = -1;
            for (int i = 1; i <= maxIdInUse; i++)
            {
                if (!jobs.ContainsKey(i))
                {
                    id = i;
                    break;
                }
            }
            if (id == -1)
            {
                id = ++maxIdInUse;
            }
            return id;
        }

        /// <summary>
        /// Deallocate a job identifier previously in use.
        /// </summary>
        /// <param name="id"></param>
        static void DeallocateId(int id)
        {
            if (id == maxIdInUse)
            {
                do
                {
                    maxIdInUse--;
                    if (jobs.ContainsKey(maxIdInUse))
                        break;
                }
                while (maxIdInUse > 0);
            }
        }

        #endregion

    }
}
