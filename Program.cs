using System;
using System.Collections.Generic;
using System.Linq;

namespace DominoGame
{
    public class Program
    {
        public static void Main()
        {
            Console.Write("Jumlah player: ");
            int count = int.Parse(Console.ReadLine()!);

            var controller = new GameController(50);

            for (int i = 0; i < count; i++)
            {
                Console.Write($"Nama player {i + 1}: ");
                string name = Console.ReadLine()!;
                controller.AddPlayer(new Player(name));
            }

            controller.SortPlayersByName();

            controller.StartGame();
        }

    }
}
