using System;
using System.Collections.Generic;
using System.Reactive.Subjects;
using System.Timers;
using log4net;

namespace Sources
{
    public abstract class CompetitionSource : IObservable<Competition>
    {
        private Subject<Competition> subject = new Subject<Competition>();

        protected readonly ILog _logger;
        private Timer checkTimer;

        public IDisposable Subscribe(IObserver<Competition> observer)
        {
            return subject.Subscribe(observer);
        }

        protected CompetitionSource(int checkInterval, string loggerName)
        {
            _logger = LogManager.GetLogger(loggerName);
            checkTimer = new System.Timers.Timer(checkInterval);
            checkTimer.Elapsed += CheckTimerOnElapsed;
        }

        public void Start()
        {
            checkTimer.Start();
            CheckTimerOnElapsed(null, null);
        }

        public void CheckTimerOnElapsed(object sender, ElapsedEventArgs elapsedEventArgs)
        {
            try
            {
                foreach (var c in GetCompetitions())
                {
                    subject.OnNext(c);
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Error checking for new items", ex);   
            }
        }

        protected abstract IEnumerable<Competition> GetCompetitions();
    }
}