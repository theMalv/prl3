using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Parallel3
{
    class Program
    {
        private static int N = 100;
        private static int M = 5;

        private static List<int> FirstPart(int n)
        {
            var res = new List<int>() {2};
            var numbers = Enumerable.Range(3, n - 2);
            foreach (var number in numbers)
            {
                var result = res.Find(x => number % x == 0);
                if (result == 0)
                    res.Add(number);
            }

            return res;
        }


        #region singleThread

        static void SingleThreadEratosphen()
        {
            var nSqrt = Convert.ToInt32(Math.Sqrt(N));
            var firstPartResult = FirstPart(nSqrt);
            var result = firstPartResult;
            var numbers = Enumerable.Range(nSqrt, N - nSqrt + 1);
            foreach (var number in numbers)
            {
                var isSimple = firstPartResult.Find(x => number % x == 0);
                if (isSimple == 0)
                    result.Add(number);
            }
        }

        #endregion

        static void MultiThreadSingleDecompose()
        {
            var nSqrt = Convert.ToInt32(Math.Sqrt(N));
            var firstPartResult = FirstPart(nSqrt);
            var threads = new Thread[M];

            var result = firstPartResult;
            for (var thread = 0; thread < M; thread++)
            {
                threads[thread] = new Thread((range) =>
                {
                    var array = range as object[];
                    var begin = Convert.ToInt32(array[0]);
                    var end = Convert.ToInt32(array[1]);

                    var numbers = Enumerable.Range(begin, end - begin + 1).ToList();
                    foreach (var number in numbers)
                    {
                        var isSimple = firstPartResult.Find(x => number % x == 0);
                        if (isSimple == 0)
                            result.Add(number);
                    }
                });

                var length = N - nSqrt;
                var threadLength = length / M;
                var start = threadLength * thread;
                var end = ((thread + 1) != M) ? threadLength * (thread + 1) : length;
                threads[thread].Start(new object[] {start + nSqrt, end + nSqrt});
            }

            foreach (var thread in threads)
                thread.Join();
        }

        static void MultiThreadDoubleDecompose()
        {
            var nSqrt = Convert.ToInt32(Math.Sqrt(N));
            var firstPartResult = FirstPart(nSqrt);
            var threads = new Thread[M];

            var result = firstPartResult;
            for (var thread = 0; thread < M; thread++)
            {
                threads[thread] = new Thread((range) =>
                {
                    var array = range as object[];
                    var thread = Convert.ToInt32(array[2]);

                    var slice = GetSlice(thread, firstPartResult.Count());

                    List<int> GetSlice(int thread, int count)
                    {
                        var sliceLength = count / M;
                        var sliceEnd = ((thread + 1) != M)
                            ? sliceLength * (thread + 1)
                            : count;
                        var slice = new List<int>() {2, 3, 5, 7};

                        for (var i = sliceLength * thread; i < sliceEnd; i++)
                            slice.Add(firstPartResult[i]);
                        return slice;
                    }

                    var begin = Convert.ToInt32(array[0]);
                    var end = Convert.ToInt32(array[1]);

                    var numbers = Enumerable.Range(begin, end - begin + 1).ToList();
                    foreach (var number in numbers)
                    {
                        var isSimple = slice.Find(x => number % x == 0);
                        if (isSimple == 0)
                            result.Add(number);
                    }
                });

                var length = N - nSqrt;
                var threadLength = length / M;
                var startPoint = threadLength * thread;
                var endPoint = ((thread + 1) != M) ? threadLength * (thread + 1) : length;
                threads[thread].Start(new object[] {startPoint + nSqrt, endPoint + nSqrt, thread});
            }

            foreach (var thread in threads)
                thread.Join();
        }

        #region ThreadPool

        static int _numberOfThreadsNotYetCompleted;
        private static ManualResetEvent _doneEvent;

        static List<int> _numbersFromNsqrtToN;

        static void ThreadPoolMethod()
        {
            var nSqrt = Convert.ToInt32(Math.Sqrt(N));
            _numbersFromNsqrtToN = Enumerable.Range(nSqrt, N - nSqrt + 1).ToList();
            var baseNumbers = FirstPart(nSqrt);
            _numberOfThreadsNotYetCompleted = baseNumbers.Count();
            _doneEvent = new ManualResetEvent(false);

            static void Run(object o)
            {
                try
                {
                    var baseNumber = Convert.ToInt32(o);
                    var length = _numbersFromNsqrtToN.Count();
                    for (var i = 0; i < length; i++)
                    {
                        if (_numbersFromNsqrtToN[i] % baseNumber == 0)
                            _numbersFromNsqrtToN.RemoveAt(i);

                        length = _numbersFromNsqrtToN.Count();
                    }
                }
                finally
                {
                    if (Interlocked.Decrement(ref _numberOfThreadsNotYetCompleted) == 0)
                        _doneEvent.Set();
                }
            }

            for (var i = 0; i < baseNumbers.Count(); i++)
                ThreadPool.QueueUserWorkItem(new WaitCallback(Run), (object) baseNumbers[i]);

            _doneEvent.WaitOne();
        }

        #endregion

        static object _locker = new object();
        private static List<int> _baseNumbers;

        private static void LockStateMultiThread()
        {
            var nSqrt = Convert.ToInt32(Math.Sqrt(N));
            _numbersFromNsqrtToN = Enumerable.Range(nSqrt, N - nSqrt + 1).ToList();
            _baseNumbers = FirstPart(nSqrt);
            _doneEvent = new ManualResetEvent(false);
            _numberOfThreadsNotYetCompleted = _baseNumbers.Count();

            static void Run(object o)
            {
                var currentIndex = 0;
                while (true)
                {
                    if (currentIndex >= _baseNumbers.Count)
                        break;
                    var currentPrime = _baseNumbers[currentIndex];
                    currentIndex++;
                    try
                    {
                        lock (_locker)
                        {
                            var length = _numbersFromNsqrtToN.Count;
                            for (var i = 0; i < length; i++)
                            {
                                if (_numbersFromNsqrtToN[i] % currentPrime == 0)
                                    _numbersFromNsqrtToN.RemoveAt(i);
                                length = _numbersFromNsqrtToN.Count;
                            }
                        }
                    }
                    finally
                    {
                        if (Interlocked.Decrement(ref _numberOfThreadsNotYetCompleted) == 0)
                            _doneEvent.Set();
                    }
                }
            }

            for (var i = 0; i < M; i++)
                ThreadPool.QueueUserWorkItem(new WaitCallback(Run));

            _doneEvent.WaitOne();
        }

        static void StandMeasure(Action methodToExecute)
        {
            var timer = new Stopwatch();
            timer.Start();
            methodToExecute();
            timer.Stop();
            Console.WriteLine($"{timer.Elapsed:g}");
        }

        static void Main(string[] args)
        {
            var range = new[] {1000, 10000, 100000};
            //var range = new [] {10000};
            var threads = new[] {8};
            //var threads = new[] {2,5,8,10};
            foreach (var thread in threads)
            {
                M = thread;
                foreach (var n in range)
                {
                    N = n;
                    // StandMeasure(SingleThreadEratosphen);
                    // StandMeasure(MultiThreadSingleDecompose);
                    // StandMeasure(MultiThreadDoubleDecompose);
                    StandMeasure(ThreadPoolMethod);
                    //StandMeasure(LockStateMultiThread);
                }

                Console.WriteLine(Environment.NewLine);
            }
        }
    }
}