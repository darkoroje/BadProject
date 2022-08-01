using BadProject;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Runtime.Caching;
using System.Threading;
using System.Threading.Tasks;
using ThirdParty;

namespace Adv
{
    public class AdvertisementService
    {
        private class Request
        {
            public string id;  // id of the advertisment
            public TaskCompletionSource<Advertisement> result;    // when this task completes, the Advertisment is ready or task will be faulted
        }

        // if service is singleton, as this one would probably be, this should not be static
        // but leaving as is for now
        private static MemoryCache cache = new MemoryCache(" ");
        private static Queue<DateTime> errors = new Queue<DateTime>();
        
        private readonly IAdvProvider mainProvider;
        private readonly IAdvProvider backupProvider;
        private CancellationTokenSource cancel = new CancellationTokenSource(); // set to stop all operations
        private BlockingCollection<Request> requests;
        private Task worker = null;
        int retryCount;

        // I assume there will be some DI framerwork (such as asp.net.core) which will construct this service
        public AdvertisementService(IAdvProvider mainProvider, IAdvProvider backupProvider)
        {
            requests = new BlockingCollection<Request>();
            this.mainProvider = mainProvider;
            this.backupProvider = backupProvider;
        }

        private Task<Advertisement> MakeRequest(string advertismentId)
        {
            var tcs = new TaskCompletionSource<Advertisement>();
            requests.Add(new Request { result = tcs, id = advertismentId });
            return tcs.Task;
        }

        public void StartService()
        {
            // called by framework to start the service (e.g. StartAsync)
            worker = Task.Factory.StartNew(() => Worker(cancel.Token), TaskCreationOptions.LongRunning).Unwrap();
            // this should probably be passed in as well
            if (!int.TryParse(ConfigurationManager.AppSettings["RetryCount"], out retryCount))
                retryCount = 3;
        }

        public void StopService()
        {
            // called by framework to stop the service (e.g. StopAsync)
            if (worker != null)
            {
                cancel.Cancel();
                if (!worker.Wait(TimeSpan.FromSeconds(100))) // use appropriate timeout
                {
                    // worker thread did not stop in time, something is wrong; log or do something
                }
            }
        }

        // **************************************************************************************************
        // Loads Advertisement information by id
        // from cache or if not possible uses the "mainProvider" or if not possible uses the "backupProvider"
        // **************************************************************************************************
        // Detailed Logic:
        // 
        // 1. Tries to use cache (and retuns the data or goes to STEP2)
        //
        // 2. If the cache is empty it uses the NoSqlDataProvider (mainProvider), 
        //    in case of an error it retries it as many times as needed based on AppSettings
        //    (returns the data if possible or goes to STEP3)
        //
        // 3. If it can't retrive the data or the ErrorCount in the last hour is more than 10, 
        //    it uses the SqlDataProvider (backupProvider)

        public Advertisement GetAdvertisement(string id)
        {
            var key = $"AdvKey_{id}";
            var adv = (Advertisement)cache.Get(key);
            if (adv != null)
                // we already have the advertisment in cache, nothing more to do
                return adv;
            try
            {
                var workTask = MakeRequest(id);
                adv =  workTask.Result;
                cache.Set(key, adv, DateTimeOffset.Now.AddMinutes(5));
                return adv;
            }
            catch (Exception ex)
            {
                // I assume providers may raise an exception, if that is not the case this is not needed
                Console.WriteLine(ex.Message);  // log something somewhere
            }
            return null;
        }

        private int ErrorsInTheLastHour()
        {
            // Count HTTP error timestamps in the last hour
            while (errors.Count > 10) errors.Dequeue();
            int errorCount = 0;
            foreach (var dat in errors)
            {
                if (dat > DateTime.Now.AddHours(-1))
                {
                    errorCount++;
                }
            }
            return errorCount;
        }

        private async Task<Advertisement> GetAdvertisementUsingPrimaryProvider(string id, CancellationToken token)
        {
            Advertisement adv = null;
            var errorCount = ErrorsInTheLastHour();
            if ((errorCount < 10))
            {
                int retry = 0;
                do
                {
                    retry++;
                    try
                    {
                        adv = mainProvider.GetAdv(id);
                    }
                    catch
                    {
                        errors.Enqueue(DateTime.Now); // Store HTTP error timestamp     
                        await Task.Delay(1000, token);        
                    }
                } while ((adv == null) && (retry < retryCount));
            }
            return adv;
        }

        // ideally this would be async as well, but the underlying API is not
        private Advertisement GetAdvertisementUsingBackupProvider(string id)
        {
            var adv = backupProvider.GetAdv(id);
            return adv;
        }

        // process request by request, so we don't have to use locks. In the future there might be more than one
        // thread if the underlying third-party API were to allow it.
        private async Task Worker(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (requests.TryTake(out var request, 1000, token))
                    {
                        try
                        {
                            var key = $"AdvKey_{request.id}";
                            var adv = (Advertisement)cache.Get(key); // double-check just in case
                            if (adv == null)
                                adv = await GetAdvertisementUsingPrimaryProvider(request.id, token);
                            if (adv == null)
                                adv = GetAdvertisementUsingBackupProvider(request.id);
                            request.result.SetResult(adv);
                        }
                        catch (OperationCanceledException)
                        {
                            ; // thread will terminate
                            request.result.SetResult(null);
                        }
                        catch (Exception ex)
                        {
                            // something has gone wrong, pass to caller
                            request.result.SetException(ex);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }

        }


    }
}
