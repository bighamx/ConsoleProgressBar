using System;
using System.Threading;
using Chuckie.ConsoleProgressBar;

namespace Chuckie.ConsoleProgressBar.Example
{
    class Program
    {
        static void Main(string[] args)
        {
            // 启用Unicode支持以确保样式正确渲染
            ProgressBarChars.EnableUnicodeSupport();

            Console.WriteLine("=== ConsoleProgressBar Demo ===");
            Console.WriteLine("This demo showcases different styles and features.");
            Console.WriteLine();

            DemoSingleProgressBar();
            DemoMultipleStyles();
            DemoMultiProgressBar();

            Console.WriteLine("\nDemo completed! Press any key to exit.");
            Console.ReadKey();
        }

        static void DemoSingleProgressBar()
        {
            Console.WriteLine("1. Single Progress Bar (BrailleFine Style - Default)");
            
            int total = 100;
            using (var progressBar = new ConsoleProgressBar(total, "Downloading file...", 50, ProgressBarStyle.BrailleFine, ConsoleColor.Cyan))
            {
                for (int i = 0; i <= total; i++)
                {
                    progressBar.Update(i, $"Downloading file... {i}/{total} MB");
                    
                    // Simulate some log output
                    if (i == 30) progressBar.WriteLine("Info: Connection speed increased.", ConsoleColor.Yellow);
                    if (i == 70) progressBar.WriteLine("Warning: Packet loss detected, retrying...", ConsoleColor.Red);

                    Thread.Sleep(50);
                }
                progressBar.Complete("Download finished successfully!");
            }
            Console.WriteLine();
        }

        static void DemoMultipleStyles()
        {
            Console.WriteLine("2. Showcasing Different Built-in Styles");

            var styles = new[]
            {
                ProgressBarStyle.Classic,
                ProgressBarStyle.Ascii,
                ProgressBarStyle.Arrow,
                ProgressBarStyle.Dots,
                ProgressBarStyle.Gradient,
                ProgressBarStyle.Line
            };

            foreach (var style in styles)
            {
                Console.WriteLine($"\nStyle: {style}");
                using (var progressBar = new ConsoleProgressBar(50, "Processing...", 40, style))
                {
                    for (int i = 0; i <= 50; i++)
                    {
                        progressBar.Update(i);
                        Thread.Sleep(20);
                    }
                }
            }
            Console.WriteLine();
        }

        static void DemoMultiProgressBar()
        {
            Console.WriteLine("3. Multi-Task Parallel Progress Bar");
            
            using (var multiBar = new MultiProgressBar(40))
            {
                int task1 = multiBar.AddProgressBar("Download", 100, ConsoleColor.Cyan, ProgressBarStyle.BrailleFine);
                int task2 = multiBar.AddProgressBar("Extract", 50, ConsoleColor.Yellow, ProgressBarStyle.Classic);
                int task3 = multiBar.AddProgressBar("Install", 200, ConsoleColor.Green, ProgressBarStyle.Gradient);

                int t1 = 0, t2 = 0, t3 = 0;
                var random = new Random();

                while (t1 < 100 || t2 < 50 || t3 < 200)
                {
                    if (t1 < 100 && random.Next(10) > 2)
                    {
                        t1 += random.Next(1, 4);
                        multiBar.Update(task1, t1, $"Downloading part {t1}");
                    }

                    if (t1 > 30 && t2 < 50 && random.Next(10) > 4)
                    {
                        t2 += random.Next(1, 3);
                        multiBar.Update(task2, t2, $"Extracting file {t2}");
                    }

                    if (t2 > 20 && t3 < 200 && random.Next(10) > 1)
                    {
                        t3 += random.Next(1, 6);
                        multiBar.Update(task3, t3, $"Installing module {t3}");
                    }

                    Thread.Sleep(50);
                }
            }
            Console.WriteLine("All tasks completed.");
        }
    }
}
