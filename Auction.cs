using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace Auction
{
    public class Auction
    {
        private int currentBid;
        private int leaderId;
        private int isClosed;

        private Stopwatch sw;

        private readonly List<(int bidder, int amount, long ms)> journal = 
                       new List<(int bidder, int amount, long ms)>();
        private readonly object journalLock = new object();
        private readonly object bidLock = new object();

        const int PARTICIPANTS = 8;
        const int STARTPRICE = 1000;
        private long ElapsedMs()
        {
            return sw.ElapsedMilliseconds;
        }

        public void Reset()
        {
            currentBid = STARTPRICE;
            leaderId = -1;
            isClosed = 0;
            lock (journalLock)
            {
                journal.Clear();
            }
            sw = Stopwatch.StartNew();
        }
        //1
        public void RunCASVersion()
        {
            Console.WriteLine("===== Часть 1: CAS =====");
            Console.WriteLine($"Стартовая цена: {STARTPRICE} грн");

            Reset();
            Thread molotok = new Thread(() =>
            {
                Thread.Sleep(3000);
                int old = Interlocked.Exchange(ref isClosed, 1);

                if (old == 0)
                {
                    Console.WriteLine($"\n(МОЛОТОК) Торги закрыты");
                    Console.WriteLine($"Победитель: учасник {leaderId}");
                    Console.WriteLine($"Цена: {currentBid} грн");
                }
            });
            Thread[] threads = new Thread[PARTICIPANTS];
            for (int i = 0; i < PARTICIPANTS; i++)
            {
                int id = i;
                threads[i] = new Thread(() =>
                {
                    Random rdn = new Random(id * 17 + Environment.TickCount);

                    while (true)
                    {
                        if (ElapsedMs() >= 3000)
                        {
                            int old = Interlocked.Exchange(ref isClosed, 1);

                            if (old == 0)
                            {
                                Console.WriteLine("\n--- Торги закрыты ---");
                                Console.WriteLine($"Победитель: учасник {leaderId}");
                                Console.WriteLine($"Цена: {currentBid} грн");
                            }
                            break;
                        }
                        if (Volatile.Read(ref isClosed) == 1) break;
                        if (rdn.NextDouble() < 0.35)
                        {
                            int current = Volatile.Read(ref currentBid);
                            int myBid = current + rdn.Next(50, 501);

                            while (true)
                            {
                                current = Volatile.Read(ref currentBid);
                                if (myBid <= current) break;

                                int result = Interlocked.CompareExchange(
                                    ref currentBid, myBid, current);
                                if (result == current)
                                {
                                    Thread.Sleep(1);
                                    leaderId = id;

                                    lock (journalLock)
                                    {
                                        journal.Add(
                                            (id, myBid, ElapsedMs()));
                                    }
                                    break;
                                }
                            }
                        }
                        Thread.Sleep(rdn.Next(10, 50));
                    }
                });
                threads[i].Start();
            }
            molotok.Start();
            foreach (var t in threads)
                t.Join();

            molotok.Join();

            Console.WriteLine("\n--- Первая CAS версия ---");
            lock (journalLock)
            {
                Console.WriteLine($"Журнал:  {journal.Count} ставок");
                foreach (var j in journal)
                {
                    Console.WriteLine($" Участник:{j.bidder}  Ставка: {j.amount}  Время: {j.ms}");
                }
                if (journal.Count > 0)
                {
                    var last = journal[journal.Count - 1];
                    Console.WriteLine($"\nЗапущено участников: {PARTICIPANTS}");
                    Console.WriteLine($"Последняя запись: участник {last.bidder}, {last.amount} грн");
                    Console.WriteLine($"Последняя ставка: участник {last.bidder}");
                    Console.WriteLine($" {(last.bidder == leaderId ? "ЕСТЬ СОВПАДЕНИЯ" : "НЕТ СОВПАДЕНИЙ— rase condition")}");
                }
            }
        }
        //2
        public void RunLockVersion()
        {
            Console.WriteLine("===== Часть 2: LOCK-версия(ставка + лидер атомарно) =====");
            Console.WriteLine($"Стартовая цена: {STARTPRICE} грн\n");

            Reset();
            Thread molotok = new Thread(() =>
            {
                Thread.Sleep(3000);
                int old = Interlocked.Exchange(ref isClosed, 1);

                if (old == 0)
                {
                    Console.WriteLine("\n(МОЛОТОК):  Продано");
                    Console.WriteLine($"Победитель: учасник {leaderId}");
                    Console.WriteLine($"Цена: {currentBid} грн");
                }
            });

            Thread[] threads = new Thread[PARTICIPANTS];
            for (int i = 0; i < PARTICIPANTS; i++)
            {
                int id = i;
                threads[i] = new Thread(() =>
                {
                    Random rdn = new Random(id * 17 + Environment.TickCount);

                    while (true)
                    {
                        if (ElapsedMs() >= 3000)
                        {
                            int old = Interlocked.Exchange(ref isClosed, 1);
                            if (old == 0)
                            {
                                Console.WriteLine($"--- Торги закрыты ---");
                                Console.WriteLine($"Победитель: учасник {leaderId}");
                                Console.WriteLine($"Цена: {currentBid} грн");
                            }
                            break;
                        }
                        if (Volatile.Read(ref isClosed) == 1) break;
                        if (rdn.NextDouble() < 0.35)
                        {
                            lock (bidLock)
                            {
                                if (Volatile.Read(ref isClosed) == 1)
                                {
                                    break;
                                }

                                int current = Volatile.Read(ref currentBid);
                                int myBid = current + rdn.Next(50, 501);

                                if (myBid > currentBid)
                                {
                                    currentBid = myBid;
                                    leaderId = id;

                                    lock (journalLock)
                                    {
                                        journal.Add((id, myBid, ElapsedMs()));
                                    }
                                }
                            }
                        }
                        Thread.Sleep(rdn.Next(10, 50));
                    }                   
                });
                threads[i].Start();
            }

            molotok.Start();
            foreach (var t in threads)
                t.Join();              
            molotok.Join();

            Console.WriteLine("\n--- Журнал ставок ---");
            lock (journalLock)
            {
                Console.WriteLine($"Всего ставок:  {journal.Count}");
                bool increase = true;

                for (int j = 0; j < journal.Count; j++)
                {
                    var r = journal[j];
                    Console.WriteLine($" {j + 1}. Участник:{r.bidder}" +
                                      $"  Ставка: {r.amount} грн " +
                                      $" Время: {r.ms}");
                    if (j > 0 && r.amount <= journal[j - 1].amount)
                    {
                        increase = false;
                        Console.WriteLine("сумма не растет");
                    }
                }
                Console.WriteLine($"\nСтавки растут: {(increase ? "YES" : "NON")}");

                if (journal.Count > 0)
                {
                    var last = journal[journal.Count - 1];
                    Console.WriteLine($"Последняя запись: участник {last.bidder}, {last.amount} грн");
                    Console.WriteLine($"Победитель: участник: {last.bidder}, цена: {last.amount} грн");
                    bool match = last.bidder == leaderId && last.amount == currentBid;
                    Console.WriteLine($" {(match ? "ЕСТЬ СОВПАДЕНИЯ" : "НЕТ СОВПАДЕНИЙ")}");
                }
            }            
        }
    }  
}
