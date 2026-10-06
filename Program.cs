using System;
using System.Text;

namespace Auction
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            var manager = new Auction();

            Console.WriteLine(new string('=', 60));
            Console.WriteLine("  ОНЛАЙН-АУКЦИОН");
            Console.WriteLine(new string('=', 60));
            Console.WriteLine();

            manager.RunCASVersion();

            Console.WriteLine("\n" + new string('-', 60));
            Console.WriteLine("  CAS недостаточно для пары «ставка + лидер», \n" +
                                "т.к. изменяется одна переменная, вторая не успевает");
            Console.WriteLine(new string('-', 60));
            
            manager.RunLockVersion();
            Console.WriteLine();
            Console.WriteLine(new string('=', 60));
            Console.WriteLine("\t\tАУКЦИОН  ЗАКРЫТ ");
            Console.WriteLine(new string('=', 60));
        }
    }
}
