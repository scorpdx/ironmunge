using ironmunge.Plugins;
using System;

namespace HelloPlugin
{
    public class HelloMunger : IMunger
    {
        public string Name { get => "hello"; }
        public string Description { get => "Displays hello message."; }

        public int Execute()
        {
            Console.WriteLine("Hello !!!");
            return 0;
        }
    }
}
