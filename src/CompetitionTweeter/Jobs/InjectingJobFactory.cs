using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Quartz;
using Quartz.Spi;
using TinyIoC;

namespace CompetitionTweeter.Jobs
{
    public class InjectingJobFactory : IJobFactory
    {
        public IJob NewJob(TriggerFiredBundle bundle, IScheduler scheduler)
        {
            try
            {
                var jobDetail = bundle.JobDetail;
                var jobType = jobDetail.JobType;
                return (IJob) TinyIoCContainer.Current.Resolve(jobType);
            }
            catch (Exception ex)
            {
                throw new SchedulerException("Problem Instantiating job class", ex);
            }
        }

        public void ReturnJob(IJob job)
        {
            //do nothing
        }
    }
}
