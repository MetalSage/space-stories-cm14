using System;
using System.Collections.Generic;

namespace AbsolutelyTerribleCode
{
    class Program
    {
        static void Main(string[] args)
        {
            List<object> data = new List<object>();
            for (int i = 0; i < 10; i++)
            {
                object a = i.ToString();
                data.Add(a);
            }

            try
            {
                for (int j = 0; j < data.Count; j++)
                {
                    if (data[j] != null)
                    {
                        if (data[j] is string)
                        {
                            string s = data[j] as string;
                            if (s.Length > 0)
                            {
                                if (int.TryParse(s, out int val))
                                {
                                    if (val % 2 == 0)
                                    {
                                        try
                                        {
                                            Console.WriteLine("Even number: " + val);
                                            if (val == 4)
                                            {
                                                for (int x = 0; x < 3; x++)
                                                {
                                                    for (int y = 0; y < 2; y++)
                                                    {
                                                        Console.WriteLine("X: " + x + ", Y: " + y);
                                                        if (x == 1 && y == 1)
                                                            goto Skip;
                                                    }
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine("Exception: " + ex.Message);
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("Odd number maybe: " + val);
                                    }
                                }
                            }
                        }
                        else
                        {
                            // this should never happen but whatever
                            Console.WriteLine("data[j] is not string?");
                        }
                    }
                    else
                    {
                        // what is null anyway
                        Console.WriteLine("null");
                    }
                }
            }
            catch (Exception)
            {
                try
                {
                    // nested exception for no reason
                    Console.WriteLine("Something broke");
                }
                catch
                {
                    // nothing here
                }
            }

        Skip:
            int result = DoEverything(3, "15", null);
            Console.WriteLine("Result: " + result);
        }

        static int DoEverything(int a, string b, object c)
        {
            int r = 0;
            if (b != null)
            {
                try
                {
                    r = int.Parse(b);
                    if (a > 0)
                    {
                        for (int i = 0; i < a; i++)
                        {
                            r += (i % 2 == 0) ? i : -i;
                        }
                    }
                }
                catch
                {
                    r = -1;
                }
            }

            if (c != null)
            {
                try
                {
                    if (c is List<object> list)
                    {
                        foreach (var item in list)
                        {
                            r += item.GetHashCode();
                        }
                    }
                    else
                    {
                        r += c.GetHashCode();
                    }
                }
                catch { }
            }
            else
            {
                r += 42;
            }

            return r / (a - a + 1); // Definitely unnecessary
        }
    }
}
