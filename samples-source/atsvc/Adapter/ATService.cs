using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ATSvc.Adapter
{

    public struct JobInfo
    {
        /// <summary>
        /// The command being executed.
        /// </summary>
        public string Command;

        /// <summary>
        /// The time in milliseconds after midnight when the
        /// command is executed.
        /// </summary>
        public int Time;

        public override string ToString()
        {
            return "Command: " + Command + " Time: " + Time;
        }

    }


    public static class ATService
    {
        /// <summary>
        /// The process to execute "at.exe"
        /// </summary>
        static Process process;

        /// <summary>
        /// The dictionary used to map concrete job ID and abstract job ID
        /// </summary>
        static Dictionary<int, int> jobIdList;

        /// <summary>
        /// Free id pool which contains all deallocated abstract IDs
        /// </summary>
        static List<int> freeIds;

        public static void Initialize()
        {
            jobIdList = new Dictionary<int, int>();
            freeIds = new List<int>();
            process = new Process();
            process.StartInfo.FileName = "at.exe";
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
        }

        public static void CleanUp()
        {
            jobIdList.Clear();
            freeIds.Clear();
            DeleteAllJobs();

        }


        #region ATService Members

        public static bool AddJob(JobInfo info, out int jobId)
        {
            string jobTime = TimeSpan.FromMilliseconds(info.Time).ToString();
            process.StartInfo.Arguments = jobTime + " " + info.Command;
            List<string> result = ExecuteCmd();

            if (result.Count != 1)
            {
                Assert.Fail("Add job {0} failed", info);
            }

            if (freeIds.Count > 0)
            {
                jobId = freeIds[0];
                freeIds.RemoveAt(0);
            }
            else
            {
                jobId = jobIdList.Count + 1;
            }

            try
            {
                string[] addJobStrArray = result[0].Split('=');
                jobIdList.Add(jobId, Int32.Parse(addJobStrArray[1]));
            }
            catch (Exception e)
            {
                Assert.Fail("Add job failed " + e.Message);
            }

            return true;
        }

        public static bool DeleteJob(int abstractId)
        {
            process.StartInfo.Arguments = jobIdList[abstractId] + " " + "/delete";
            List<string> result = ExecuteCmd();
            if (result.Count != 0)
            {
                Assert.Fail("Delete job {0} failed", abstractId);
            }
            jobIdList.Remove(abstractId);
            freeIds.Add(abstractId);
            freeIds.Sort();

            return true;
        }

        public static void DeleteAllJobs()
        {
            foreach (KeyValuePair<int, int> kvp in jobIdList)
            {
                DeleteJob(kvp.Key);
            }
        }

        public static bool GetJobInfo(int abstractId, out JobInfo info)
        {
            process.StartInfo.Arguments = jobIdList[abstractId].ToString();
            List<string> result = ExecuteCmd();
            info = new JobInfo();

            //Analyze the output and get the command and time information
            try
            {
                string[] jobTimeStrArray = result[4].Split(':', ' ');
                string[] jobNameStrArray = result[6].Split(':');
                DateTime taskTime = DateTime.Parse(jobTimeStrArray[6] + ":" + jobTimeStrArray[7] + " " + jobTimeStrArray[8]);
                DateTime startTime = DateTime.Parse("00:00:00");
                info.Time = (int)(Math.Round(taskTime.TimeOfDay.TotalMilliseconds - startTime.TimeOfDay.TotalMilliseconds));
                info.Command = jobNameStrArray[1].TrimStart();
            }
            catch (Exception e)
            {
                Assert.Fail("GetJobInfo failed " + e.Message);
            }

            return true;
        }

        static List<string> ExecuteCmd()
        {
            List<string> resultList = new List<string>();
            string line;
            process.Start();
            process.WaitForExit();
            while ((line = process.StandardOutput.ReadLine()) != null)
            {
                resultList.Add(line);
            }
            if (resultList.Count>0 && resultList[0] == "Access is denied.")
                Assert.Fail("Access is denied, please use administrator permission to run test cases");
            return resultList;
        }

        #endregion
    }
}
